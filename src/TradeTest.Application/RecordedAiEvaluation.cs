namespace TradeTest.Application;

public sealed record RecordedAiResponse(string CaseId, string ContextHash, string ResponseJson, AiProviderUsage? Usage);
public sealed class RecordedAssessmentUnavailableException : InvalidOperationException;

/// <summary>Replays a frozen response only when both candidate identity and the entire dated context hash match.</summary>
public sealed class RecordedAssessmentProvider : IAiAssessmentProvider
{
    private readonly IReadOnlyDictionary<string, RecordedAiResponse> _responses;
    public RecordedAssessmentProvider(IReadOnlyList<RecordedAiResponse> responses)
    {
        if (responses.Any(r => r is null || string.IsNullOrWhiteSpace(r.CaseId)))
            throw new ArgumentException("Recording identities cannot be empty.");
        _responses = responses.ToDictionary(r => r.CaseId, StringComparer.Ordinal);
    }

    public Task<AiProviderReply> AssessAsync(AiAssessmentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_responses.TryGetValue(request.CaseId, out var recording) || recording.ContextHash != request.ContextHash)
            throw new RecordedAssessmentUnavailableException();
        return Task.FromResult(new AiProviderReply(recording.ResponseJson, recording.Usage));
    }
}

public sealed record RecordedAiDataset(string SchemaVersion, string DataKind, string PlanId, string ModelId,
    AiAssessmentPolicy Policy, IReadOnlyList<AiEvaluationCase> Cases);
public sealed record AiPreparedCase(string CaseId, AiAssessmentStatus Status, AiAssessmentRequest? Request);
public sealed record AiPreparationReport(string SchemaVersion, string Mode, string DataKind, string PlanId,
    string ModelId, string DatasetHash, IReadOnlyList<AiPreparedCase> Cases);
public sealed record AiCaseComparison(string CaseId, bool RulesEligible, decimal RulesOnlyCandidateNetPnlRupees,
    decimal RecordedFilterCandidateNetPnlRupees, AiAssessmentResult Review);
public sealed record RecordedAiReport(string SchemaVersion, string Mode, string DataKind, string PlanId, string ModelId,
    string PromptVersion, string DatasetHash, string RecordingsHash, int CandidateCount, int RulesEligibleCount,
    int ContinuedCount, int ProviderCallCount, int InvalidResponseCount, int UnknownChargeCalls,
    long KnownInputTokens, long KnownOutputTokens, decimal KnownRecordedChargeUsd,
    decimal RulesOnlyCandidateNetPnlRupees, decimal RecordedFilterCandidateNetPnlRupees,
    decimal DifferenceBeforeInferenceCostRupees, IReadOnlyList<string> Limitations, IReadOnlyList<AiCaseComparison> Cases);

public sealed class RecordedAiEvaluator
{
    public AiPreparationReport Prepare(RecordedAiDataset dataset)
    {
        ValidateDataset(dataset);
        return new("tradetest-ai-requests-v1", "RECORDED_OFFLINE", dataset.DataKind, dataset.PlanId, dataset.ModelId,
            AiAssessmentContract.Hash(dataset), dataset.Cases.Select(c =>
            {
                var prepared = AiAssessmentContract.Prepare(c, dataset.ModelId, dataset.Policy);
                return new AiPreparedCase(c.CaseId, prepared.Status, prepared.Request);
            }).ToArray());
    }

    public async Task<RecordedAiReport> EvaluateAsync(RecordedAiDataset dataset, IReadOnlyList<RecordedAiResponse> recordings,
        CancellationToken cancellationToken = default)
    {
        ValidateDataset(dataset);
        var provider = new RecordedAssessmentProvider(recordings);
        var caseIds = dataset.Cases.Select(c => c.CaseId).ToHashSet(StringComparer.Ordinal);
        if (recordings.Count > dataset.Cases.Count || recordings.Any(r => !caseIds.Contains(r.CaseId)))
            throw new ArgumentException("Recordings must reference cases in the frozen dataset.");
        var gate = new AiAssessmentGate();
        var comparisons = new List<AiCaseComparison>(dataset.Cases.Count);
        foreach (var candidate in dataset.Cases)
        {
            var review = await gate.AssessAsync(candidate, dataset.ModelId, dataset.Policy, provider, cancellationToken);
            decimal net = candidate.Outcome.GrossPnlRupees - candidate.Outcome.TradingCostsRupees;
            comparisons.Add(new(candidate.CaseId, candidate.RulesEligible, candidate.RulesEligible ? net : 0,
                candidate.RulesEligible && review.AllowsFurtherReview ? net : 0, review));
        }
        decimal rules = comparisons.Sum(c => c.RulesOnlyCandidateNetPnlRupees);
        decimal filtered = comparisons.Sum(c => c.RecordedFilterCandidateNetPnlRupees);
        var usage = comparisons.Select(c => c.Review.Usage).OfType<AiProviderUsage>().ToArray();
        return new("tradetest-ai-evaluation-v1", "RECORDED_OFFLINE", dataset.DataKind, dataset.PlanId, dataset.ModelId,
            AiAssessmentContract.PromptVersion, AiAssessmentContract.Hash(dataset),
            AiAssessmentContract.Hash(recordings.OrderBy(r => r.CaseId, StringComparer.Ordinal).ToArray()),
            comparisons.Count, comparisons.Count(c => c.RulesEligible), comparisons.Count(c => c.Review.AllowsFurtherReview),
            comparisons.Count(c => c.Review.ProviderCalled), comparisons.Count(c => c.Review.Status == AiAssessmentStatus.InvalidResponse),
            comparisons.Count(c => c.Review.ProviderCalled && c.Review.Usage is null),
            usage.Sum(u => (long)u.InputTokens), usage.Sum(u => (long)u.OutputTokens), usage.Sum(u => u.ChargeUsd),
            rules, filtered, filtered - rules,
            ["Recorded responses verify contracts; they do not measure a model's skill or market edge.",
             "Candidate P&L uses fixed independent sizes and supplied trading costs; it is not a portfolio replay.",
             "Provider charges are recorded USD metadata, separate from rupee trading P&L; unknown charges are counted.",
             "Source and citation checks validate identity and chronology, not the truth of a claim or its relevance.",
             "No real provider call, calibrated profit probability, order permission or latency measurement is produced."], comparisons);
    }

    private static void ValidateDataset(RecordedAiDataset dataset)
    {
        if (dataset.SchemaVersion != "tradetest-ai-cases-v1" || dataset.DataKind is not ("Synthetic" or "ImportedRecords") ||
            string.IsNullOrWhiteSpace(dataset.PlanId) || dataset.PlanId.Length > 128 ||
            dataset.Cases is null || dataset.Cases.Count is < 1 or > 1_000 || dataset.Cases.Any(c => c is null) ||
            dataset.Cases.Select(c => c.CaseId).Distinct(StringComparer.Ordinal).Count() != dataset.Cases.Count ||
            !dataset.Cases.Select(c => c.AsOf).SequenceEqual(dataset.Cases.Select(c => c.AsOf).Order()))
            throw new ArgumentException("Use a versioned, named dataset with unique chronological cases and an explicit data kind.");
        dataset.Policy.Validate();
    }
}
