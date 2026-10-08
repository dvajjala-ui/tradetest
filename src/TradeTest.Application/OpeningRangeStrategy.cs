using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>A fixed, explainable hypothesis for offline evaluation, not a validated edge.</summary>
public sealed class OpeningRangeStrategy
{
    public const string Version = "opening-range-v1";
    private readonly TimeZoneInfo _india = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public TradeCandidate? Scan(IReadOnlyList<MarketBar> bars, DateTimeOffset asOf)
    {
        if (bars.Count < 4 || DataQuality.CheckSeries(bars, asOf).Count != 0) return null;
        var latest = bars[^1];
        var local = TimeZoneInfo.ConvertTime(latest.StartsAt, _india);
        if (local.TimeOfDay < new TimeSpan(9, 30, 0) || local.TimeOfDay > new TimeSpan(11, 0, 0)) return null;
        if (bars.Any(b => TimeZoneInfo.ConvertTime(b.StartsAt, _india).Date != local.Date || b.Interval != TimeSpan.FromMinutes(5))) return null;

        var opening = bars.Take(3).ToArray();
        var expectedStart = new TimeSpan(9, 15, 0);
        for (int i = 0; i < 3; i++)
            if (TimeZoneInfo.ConvertTime(opening[i].StartsAt, _india).TimeOfDay != expectedStart + TimeSpan.FromMinutes(5 * i)) return null;

        decimal rangeHigh = opening.Max(b => b.High);
        decimal averageOpeningVolume = opening.Average(b => b.Volume);
        if (averageOpeningVolume <= 0) return null;
        decimal volumeRatio = latest.Volume / averageOpeningVolume;
        if (volumeRatio < 1.2m || latest.Close <= rangeHigh || bars[^2].Close > rangeHigh) return null;

        decimal totalVolume = bars.Sum(b => b.Volume);
        if (totalVolume <= 0) return null;
        decimal vwap = bars.Sum(b => ((b.High + b.Low + b.Close) / 3m) * b.Volume) / totalVolume;
        if (latest.Close <= vwap) return null;

        decimal stop = rangeHigh;
        decimal risk = latest.Close - stop;
        if (risk <= 0) return null;
        string id = $"{latest.SecurityId}-{latest.EndsAt.ToUniversalTime():yyyyMMddHHmm}";
        return new TradeCandidate(id, latest.SecurityId, latest.EndsAt, latest.EndsAt.AddMinutes(5),
            latest.Close, stop, latest.Close + 2m * risk, volumeRatio, vwap, Version);
    }
}
