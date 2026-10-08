namespace TradeTest.Domain;

public enum CorporateActionKind { Split, Bonus, CashDividend, TerminalCashSettlement, TerminalWriteOff, Unsupported }

/// <summary>Terms apply to one share immediately before this action, in Sequence order at the effective timestamp.</summary>
public sealed record CorporateAction(string ActionId, string SecurityId, CorporateActionKind Kind,
    DateTimeOffset EffectiveAt, DateTimeOffset FirstKnownAt, int Sequence,
    decimal SharesAfterPerShareBefore, decimal CashRupeesPerShareBefore,
    Uri SourceUrl, string LicenceId, VerificationState Verification, string? SupersedesActionId = null);

public sealed record RawCloseObservation(string ObservationId, string SecurityId,
    DateTimeOffset CloseAt, DateTimeOffset FirstKnownAt, decimal CloseRupees,
    Uri SourceUrl, string LicenceId, VerificationState Verification);

/// <summary>Explicit source assertion that the action history covers this entire interval; not inferred from an empty list.</summary>
public sealed record CorporateActionCoverage(string SecurityId, DateTimeOffset FromExclusive,
    DateTimeOffset ThroughInclusive, DateTimeOffset FirstKnownAt,
    Uri SourceUrl, string LicenceId, VerificationState Verification);
