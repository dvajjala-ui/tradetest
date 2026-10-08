using System.Text.Json;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Tests;

public sealed class MetricHistoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Indexed_rank_matches_scan_rank_for_future_stale_partial_missing_and_tied_rows()
    {
        var rows = Enumerable.Range(0, 25).SelectMany(company => Enumerable.Range(0, 6).SelectMany(revision =>
            Metrics("COMPANY-" + company, Start.AddDays(revision * 30), "fact-" + revision))).ToList();
        rows.AddRange(Metrics("PARTIAL", Start, "partial").Select(m => m with { Verification = VerificationState.Partial }));
        rows.AddRange(Metrics("INCOMPLETE", Start, "incomplete")[..3]);
        rows.AddRange(Metrics("COMPANY-0", Start, "aaaa").Select(m => m with { Value = m.Kind == CompanyMetricKind.AverageDailyTurnoverRupees ? 15_000_000m : 3m }));
        var index = new MetricHistoryIndex(rows);
        var ranker = new LongTermRanker();
        foreach (int day in new[] { -1, 0, 1, 29, 30, 60, 149, 150, 400, 800 })
        {
            var asOf = Start.AddDays(day);
            Assert.Equal(JsonSerializer.Serialize(ranker.Rank(rows, asOf)), JsonSerializer.Serialize(index.RankAt(asOf)));
        }
        Assert.False(index.ProvenanceValidated);
        rows.Clear();
        Assert.Equal(25, index.RankAt(Start.AddDays(1)).Count); // Index does not keep the caller's mutable list.
    }

    [Fact]
    public void Visible_unverified_fact_correction_withdraws_its_metric_without_rewriting_earlier_rankings()
    {
        var document = Document("doc", Start.AddDays(-1));
        var metrics = Metrics("SYNTH", Start, "original");
        var facts = metrics.Select(m => new SourceFact(m.SourceFactId, document.DocumentId, "SYNTH", "Synthetic metric", Start, VerificationState.Verified)).ToArray();
        var corrected = facts[0] with { FactId = "corrected", FirstKnownAt = Start.AddDays(2), Verification = VerificationState.Unverified, SupersedesFactId = facts[0].FactId };
        var index = new MetricHistoryIndex(metrics, [.. facts, corrected], [document]);
        Assert.True(index.ProvenanceValidated);
        Assert.Single(index.RankAt(Start.AddDays(1)));
        Assert.Empty(index.RankAt(Start.AddDays(3)));
        Assert.Single(index.RankAt(Start.AddDays(1)));
    }

    [Fact]
    public void Document_correction_withdraws_all_old_metrics_and_source_violations_fail_compilation()
    {
        var document = Document("doc", Start.AddDays(-1));
        var correction = Document("doc-v2", Start.AddDays(2)) with { SupersedesDocumentId = document.DocumentId };
        var metrics = Metrics("SYNTH", Start, "source");
        var facts = metrics.Select(m => new SourceFact(m.SourceFactId, document.DocumentId, "SYNTH", "Synthetic metric", Start, VerificationState.Verified)).ToArray();
        var index = new MetricHistoryIndex(metrics, facts, [document, correction]);
        Assert.Single(index.RankAt(Start));
        Assert.Empty(index.RankAt(Start.AddDays(2)));
        Assert.Throws<ArgumentException>(() => new MetricHistoryIndex(metrics, facts, [document with { ContentSha256 = new string('0', 64) }]));
        Assert.Throws<ArgumentException>(() => new MetricHistoryIndex(metrics, facts.Select(f => f with { Verification = VerificationState.Unverified }).ToArray(), [document]));
        Assert.Throws<ArgumentException>(() => new MetricHistoryIndex(metrics, facts, null));
        Assert.Throws<ArgumentException>(() => new MetricHistoryIndex([.. metrics, metrics[0]]));
    }

    private static SourceDocument Document(string id, DateTimeOffset known) => ResearchServices.CreateDocument(id, "SYNTH",
        new Uri("https://example.com/synthetic-source"), "Synthetic publisher", known, known, known, "SYNTHETIC_ONLY", "test-v1", "Synthetic content");
    private static CompanyMetric[] Metrics(string security, DateTimeOffset known, string prefix) => Enum.GetValues<CompanyMetricKind>()
        .Select(kind => new CompanyMetric(security, kind, kind switch { CompanyMetricKind.AverageDailyTurnoverRupees => 12_000_000m, CompanyMetricKind.NetDebtToEbitda => 0.4m, _ => 10m },
            known, security + prefix + kind, VerificationState.Verified)).ToArray();
}
