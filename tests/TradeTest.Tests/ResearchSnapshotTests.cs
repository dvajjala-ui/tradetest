using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;
using TradeTest.Infrastructure;

namespace TradeTest.Tests;

public sealed class ResearchSnapshotTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public async Task Pinned_snapshot_survives_a_concurrent_correction_and_new_reads_see_the_change()
    {
        using var temp = new TemporaryDatabase();
        var writer = new SqliteStore(temp.Path);
        await writer.InitializeAsync();
        await writer.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        var reader = new SqliteStore(temp.Path, readOnly: true);
        var original = await reader.ReadCompanySnapshotAsync("SYNTH", At.AddDays(3), "revenue");
        var historical = await reader.ReadCompanySnapshotAsync("SYNTH", At.AddDays(1), "revenue");
        await using var pinned = await reader.OpenResearchSnapshotAsync(); // Pin before any company query.
        var correction = Batch("b2", "d2", "f2", At.AddDays(2));
        correction = correction with
        {
            Documents = [correction.Documents[0] with { SupersedesDocumentId = "d1" }],
            Facts = [correction.Facts[0] with { SupersedesFactId = "f1" }]
        };
        Assert.Equal(ResearchImportStatus.Applied, (await writer.ImportResearchAsync(correction)).Status);
        var pinnedReport = await pinned.ReadCompanyAsync("SYNTH", At.AddDays(3), "revenue");
        Assert.Equal(original.Hash, pinnedReport.Hash);
        Assert.Equal("f1", Assert.Single(await pinned.GetFactsAtAsync(At.AddDays(3), "SYNTH")).FactId);
        Assert.Equal("f1", Assert.Single(await pinned.GetMetricsAtAsync(At.AddDays(3), "SYNTH")).SourceFactId);
        Assert.Equal("d1", Assert.Single(await pinned.SearchDocumentsAsync("SYNTH", "revenue", At.AddDays(3))).DocumentId);
        var pinnedHealth = await pinned.ReadHealthAsync();
        Assert.Equal(1, pinnedHealth.Health.AppliedBatches);
        Assert.Single(pinnedHealth.Imports);
        Assert.Equal(2, (await reader.ReadResearchHealthSnapshotAsync()).Health.AppliedBatches);
        var current = await reader.ReadCompanySnapshotAsync("SYNTH", At.AddDays(3), "revenue");
        Assert.NotEqual(original.Hash, current.Hash);
        Assert.Equal("f2", Assert.Single(current.Facts).FactId);
        Assert.Equal("f2", Assert.Single(current.Metrics).SourceFactId);
        Assert.Equal("d2", Assert.Single(current.Documents).DocumentId);
        Assert.Equal(historical.Hash, (await reader.ReadCompanySnapshotAsync("SYNTH", At.AddDays(1), "revenue")).Hash);
        await pinned.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pinned.GetFactsAtAsync(At));
    }

    [Fact]
    public async Task Cited_documents_are_included_without_search_hits_and_hashes_are_reproducible()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        await store.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        var report = await store.ReadCompanySnapshotAsync("SYNTH", At.AddDays(1));
        Assert.Single(report.Documents);
        Assert.Empty(report.MatchingDocumentIds);
        Assert.Equal(64, report.Hash.Length);
        Assert.Equal(report.Hash, (await store.ReadCompanySnapshotAsync("SYNTH", At.AddDays(1).ToOffset(TimeSpan.FromHours(5.5)))).Hash);
        Assert.Empty((await store.ReadCompanySnapshotAsync("OTHER", At.AddDays(1))).Documents);
        Assert.Empty((await store.ReadCompanySnapshotAsync("SYNTH", At.AddSeconds(-1))).Facts);
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadCompanySnapshotAsync("SYNTH", At, new string('x', 501)));
    }

    [Fact]
    public async Task Withdrawn_fact_does_not_return_when_its_correction_document_is_replaced()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        await store.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        var correction = Batch("b2", "d2", "f2", At.AddDays(1));
        correction = correction with
        {
            Facts = [correction.Facts[0] with { SupersedesFactId = "f1", Verification = VerificationState.Unverified }],
            Metrics = []
        };
        await store.ImportResearchAsync(correction);
        var replacement = Batch("b3", "d3", "f3", At.AddDays(2));
        replacement = replacement with
        {
            Documents = [replacement.Documents[0] with { SupersedesDocumentId = "d2" }], Facts = [], Metrics = []
        };
        await store.ImportResearchAsync(replacement);
        Assert.Single((await store.ReadCompanySnapshotAsync("SYNTH", At)).Facts);
        var current = await store.ReadCompanySnapshotAsync("SYNTH", At.AddDays(3));
        Assert.Empty(current.Facts);
        Assert.Empty(current.Metrics);
    }

    [Fact]
    public async Task Company_report_shows_latest_known_metrics_while_retaining_dated_claims()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        await store.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        var update = Batch("b2", "d2", "f2", At.AddDays(2));
        update = update with { Metrics = [update.Metrics[0] with { Value = 12m }] };
        await store.ImportResearchAsync(update);
        Assert.Equal(10m, Assert.Single((await store.ReadCompanySnapshotAsync("SYNTH", At.AddDays(1))).Metrics).Value);
        var latest = await store.ReadCompanySnapshotAsync("SYNTH", At.AddDays(3));
        Assert.Equal(12m, Assert.Single(latest.Metrics).Value);
        Assert.Equal(2, latest.Facts.Count);
        Assert.Equal(2, latest.Documents.Count);
    }

    [Fact]
    public async Task Read_snapshots_do_not_create_missing_databases_and_detect_altered_evidence()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await Assert.ThrowsAsync<SqliteException>(() => store.OpenResearchSnapshotAsync());
        Assert.False(File.Exists(temp.Path));
        await store.InitializeAsync();
        await store.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        await using var connection = new SqliteConnection($"Data Source={temp.Path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE documents SET content='Altered source content' WHERE document_id='d1'";
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadCompanySnapshotAsync("SYNTH", At.AddDays(1)));
    }

    private static ResearchBatch Batch(string id, string docId, string factId, DateTimeOffset at)
    {
        var document = ResearchServices.CreateDocument(docId, "SYNTH", new Uri("https://example.com/" + docId),
            "Synthetic", at, at, at, "SYNTHETIC_ONLY", "manual-v1", "Synthetic revenue report.");
        var fact = new SourceFact(factId, docId, "SYNTH", "Synthetic revenue grew 10 percent.", at, VerificationState.Verified);
        return new ResearchBatch(id, [document], [fact],
            [new CompanyMetric("SYNTH", CompanyMetricKind.RevenueGrowth3YPercent, 10m, at, factId, VerificationState.Verified)]);
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tradetest-snapshot-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            foreach (string path in new[] { Path, Path + "-wal", Path + "-shm" }) File.Delete(path);
        }
    }
}
