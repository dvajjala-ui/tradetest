using TradeTest.Domain;

namespace TradeTest.Application;

public static class DataQuality
{
    public static IReadOnlyList<DataIssue> Check(MarketBar bar)
    {
        List<DataIssue>? issues = null;
        void Add(DataIssueCode code, string detail) => (issues ??= []).Add(new(code, detail));
        if (string.IsNullOrWhiteSpace(bar.SecurityId)) Add(DataIssueCode.MissingIdentifier, "Security ID is required.");
        if (bar.Interval <= TimeSpan.Zero) Add(DataIssueCode.InvalidInterval, "Interval must be positive.");
        if (bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0)
            Add(DataIssueCode.InvalidPrice, "All OHLC prices must be positive.");
        if (bar.High < Math.Max(bar.Open, bar.Close) || bar.Low > Math.Min(bar.Open, bar.Close) || bar.Low > bar.High)
            Add(DataIssueCode.InvalidOhlc, "OHLC extrema are inconsistent.");
        if (bar.Volume < 0) Add(DataIssueCode.InvalidVolume, "Volume cannot be negative.");
        if (!bar.IsComplete) Add(DataIssueCode.IncompleteBar, "Only completed bars are actionable.");
        return issues is null ? Array.Empty<DataIssue>() : issues;
    }

    public static IReadOnlyList<DataIssue> CheckSeries(IReadOnlyList<MarketBar> bars, DateTimeOffset asOf)
    {
        var issues = new List<DataIssue>();
        var seen = new HashSet<(string, DateTimeOffset, TimeSpan)>();
        MarketBar? previous = null;
        foreach (var bar in bars)
        {
            issues.AddRange(Check(bar));
            if (!seen.Add((bar.SecurityId, bar.StartsAt, bar.Interval)))
                issues.Add(new(DataIssueCode.DuplicateBar, $"Duplicate bar at {bar.StartsAt:O}."));
            if (previous is not null && (bar.SecurityId != previous.SecurityId || bar.StartsAt < previous.EndsAt))
                issues.Add(new(DataIssueCode.OverlappingBar, $"Unsorted or overlapping bar at {bar.StartsAt:O}."));
            else if (previous is not null && bar.StartsAt > previous.EndsAt)
                issues.Add(new(DataIssueCode.MissingBar, $"Gap before bar at {bar.StartsAt:O}."));
            if (bar.EndsAt > asOf)
                issues.Add(new(DataIssueCode.FutureBar, $"Bar ending at {bar.EndsAt:O} is after as-of time."));
            previous = bar;
        }
        return issues;
    }
}
