using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Research-only, deterministic screening baseline. Scores are not expected returns.</summary>
public sealed class LongTermRanker
{
    public const string Version = "quality-growth-momentum-v1";
    private static readonly CompanyMetricKind[] Required =
    [
        CompanyMetricKind.RevenueGrowth3YPercent,
        CompanyMetricKind.ReturnOnCapitalPercent,
        CompanyMetricKind.FreeCashFlowMarginPercent,
        CompanyMetricKind.NetDebtToEbitda,
        CompanyMetricKind.ShareDilution3YPercent,
        CompanyMetricKind.Momentum12MPercent,
        CompanyMetricKind.AverageDailyTurnoverRupees
    ];

    public IReadOnlyList<CompanyScore> Rank(IEnumerable<CompanyMetric> metrics, DateTimeOffset asOf)
    {
        var visible = metrics.Where(m => m.FirstKnownAt <= asOf && m.Verification == VerificationState.Verified)
            .GroupBy(m => m.SecurityId, StringComparer.OrdinalIgnoreCase);
        var scores = new List<CompanyScore>();
        foreach (var company in visible)
        {
            var latest = company.GroupBy(m => m.Kind)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.FirstKnownAt).ThenBy(m => m.SourceFactId, StringComparer.Ordinal).First());
            var score = ScoreCompany(company.Key, latest, asOf);
            if (score is not null) scores.Add(score);
        }
        return Sort(scores);
    }

    internal static CompanyScore? ScoreCompany(string securityId, IReadOnlyDictionary<CompanyMetricKind, CompanyMetric> latest, DateTimeOffset asOf)
    {
        if (Required.Any(k => !latest.ContainsKey(k))) return null;
        if (latest.Values.Any(m => asOf - m.FirstKnownAt > TimeSpan.FromDays(550))) return null;
        decimal revenue = latest[CompanyMetricKind.RevenueGrowth3YPercent].Value;
        decimal roce = latest[CompanyMetricKind.ReturnOnCapitalPercent].Value;
        decimal fcf = latest[CompanyMetricKind.FreeCashFlowMarginPercent].Value;
        decimal debt = latest[CompanyMetricKind.NetDebtToEbitda].Value;
        decimal dilution = latest[CompanyMetricKind.ShareDilution3YPercent].Value;
        decimal momentum = latest[CompanyMetricKind.Momentum12MPercent].Value;
        decimal turnover = latest[CompanyMetricKind.AverageDailyTurnoverRupees].Value;
        if (turnover < 10_000_000m || dilution > 20m || debt > 4m) return null;

        // Clipped inputs avoid allowing one extreme data point to dominate the screen.
        decimal score = Clip(revenue, -20m, 40m) * 0.25m
            + Clip(roce, -10m, 40m) * 0.25m
            + Clip(fcf, -20m, 30m) * 0.20m
            + Clip(momentum, -50m, 100m) * 0.15m
            - Clip(debt, -2m, 4m) * 2m
            - Clip(dilution, -20m, 20m) * 0.5m;
        var flags = new List<string>();
        if (fcf <= 0) flags.Add("NON_POSITIVE_FREE_CASH_FLOW");
        if (momentum < 0) flags.Add("NEGATIVE_12M_MOMENTUM");
        return new CompanyScore(securityId, asOf, score,
            Required.Select(k => latest[k].SourceFactId).ToArray(), flags, Version);
    }

    internal static CompanyScore[] Sort(IEnumerable<CompanyScore> scores) =>
        scores.OrderByDescending(s => s.Score).ThenBy(s => s.SecurityId, StringComparer.Ordinal).ToArray();

    private static decimal Clip(decimal value, decimal min, decimal max) => Math.Min(Math.Max(value, min), max);
}
