namespace TradeTest.Domain;

public enum VerificationState { Unverified, Partial, Verified }

public sealed record SourceDocument(
    string DocumentId,
    string SecurityId,
    Uri SourceUrl,
    string Publisher,
    DateTimeOffset PublishedAt,
    DateTimeOffset FirstKnownAt,
    DateTimeOffset RetrievedAt,
    string ContentSha256,
    string LicenceId,
    string ParserVersion,
    string? SupersedesDocumentId,
    string Content);

public sealed record SourceFact(
    string FactId,
    string DocumentId,
    string SecurityId,
    string Claim,
    DateTimeOffset FirstKnownAt,
    VerificationState Verification,
    string? SupersedesFactId = null);

public sealed record ResearchPacket(
    DateTimeOffset AsOf,
    string Version,
    IReadOnlyList<SourceFact> Facts,
    string Hash);

public enum CompanyMetricKind
{
    RevenueGrowth3YPercent,
    ReturnOnCapitalPercent,
    FreeCashFlowMarginPercent,
    NetDebtToEbitda,
    ShareDilution3YPercent,
    Momentum12MPercent,
    AverageDailyTurnoverRupees
}

public sealed record CompanyMetric(
    string SecurityId,
    CompanyMetricKind Kind,
    decimal Value,
    DateTimeOffset FirstKnownAt,
    string SourceFactId,
    VerificationState Verification);

/// <summary>A dated research rank, never an order or probability of return.</summary>
public sealed record CompanyScore(
    string SecurityId,
    DateTimeOffset AsOf,
    decimal Score,
    IReadOnlyList<string> EvidenceFactIds,
    IReadOnlyList<string> RiskFlags,
    string ModelVersion);
