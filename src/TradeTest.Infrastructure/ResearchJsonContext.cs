using System.Text.Json.Serialization;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

internal sealed record ResearchPayload(IReadOnlyList<SourceDocument> Documents,
    IReadOnlyList<SourceFact> Facts, IReadOnlyList<CompanyMetric> Metrics);

[JsonSerializable(typeof(ResearchPayload))]
internal partial class ResearchJsonContext : JsonSerializerContext;
