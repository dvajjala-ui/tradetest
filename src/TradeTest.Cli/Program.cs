using System.Text.Json;
using System.Text.Json.Serialization;
using TradeTest.Application;
using TradeTest.Domain;
using TradeTest.Infrastructure;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    var json = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    try
    {
        switch (args)
        {
            case ["demo"]:
                Print(new ReplayEngine().Run(SyntheticBars(), ExampleConfig()), json);
                return 0;
            case ["evaluate-intraday", var sessionsPath]:
            {
                var sessions = JsonSerializer.Deserialize<MarketBar[][]>(await File.ReadAllTextAsync(sessionsPath), json)
                    ?? throw new InvalidDataException("Sessions JSON is empty.");
                Print(new IntradayEvaluator().Evaluate(sessions, ExampleConfig()), json);
                return 0;
            }
            case ["evaluate-study", var studyPath]:
            {
                var input = JsonSerializer.Deserialize<IntradayStudyInput>(await File.ReadAllTextAsync(studyPath), json)
                    ?? throw new InvalidDataException("Study JSON is empty.");
                Print(new IntradayStudyEvaluator().Evaluate(input.Sessions, input.Config, input.Plan), json);
                return 0;
            }
            case ["evaluate-long-term", var inputPath]:
            {
                var input = JsonSerializer.Deserialize<LongTermInput>(await File.ReadAllTextAsync(inputPath), json)
                    ?? throw new InvalidDataException("Long-term input JSON is empty.");
                Print(new LongTermEvaluator().Evaluate(input.DecisionTimes, input.Metrics, input.Prices,
                    input.BenchmarkSecurityId, input.InitialCapital, input.MaxHoldings,
                    input.EntryCostBps, input.ExitCostBps, input.FixedSellChargePerHolding), json);
                return 0;
            }
            case ["replay", var barsPath, var databasePath, var stream]:
            {
                var bars = JsonSerializer.Deserialize<MarketBar[]>(await File.ReadAllTextAsync(barsPath), json)
                    ?? throw new InvalidDataException("Bars JSON is empty.");
                var result = new ReplayEngine().Run(bars, ExampleConfig());
                var store = new SqliteStore(databasePath);
                await store.InitializeAsync();
                await store.AppendEventsAsync(stream, 0, result.Events);
                Print(new { result.StrategyVersion, result.CostModelVersion, result.CandidateCount,
                    result.RiskRejectedCount, result.SubmittedOrders, result.Trades,
                    result.NetPnl, result.NetReturnPercent, result.MaxClosedEquityDrawdownPercent,
                    JournalStream = stream, JournalEvents = result.Events.Count }, json);
                return 0;
            }
            case ["journal", var databasePath, var stream]:
            {
                var store = new SqliteStore(databasePath);
                await store.InitializeAsync();
                var events = await store.ReadEventsAsync(stream);
                Print(new { Stream = stream, EventCount = events.Count, LastHash = events.LastOrDefault()?.Hash, Events = events }, json);
                return 0;
            }
            case ["import-research", var batchPath, var databasePath]:
            {
                var batch = JsonSerializer.Deserialize<ResearchImportBatch>(await File.ReadAllTextAsync(batchPath), json)
                    ?? throw new InvalidDataException("Research batch JSON is empty.");
                var store = new SqliteStore(databasePath);
                await store.InitializeAsync();
                foreach (var input in batch.Documents)
                {
                    var doc = ResearchServices.CreateDocument(input.DocumentId, input.SecurityId,
                        new Uri(input.SourceUrl), input.Publisher, input.PublishedAt,
                        input.FirstKnownAt, input.RetrievedAt, input.LicenceId, input.ParserVersion,
                        input.Content, input.SupersedesDocumentId);
                    await store.AddDocumentAsync(doc);
                }
                foreach (var fact in batch.Facts) await store.AddFactAsync(fact);
                foreach (var metric in batch.Metrics) await store.AddMetricAsync(metric);
                Print(new { DocumentCount = batch.Documents.Count, FactCount = batch.Facts.Count, MetricCount = batch.Metrics.Count }, json);
                return 0;
            }
            case ["research", var databasePath, var asOfText, var securityId, var search]:
            {
                var asOf = DateTimeOffset.Parse(asOfText, System.Globalization.CultureInfo.InvariantCulture);
                var store = new SqliteStore(databasePath);
                await store.InitializeAsync();
                var packet = ResearchServices.BuildPacket(await store.GetFactsAtAsync(asOf), asOf);
                var ranked = new LongTermRanker().Rank(await store.GetMetricsAtAsync(asOf), asOf);
                var citations = await store.SearchDocumentsAsync(securityId, search, asOf);
                Print(new { packet.AsOf, packet.Version, packet.Hash, FactCount = packet.Facts.Count,
                    Facts = ResearchServices.CiteCompany(packet, securityId, search),
                    Documents = citations.Select(d => new { d.DocumentId, d.SourceUrl, d.PublishedAt,
                        d.FirstKnownAt, d.ContentSha256, d.LicenceId }),
                    RankedCompanies = ranked }, json);
                return 0;
            }
            default:
                Console.Error.WriteLine("Usage: demo | evaluate-intraday <sessions.json> | evaluate-study <input.json> | evaluate-long-term <input.json> | replay <bars.json> <db.sqlite> <stream> | journal <db.sqlite> <stream> | import-research <batch.json> <db.sqlite> | research <db.sqlite> <as-of-ISO> <security-id> <search-words>");
                return 2;
        }
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or JsonException or IOException)
    {
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
        return 1;
    }
}

static SimulationConfig ExampleConfig() => new(
    InitialCash: 5_000m,
    SpreadBps: 5m,
    SlippageBps: 2m,
    MaxFillQuantityPerBar: int.MaxValue,
    SessionEndLocalTime: new TimeSpan(9, 40, 0),
    Policy: new RiskPolicy(5_000m, 50m, 100m, 1, 1.5m, 30m, 5m, TimeSpan.FromSeconds(30), "paper-example-v1"));

static MarketBar[] SyntheticBars()
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

static void Print(object value, JsonSerializerOptions options) => Console.WriteLine(JsonSerializer.Serialize(value, options));

public sealed record SourceDocumentInput(
    string DocumentId, string SecurityId, string SourceUrl, string Publisher,
    DateTimeOffset PublishedAt, DateTimeOffset FirstKnownAt, DateTimeOffset RetrievedAt,
    string LicenceId, string ParserVersion, string Content, string? SupersedesDocumentId);

public sealed record ResearchImportBatch(
    IReadOnlyList<SourceDocumentInput> Documents,
    IReadOnlyList<SourceFact> Facts,
    IReadOnlyList<CompanyMetric> Metrics);

public sealed record LongTermInput(
    IReadOnlyList<DateTimeOffset> DecisionTimes,
    IReadOnlyList<CompanyMetric> Metrics,
    IReadOnlyList<TotalReturnPrice> Prices,
    string BenchmarkSecurityId,
    decimal InitialCapital,
    int MaxHoldings,
    decimal EntryCostBps,
    decimal ExitCostBps,
    decimal FixedSellChargePerHolding);

public sealed record IntradayStudyInput(
    IReadOnlyList<IReadOnlyList<MarketBar>> Sessions,
    SimulationConfig Config,
    IntradayStudyPlan Plan);
