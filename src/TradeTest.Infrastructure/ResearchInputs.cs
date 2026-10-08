using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public static class ResearchJson
{
    public static JsonSerializerOptions InputOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static JsonSerializerOptions OutputOptions { get; } = new(InputOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, InputOptions, cancellationToken)
            ?? throw new InvalidDataException($"Input {Path.GetFileName(path)} is empty.");
    }
}

public sealed record SourceDocumentInput(
    string DocumentId, string SecurityId, string SourceUrl, string Publisher,
    DateTimeOffset PublishedAt, DateTimeOffset FirstKnownAt, DateTimeOffset RetrievedAt,
    string LicenceId, string ParserVersion, string Content, string? SupersedesDocumentId)
{
    public SourceDocument ToDocument() => new(DocumentId, SecurityId, new Uri(SourceUrl, UriKind.RelativeOrAbsolute),
        Publisher, PublishedAt, FirstKnownAt, RetrievedAt,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content))).ToLowerInvariant(),
        LicenceId, ParserVersion, SupersedesDocumentId, Content);
}

public sealed record ResearchImportBatch(
    IReadOnlyList<SourceDocumentInput> Documents,
    IReadOnlyList<SourceFact> Facts,
    IReadOnlyList<CompanyMetric> Metrics,
    string? BatchId = null);

public sealed record LongTermInput(
    IReadOnlyList<DateTimeOffset> DecisionTimes,
    IReadOnlyList<CompanyMetric> Metrics,
    IReadOnlyList<TotalReturnPrice> Prices,
    string BenchmarkSecurityId,
    decimal InitialCapital,
    int MaxHoldings,
    decimal EntryCostBps,
    decimal ExitCostBps,
    decimal FixedSellChargePerHolding,
    MarketReferenceData? ReferenceData = null,
    DateTimeOffset? ReturnDataAsOf = null,
    InvestableBenchmarkInput? InvestableBenchmark = null,
    IReadOnlyList<SourceFact>? EvidenceFacts = null,
    IReadOnlyList<SourceDocument>? EvidenceDocuments = null);

public sealed record IntradayStudyInput(
    IReadOnlyList<IReadOnlyList<MarketBar>> Sessions,
    SimulationConfig Config,
    IntradayStudyPlan Plan);

public sealed record WalkForwardInput(IReadOnlyList<IReadOnlyList<MarketBar>> Sessions,
    SimulationConfig Config, WalkForwardPlan Plan);

public static class ExampleResearch
{
    public static SimulationConfig Config() => new(5_000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0),
        new RiskPolicy(5_000m, 50m, 100m, 1, 1.5m, 30m, 5m, TimeSpan.FromSeconds(30), "paper-example-v2-fees"));

    public static MarketBar[] Bars()
    {
        var start = new DateTimeOffset(2026, 1, 5, 9, 15, 0, TimeSpan.FromHours(5.5));
        (decimal o, decimal h, decimal l, decimal c, decimal v)[] values =
        [
            (100m, 100.3m, 99.9m, 100.1m, 1000m),
            (100.1m, 100.5m, 100m, 100.4m, 1000m),
            (100.4m, 100.6m, 100.2m, 100.5m, 1000m),
            (100.5m, 101.1m, 100.4m, 101m, 2000m),
            (100.99m, 101.9m, 100.8m, 101.7m, 1500m)
        ];
        return values.Select((v, i) => new MarketBar("SYNTH-ONE", start.AddMinutes(5 * i),
            TimeSpan.FromMinutes(5), v.o, v.h, v.l, v.c, v.v)).ToArray();
    }
}
