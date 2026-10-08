using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;
using TradeTest.Infrastructure;

namespace TradeTest.Tests;

public sealed class ResearchAndStorageTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Packet_uses_only_verified_facts_known_at_the_decision_time()
    {
        var oldFact = new SourceFact("f1", "d1", "SYNTH", "Revenue grew 10 percent.", Day1, VerificationState.Verified);
        var correction = new SourceFact("f2", "d2", "SYNTH", "Revenue grew 8 percent.",
            Day1.AddDays(2), VerificationState.Verified, "f1");
        var rumor = new SourceFact("f3", "d3", "SYNTH", "Revenue grew 90 percent.", Day1, VerificationState.Unverified);
        var before = ResearchServices.BuildPacket([oldFact, correction, rumor], Day1.AddDays(1));
        var after = ResearchServices.BuildPacket([oldFact, correction, rumor], Day1.AddDays(3));
        Assert.Single(before.Facts);
        Assert.Equal("f1", before.Facts[0].FactId);
        Assert.Single(after.Facts);
        Assert.Equal("f2", after.Facts[0].FactId);
        Assert.NotEqual(before.Hash, after.Hash);
    }

    [Fact]
    public void Long_term_rank_excludes_future_or_unverified_metrics()
    {
        var metrics = Enum.GetValues<CompanyMetricKind>().Select(kind =>
            new CompanyMetric("SYNTH", kind, kind switch
            {
                CompanyMetricKind.AverageDailyTurnoverRupees => 12_000_000m,
                CompanyMetricKind.NetDebtToEbitda => 0.5m,
                _ => 10m
            },
                kind == CompanyMetricKind.Momentum12MPercent ? Day1.AddDays(2) : Day1,
                kind.ToString(), VerificationState.Verified)).ToArray();
        var ranker = new LongTermRanker();
        Assert.Empty(ranker.Rank(metrics, Day1.AddDays(1)));
        Assert.Single(ranker.Rank(metrics, Day1.AddDays(3)));
        Assert.Empty(ranker.Rank(metrics.Select(m => m.Kind == CompanyMetricKind.Momentum12MPercent
            ? m with { Verification = VerificationState.Unverified } : m), Day1.AddDays(3)));
    }

    [Fact]
    public async Task Journal_reopens_and_detects_tampering_or_duplicate_session()
    {
        using var temp = new TemporaryDatabase();
        var first = new SqliteStore(temp.Path);
        await first.InitializeAsync();
        var entries = new[] { new SimulationEvent("CANDIDATE", Day1, "{\"id\":1}"),
            new SimulationEvent("RISK_DECISION", Day1.AddSeconds(1), "{\"approved\":false}") };
        await first.AppendEventsAsync("session-1", 0, entries);
        var second = new SqliteStore(temp.Path);
        Assert.Equal(2, (await second.ReadEventsAsync("session-1")).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.AppendEventsAsync("session-1", 0, entries));

        await using var connection = new SqliteConnection($"Data Source={temp.Path}");
        await connection.OpenAsync();
        await using var tamper = connection.CreateCommand();
        tamper.CommandText = "UPDATE events SET json='changed' WHERE stream='session-1' AND version=1";
        await tamper.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => second.ReadEventsAsync("session-1"));
    }

    [Fact]
    public async Task Dated_search_and_metrics_preserve_source_provenance()
    {
        using var temp = new TemporaryDatabase();
        var store = new SqliteStore(temp.Path);
        await store.InitializeAsync();
        var document = ResearchServices.CreateDocument("d1", "SYNTH", new Uri("https://example.com/d1"),
            "Synthetic publisher", Day1, Day1.AddDays(1), Day1.AddDays(2), "SYNTHETIC_TEST_ONLY",
            "manual-v1", "Synthetic report: revenue grew by ten percent.");
        await store.AddDocumentAsync(document);
        var fact = new SourceFact("f1", "d1", "SYNTH", "Revenue grew by ten percent.",
            Day1.AddDays(1), VerificationState.Verified);
        await store.AddFactAsync(fact);
        Assert.Empty(await store.SearchDocumentsAsync("SYNTH", "revenue", Day1));
        Assert.Single(await store.SearchDocumentsAsync("SYNTH", "revenue", Day1.AddDays(1)));
        Assert.Empty(await store.GetFactsAtAsync(Day1));
        Assert.Single(await store.GetFactsAtAsync(Day1.AddDays(1)));
        var metric = new CompanyMetric("SYNTH", CompanyMetricKind.RevenueGrowth3YPercent,
            10m, Day1.AddDays(1), "f1", VerificationState.Verified);
        await store.AddMetricAsync(metric);
        Assert.Empty(await store.GetMetricsAtAsync(Day1));
        Assert.Single(await store.GetMetricsAtAsync(Day1.AddDays(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => store.AddMetricAsync(metric with { SourceFactId = "nonexistent" }));
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tradetest-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            foreach (var name in new[] { Path, Path + "-wal", Path + "-shm" })
                if (File.Exists(name)) File.Delete(name);
        }
    }
}
