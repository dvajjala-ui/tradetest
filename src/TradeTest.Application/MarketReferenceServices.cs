using TradeTest.Domain;

namespace TradeTest.Application;

public sealed class SecurityMaster
{
    private readonly Dictionary<string, SecurityVersion[]> _versions;

    public SecurityMaster(IEnumerable<SecurityVersion> versions)
    {
        var rows = versions.ToArray();
        if (rows.Any(v => v is null || string.IsNullOrWhiteSpace(v.VersionId) || string.IsNullOrWhiteSpace(v.SecurityId) ||
            string.IsNullOrWhiteSpace(v.Isin) || string.IsNullOrWhiteSpace(v.Symbol) ||
            string.IsNullOrWhiteSpace(v.Exchange) || string.IsNullOrWhiteSpace(v.Segment) ||
            !ValidSource(v.SourceUrl, v.LicenceId) || !Enum.IsDefined(v.Verification)) ||
            rows.Select(v => v.VersionId).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.GroupBy(v => (v.SecurityId.ToUpperInvariant(), v.EffectiveAt, v.FirstKnownAt)).Any(g => g.Count() > 1))
            throw new ArgumentException("Invalid or ambiguous security history.", nameof(versions));
        _versions = rows.GroupBy(v => v.SecurityId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key,
            g => g.OrderByDescending(v => v.EffectiveAt).ThenByDescending(v => v.FirstKnownAt).ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public SecurityVersion? GetAt(string securityId, DateTimeOffset asOf)
    {
        if (!_versions.TryGetValue(securityId, out var versions)) return null;
        var version = versions.FirstOrDefault(v => v.EffectiveAt <= asOf && v.FirstKnownAt <= asOf);
        return version is { IsListed: true, Verification: VerificationState.Verified } ? version : null;
    }

    public IReadOnlyList<SecurityVersion> UniverseAt(DateTimeOffset asOf) => _versions.Keys
        .Select(id => GetAt(id, asOf)).Where(v => v is not null).Select(v => v!)
        .OrderBy(v => v.SecurityId, StringComparer.Ordinal).ToArray();

    public SecurityVersion? ResolveSymbol(string exchange, string symbol, DateTimeOffset asOf)
    {
        var matches = UniverseAt(asOf).Where(v => v.Exchange.Equals(exchange, StringComparison.OrdinalIgnoreCase) &&
            v.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length > 1) throw new InvalidDataException("Ambiguous historical exchange/symbol mapping.");
        return matches.SingleOrDefault();
    }

    internal static bool ValidSource(Uri? url, string licence) => url is { IsAbsoluteUri: true } &&
        url.Scheme is "https" or "http" && !string.IsNullOrWhiteSpace(licence);
}

public sealed class TradingCalendar
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private readonly Dictionary<(string, DateOnly), ExchangeSession[]> _sessions;

    public TradingCalendar(IEnumerable<ExchangeSession> sessions)
    {
        var rows = sessions.ToArray();
        if (rows.Any(s => s is null || string.IsNullOrWhiteSpace(s.Exchange) || s.ClosesAt <= s.OpensAt ||
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.OpensAt, India).Date) != s.SessionDate ||
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.ClosesAt, India).Date) != s.SessionDate ||
            !SecurityMaster.ValidSource(s.SourceUrl, s.LicenceId) || !Enum.IsDefined(s.Verification)) ||
            rows.GroupBy(s => (s.Exchange.ToUpperInvariant(), s.SessionDate, s.FirstKnownAt)).Any(g => g.Count() > 1))
            throw new ArgumentException("Invalid or ambiguous calendar history.", nameof(sessions));
        _sessions = rows.GroupBy(s => (s.Exchange.ToUpperInvariant(), s.SessionDate))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.FirstKnownAt).ToArray());
    }

    public ExchangeSession? GetAt(string exchange, DateOnly day, DateTimeOffset asOf)
    {
        if (!_sessions.TryGetValue((exchange.ToUpperInvariant(), day), out var versions)) return null;
        var session = versions.FirstOrDefault(s => s.FirstKnownAt <= asOf);
        return session?.Verification == VerificationState.Verified ? session : null;
    }
}

public sealed class MarketReferenceValidator(MarketReferenceData data)
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    private readonly SecurityMaster _master = new(data.Securities);
    private readonly TradingCalendar _calendar = new(data.Sessions);

    public void ValidateReplay(IReadOnlyList<MarketBar> bars, DateTimeOffset sessionEndsAt)
    {
        if (bars.Count == 0) throw new InvalidDataException("Bars are required.");
        var first = bars[0];
        var security = _master.GetAt(first.SecurityId, first.StartsAt)
            ?? throw new InvalidDataException("Security is not verified and listed as of the session open.");
        if (!security.Segment.Equals("CASH_EQUITY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only cash equity sessions are supported.");
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(first.StartsAt, India).Date);
        var session = _calendar.GetAt(security.Exchange, date, first.StartsAt)
            ?? throw new InvalidDataException("No verified exchange session was known at the session open.");
        if (first.StartsAt != session.OpensAt || sessionEndsAt > session.ClosesAt || sessionEndsAt <= session.OpensAt)
            throw new InvalidDataException("Replay window contradicts the exchange session.");
        foreach (var bar in bars)
        {
            var version = _master.GetAt(bar.SecurityId, bar.StartsAt);
            if (version is null || !version.Exchange.Equals(security.Exchange, StringComparison.OrdinalIgnoreCase) ||
                !version.Segment.Equals("CASH_EQUITY", StringComparison.OrdinalIgnoreCase) ||
                bar.StartsAt < session.OpensAt || bar.EndsAt > session.ClosesAt)
                throw new InvalidDataException("Bar falls outside a verified listing or exchange session.");
        }
    }
}
