using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>A fixed, explainable hypothesis for offline evaluation, not a validated edge.</summary>
public sealed class OpeningRangeStrategy
{
    public const string Version = "opening-range-v1";
    public OpeningRangeSession CreateSession() => new();

    public TradeCandidate? Scan(IReadOnlyList<MarketBar> bars, DateTimeOffset asOf)
    {
        var session = CreateSession();
        foreach (var bar in bars) session.Append(bar, asOf);
        return session.CurrentCandidate();
    }
}

/// <summary>One security/session. Each completed bar updates indicators once; invalid input latches closed.</summary>
public sealed class OpeningRangeSession
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private MarketBar? _latest;
    private DateTime _date;
    private TimeSpan _localStart;
    private bool _valid = true;
    private int _count;
    private decimal _openingHigh, _openingVolume, _totalVolume, _weightedPrice, _previousClose;

    public void Append(MarketBar bar, DateTimeOffset asOf)
    {
        if (!_valid) return;
        var local = TimeZoneInfo.ConvertTime(bar.StartsAt, India);
        if (DataQuality.Check(bar).Count != 0 || bar.EndsAt > asOf || bar.Interval != TimeSpan.FromMinutes(5) ||
            (_latest is null && local.TimeOfDay != new TimeSpan(9, 15, 0)) ||
            (_latest is not null && (bar.SecurityId != _latest.SecurityId ||
                bar.StartsAt != _latest.EndsAt || local.Date != _date)))
        {
            _valid = false;
            return;
        }
        _date = local.Date;
        _localStart = local.TimeOfDay;
        _previousClose = _latest?.Close ?? 0;
        _latest = bar;
        _count++;
        if (_count <= 3)
        {
            _openingHigh = Math.Max(_openingHigh, bar.High);
            _openingVolume += bar.Volume;
        }
        _totalVolume += bar.Volume;
        _weightedPrice += ((bar.High + bar.Low + bar.Close) / 3m) * bar.Volume;
    }

    public TradeCandidate? CurrentCandidate()
    {
        if (!_valid || _count < 4 || _latest is null || _openingVolume <= 0 || _totalVolume <= 0 ||
            _localStart < new TimeSpan(9, 30, 0) || _localStart > new TimeSpan(11, 0, 0)) return null;
        var latest = _latest;
        decimal volumeRatio = latest.Volume / (_openingVolume / 3m);
        if (volumeRatio < 1.2m || latest.Close <= _openingHigh || _previousClose > _openingHigh) return null;
        decimal vwap = _weightedPrice / _totalVolume;
        if (latest.Close <= vwap) return null;
        decimal risk = latest.Close - _openingHigh;
        string id = $"{latest.SecurityId}-{latest.EndsAt.ToUniversalTime():yyyyMMddHHmm}";
        return new TradeCandidate(id, latest.SecurityId, latest.EndsAt, latest.EndsAt.AddMinutes(5),
            latest.Close, _openingHigh, latest.Close + 2m * risk, volumeRatio, vwap, OpeningRangeStrategy.Version);
    }
}
