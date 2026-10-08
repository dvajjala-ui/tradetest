using System.Security.Cryptography;
using System.Text.Json;
using TradeTest.Domain;

namespace TradeTest.Application;

public sealed record TotalReturnBuildInput(string SecurityId, IReadOnlyList<RawCloseObservation> Closes,
    IReadOnlyList<CorporateAction> Actions, CorporateActionCoverage Coverage, DateTimeOffset AsOf);
public sealed record TotalReturnBuildReport(string BuilderVersion, DateTimeOffset AsOf, string InputSha256,
    IReadOnlyList<TotalReturnPrice> Prices, string EvidenceNote);

/// <summary>Forward return chain with explicit action coverage. It never applies a future-known correction to an earlier snapshot.</summary>
public sealed class TotalReturnBuilder
{
    public const string Version = "forward-total-return-v1";
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public TotalReturnBuildReport Build(TotalReturnBuildInput input)
    {
        if (string.IsNullOrWhiteSpace(input.SecurityId) || input.Closes.Count == 0 ||
            input.Closes.Any(c => c is null) || input.Actions.Any(a => a is null))
            throw new ArgumentException("A security and raw close observations are required.");
        var coverage = input.Coverage;
        ValidateSource(coverage.SourceUrl, coverage.LicenceId, coverage.Verification);
        if (coverage.SecurityId != input.SecurityId || coverage.FromExclusive >= coverage.ThroughInclusive ||
            coverage.FirstKnownAt < coverage.ThroughInclusive || coverage.FirstKnownAt > input.AsOf)
            throw new InvalidDataException("Corporate-action coverage must be known and match the requested security.");
        var closes = input.Closes.Where(p => p.FirstKnownAt <= input.AsOf).OrderBy(p => p.CloseAt).ToArray();
        if (closes.Length == 0) throw new InvalidDataException("No raw closes were known at the requested snapshot time.");
        if (closes[0].CloseAt <= coverage.FromExclusive || closes[^1].CloseAt > coverage.ThroughInclusive)
            throw new InvalidDataException("Corporate-action coverage does not cover the raw close history.");
        if (closes.Select(p => p.CloseAt).Distinct().Count() != closes.Length ||
            closes.Select(p => p.ObservationId).Distinct(StringComparer.Ordinal).Count() != closes.Length)
            throw new InvalidDataException("Raw close observations must have unique IDs and timestamps.");
        foreach (var close in closes)
        {
            ValidateSource(close.SourceUrl, close.LicenceId, close.Verification);
            if (close.SecurityId != input.SecurityId || string.IsNullOrWhiteSpace(close.ObservationId) ||
                close.CloseRupees <= 0 || close.FirstKnownAt < close.CloseAt)
                throw new InvalidDataException("Invalid raw close security, timestamp or value.");
        }
        var actions = ResolveActions(input.Actions, input.SecurityId, input.AsOf);
        if (actions.Any(a => a.EffectiveAt > coverage.ThroughInclusive && a.EffectiveAt <= input.AsOf))
            throw new InvalidDataException("Supplied action lies beyond declared action coverage.");
        var applicable = actions.Where(a => a.EffectiveAt > closes[0].CloseAt && a.EffectiveAt <= input.AsOf)
            .OrderBy(a => a.EffectiveAt).ThenBy(a => a.Sequence).ToArray();
        if (applicable.GroupBy(a => (a.EffectiveAt, a.Sequence)).Any(group => group.Count() > 1))
            throw new InvalidDataException("Actions at the same timestamp need distinct explicit sequence numbers.");
        foreach (var action in applicable) ValidateTerms(action);

        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Builder = Version, input.SecurityId, input.AsOf, Coverage = coverage,
            Closes = closes, Actions = applicable
        }))).ToLowerInvariant();
        string adjustmentVersion = Version + ":" + hash;
        var result = new List<TotalReturnPrice>(closes.Length + 1);
        DateTimeOffset known = Max(closes[0].FirstKnownAt, coverage.FirstKnownAt);
        decimal value = 100m, previousClose = closes[0].CloseRupees;
        result.Add(new(input.SecurityId, closes[0].CloseAt, known, value, adjustmentVersion,
            SourceEvidenceIds: [closes[0].ObservationId]));
        int actionIndex = 0;
        var terminal = applicable.FirstOrDefault(a => a.Kind is CorporateActionKind.TerminalCashSettlement or CorporateActionKind.TerminalWriteOff);
        if (terminal is not null && closes.Any(p => p.CloseAt >= terminal.EffectiveAt))
            throw new InvalidDataException("Tradable raw closes cannot exist at or after a terminal action.");
        for (int index = 1; index < closes.Length; index++)
        {
            var close = closes[index];
            decimal shares = 1m, cash = 0m;
            var evidence = new List<string> { close.ObservationId };
            // Reinvestment needs each dividend's ex-date close. Sparse endpoints cannot invent that reinvestment price.
            while (actionIndex < applicable.Length && applicable[actionIndex].EffectiveAt <= close.CloseAt)
            {
                var action = applicable[actionIndex++];
                if (action.Kind == CorporateActionKind.CashDividend &&
                    TimeZoneInfo.ConvertTime(action.EffectiveAt, India).Date != TimeZoneInfo.ConvertTime(close.CloseAt, India).Date)
                    throw new InvalidDataException("Dividend action requires an observation at its effective ex-date close.");
                if (action.Kind is CorporateActionKind.TerminalCashSettlement or CorporateActionKind.TerminalWriteOff)
                    throw new InvalidDataException("Terminal action must follow the last tradable close.");
                cash += shares * action.CashRupeesPerShareBefore;
                shares *= action.SharesAfterPerShareBefore;
                known = Max(known, action.FirstKnownAt);
                evidence.Add(action.ActionId);
            }
            value *= (shares * close.CloseRupees + cash) / previousClose;
            known = Max(known, close.FirstKnownAt);
            result.Add(new(input.SecurityId, close.CloseAt, known, value, adjustmentVersion, SourceEvidenceIds: evidence));
            previousClose = close.CloseRupees;
        }
        if (terminal is not null)
        {
            decimal shares = 1m, cash = 0m;
            var evidence = new List<string>();
            while (actionIndex < applicable.Length)
            {
                var action = applicable[actionIndex++];
                if (action.EffectiveAt > terminal.EffectiveAt || action.Kind == CorporateActionKind.CashDividend)
                    throw new InvalidDataException("Unobserved dividends or actions after termination need a separate settlement model.");
                cash += shares * action.CashRupeesPerShareBefore;
                shares *= action.SharesAfterPerShareBefore;
                known = Max(known, action.FirstKnownAt);
                evidence.Add(action.ActionId);
                if (action.ActionId == terminal.ActionId) break;
            }
            if (actionIndex != applicable.Length) throw new InvalidDataException("Actions cannot follow a terminal settlement.");
            known = Max(known, terminal.EffectiveAt);
            value *= cash / previousClose;
            result.Add(new(input.SecurityId, terminal.EffectiveAt, known, value, adjustmentVersion,
                IsTerminal: true, TerminalReason: terminal.Kind.ToString(), SourceEvidenceIds: evidence));
        }
        else if (actionIndex < applicable.Length)
            throw new InvalidDataException("Effective actions after the last close require further prices or an explicit terminal settlement.");
        return new(Version, input.AsOf, hash, result,
            "Forward chain starts at 100; regular cash dividends are reinvested at the supplied ex-date close. Split/bonus terms apply in explicit sequence order. Terminal cash is carried without subsequent reinvestment. Gross pre-tax research returns; no rights, merger, demerger, withholding or settlement-delay model. Source coverage is an input assertion and requires vendor reconciliation.");
    }

    private static CorporateAction[] ResolveActions(IReadOnlyList<CorporateAction> rows, string securityId, DateTimeOffset asOf)
    {
        var visible = rows.Where(a => a.FirstKnownAt <= asOf).ToArray();
        var byId = visible.ToDictionary(a => a.ActionId, StringComparer.Ordinal);
        foreach (var action in visible)
        {
            if (action.SecurityId != securityId || string.IsNullOrWhiteSpace(action.ActionId) || !Enum.IsDefined(action.Kind))
                throw new InvalidDataException("Corporate-action IDs, kinds and securities must be valid.");
            if (action.SupersedesActionId is { } parentId && (!byId.TryGetValue(parentId, out var parent) ||
                action.FirstKnownAt <= parent.FirstKnownAt || action.SecurityId != parent.SecurityId))
                throw new InvalidDataException("Action corrections require an earlier visible predecessor for the same security.");
        }
        if (visible.Where(a => a.SupersedesActionId is not null).GroupBy(a => a.SupersedesActionId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Corporate-action correction chains cannot fork.");
        var superseded = visible.Select(a => a.SupersedesActionId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return visible.Where(a => !superseded.Contains(a.ActionId)).ToArray();
    }

    private static void ValidateTerms(CorporateAction action)
    {
        ValidateSource(action.SourceUrl, action.LicenceId, action.Verification);
        if (action.Sequence < 0 || action.CashRupeesPerShareBefore < 0) throw new InvalidDataException("Invalid action sequence or cash terms.");
        bool valid = action.Kind switch
        {
            CorporateActionKind.Split => action.SharesAfterPerShareBefore > 0m && action.CashRupeesPerShareBefore == 0m,
            CorporateActionKind.Bonus => action.SharesAfterPerShareBefore > 1m && action.CashRupeesPerShareBefore == 0m,
            CorporateActionKind.CashDividend => action.SharesAfterPerShareBefore == 1m && action.CashRupeesPerShareBefore > 0m,
            CorporateActionKind.TerminalCashSettlement => action.SharesAfterPerShareBefore == 0m && action.CashRupeesPerShareBefore > 0m,
            CorporateActionKind.TerminalWriteOff => action.SharesAfterPerShareBefore == 0m && action.CashRupeesPerShareBefore == 0m,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Unsupported corporate action or inconsistent share/cash terms.");
    }

    private static void ValidateSource(Uri uri, string licence, VerificationState verification)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("https" or "http") || string.IsNullOrWhiteSpace(licence) || verification != VerificationState.Verified)
            throw new InvalidDataException("Return inputs require verified HTTP(S) provenance and licence metadata.");
    }
    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
