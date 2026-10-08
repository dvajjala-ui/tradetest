namespace TradeTest.Domain;

public sealed record ResearchBatch(
    string BatchId,
    IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<SourceFact> Facts,
    IReadOnlyList<CompanyMetric> Metrics);

public enum ResearchImportStatus { Applied, AlreadyApplied, Quarantined }

public sealed record ResearchImportResult(
    string BatchId, string PayloadHash, ResearchImportStatus Status,
    int DocumentCount, int FactCount, int MetricCount, string? Error);

public sealed record ResearchImportAudit(
    string BatchId, string PayloadHash, ResearchImportStatus Status,
    DateTimeOffset AttemptedAt, int DocumentCount, int FactCount, int MetricCount, string? Error);

public sealed record ResearchHealthReport(
    int Documents, int Facts, int Metrics, int UnverifiedFacts,
    int AppliedBatches, int QuarantinedBatches, DateTimeOffset? LatestFirstKnownAt);
