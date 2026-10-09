using System.Text.Json;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public sealed record AiEvaluationCaseInput(string CaseId, string SecurityId, string StrategyVersion,
    DateTimeOffset AsOf, DateTimeOffset ReviewAt, DateTimeOffset ExpiresAt, bool RulesEligible,
    IReadOnlyList<SourceFact> Facts, IReadOnlyList<SourceDocumentInput> Documents, AiCaseOutcome Outcome)
{
    public AiEvaluationCase ToCase()
    {
        if (Documents.Any(d => d is null)) throw new InvalidDataException("Assessment documents cannot be null.");
        return new(CaseId, SecurityId, StrategyVersion, AsOf, ReviewAt, ExpiresAt, RulesEligible,
            Facts, Documents.Select(d => d.ToDocument()).ToArray(), Outcome);
    }
}

public sealed record RecordedAiDatasetInput(string SchemaVersion, string DataKind, string PlanId, string ModelId,
    AiAssessmentPolicy Policy, IReadOnlyList<AiEvaluationCaseInput> Cases)
{
    public RecordedAiDataset ToDataset()
    {
        if (Cases.Count is < 1 or > 1_000 || Cases.Any(c => c is null))
            throw new InvalidDataException("Assessment inputs require 1 to 1,000 non-null cases.");
        return new(SchemaVersion, DataKind, PlanId, ModelId, Policy, Cases.Select(c => c.ToCase()).ToArray());
    }
}

public static class AiEvaluationInputs
{
    // Bound files before deserializing. Recorded fixtures contain model JSON as a string, so its byte limit is enforced again at the gate.
    public static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length > 8 * 1_048_576) throw new InvalidDataException("Recorded evaluation files cannot exceed 8 MiB.");
        return await JsonSerializer.DeserializeAsync<T>(stream, AiAssessmentContract.JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Recorded evaluation input is empty.");
    }
}
