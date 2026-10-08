using TradeTest.Application;
using TradeTest.Domain;
using TradeTest.Infrastructure;

namespace TradeTest.Tests;

public sealed class ResearchImportTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public async Task Batch_retry_is_idempotent_and_payload_changes_are_rejected()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        var batch = Batch("b1", "d1", "f1", At);
        Assert.Equal(ResearchImportStatus.Applied, (await store.ImportResearchAsync(batch)).Status);
        Assert.Equal(ResearchImportStatus.AlreadyApplied, (await store.ImportResearchAsync(batch)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ImportResearchAsync(batch with { Metrics = [] }));
        var health = await store.GetResearchHealthAsync();
        Assert.Equal((1, 1, 1, 1), (health.Documents, health.Facts, health.Metrics, health.AppliedBatches));
    }

    [Fact]
    public async Task Late_constraint_failure_rolls_back_all_rows_and_fts_then_records_quarantine()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        await store.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        var failed = await store.ImportResearchAsync(Batch("b2", "d2", "f1", At.AddDays(1)));
        Assert.Equal(ResearchImportStatus.Quarantined, failed.Status);
        Assert.NotNull(failed.Error);
        Assert.Single(await store.SearchDocumentsAsync("SYNTH", "revenue", At.AddDays(2)));
        var health = await store.GetResearchHealthAsync();
        Assert.Equal((1, 1, 1, 1), (health.Documents, health.Facts, health.Metrics, health.QuarantinedBatches));
        Assert.Equal(ResearchImportStatus.Quarantined, (await store.ImportResearchAsync(Batch("b2", "d2", "f1", At.AddDays(1)))).Status);
    }

    [Fact]
    public async Task Document_correction_invalidates_old_metrics_only_when_first_known()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        await store.ImportResearchAsync(Batch("b1", "d1", "f1", At));
        var corrected = Batch("b2", "d2", "f2", At.AddDays(2));
        corrected = corrected with
        {
            Documents = [corrected.Documents[0] with { SupersedesDocumentId = "d1" }],
            Facts = [corrected.Facts[0] with { SupersedesFactId = "f1", Verification = VerificationState.Unverified }],
            Metrics = []
        };
        Assert.Equal(ResearchImportStatus.Applied, (await store.ImportResearchAsync(corrected)).Status);
        Assert.Single(await store.GetMetricsAtAsync(At.AddDays(1), "SYNTH"));
        Assert.Empty(await store.GetMetricsAtAsync(At.AddDays(3), "SYNTH"));
        Assert.Equal("d1", (await store.SearchDocumentsAsync("SYNTH", "revenue", At.AddDays(1)))[0].DocumentId);
        Assert.Equal("d2", (await store.SearchDocumentsAsync("SYNTH", "revenue", At.AddDays(3)))[0].DocumentId);
        Assert.Empty(ResearchServices.BuildPacket(await store.GetFactsAtAsync(At.AddDays(3)), At.AddDays(3)).Facts);
    }

    [Fact]
    public async Task Metric_cannot_promote_an_unverified_source_and_concurrent_retry_writes_once()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        var invalid = Batch("bad", "d1", "f1", At);
        invalid = invalid with { Facts = [invalid.Facts[0] with { Verification = VerificationState.Unverified }] };
        Assert.Equal(ResearchImportStatus.Quarantined, (await store.ImportResearchAsync(invalid)).Status);
        Assert.Equal(0, (await store.GetResearchHealthAsync()).Documents);
        var good = Batch("good", "d2", "f2", At);
        var results = await Task.WhenAll(Task.Run(() => store.ImportResearchAsync(good)), Task.Run(() => store.ImportResearchAsync(good)));
        Assert.Single(results, r => r.Status == ResearchImportStatus.Applied);
        Assert.Single(results, r => r.Status == ResearchImportStatus.AlreadyApplied);
        Assert.Equal(1, (await store.GetResearchHealthAsync()).Documents);
    }

    private static ResearchBatch Batch(string id, string docId, string factId, DateTimeOffset at)
    {
        var doc = ResearchServices.CreateDocument(docId, "SYNTH", new Uri("https://example.com/" + docId),
            "Synthetic", at, at, at, "SYNTHETIC_ONLY", "manual-v1", "Synthetic revenue report.");
        var fact = new SourceFact(factId, docId, "SYNTH", "Synthetic revenue grew 10 percent.", at, VerificationState.Verified);
        return new ResearchBatch(id, [doc], [fact],
            [new CompanyMetric("SYNTH", CompanyMetricKind.RevenueGrowth3YPercent, 10m, at, factId, VerificationState.Verified)]);
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tradetest-import-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            foreach (string path in new[] { Path, Path + "-wal", Path + "-shm" }) File.Delete(path);
        }
    }
}
