namespace TradeTest.Domain;

/// <summary>Append a version for listings, symbol changes, delistings, or corrections.</summary>
public sealed record SecurityVersion(
    string VersionId, string SecurityId, string Isin, string Exchange, string Symbol, string Segment,
    DateTimeOffset EffectiveAt, DateTimeOffset FirstKnownAt, bool IsListed,
    Uri SourceUrl, string LicenceId, VerificationState Verification);

/// <summary>Explicit exchange session; special sessions and closures are supplied by the source.</summary>
public sealed record ExchangeSession(
    string Exchange, DateOnly SessionDate, DateTimeOffset OpensAt, DateTimeOffset ClosesAt,
    DateTimeOffset FirstKnownAt, Uri SourceUrl, string LicenceId, VerificationState Verification);

public sealed record MarketReferenceData(
    IReadOnlyList<SecurityVersion> Securities, IReadOnlyList<ExchangeSession> Sessions);
