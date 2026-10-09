using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
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
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };
    try
    {
        switch (args)
        {
            case ["demo"]:
                Print(new ReplayEngine().Run(ExampleResearch.Bars(), ExampleResearch.Config()), json);
                return 0;
            case ["prepare-ai", var datasetPath]:
            {
                var input = await AiEvaluationInputs.ReadAsync<RecordedAiDatasetInput>(datasetPath);
                Print(new RecordedAiEvaluator().Prepare(input.ToDataset()), ResearchJson.OutputOptions);
                return 0;
            }
            case ["evaluate-ai", var datasetPath, var recordingsPath]:
            {
                var input = await AiEvaluationInputs.ReadAsync<RecordedAiDatasetInput>(datasetPath);
                var recordings = await AiEvaluationInputs.ReadAsync<RecordedAiResponse[]>(recordingsPath);
                Print(await new RecordedAiEvaluator().EvaluateAsync(input.ToDataset(), recordings), ResearchJson.OutputOptions);
                return 0;
            }
            case ["export-dashboard", var rootPath, var outputPath]:
            {
                var snapshot = await DashboardSnapshotBuilder.BuildSyntheticAsync(rootPath);
                string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (directory is not null) Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(snapshot, ResearchJson.OutputOptions));
                Print(new { Output = Path.GetFullPath(outputPath), snapshot.SchemaVersion, snapshot.DataKind,
                    snapshot.GeneratedAt, InputCount = snapshot.Inputs.Count }, json);
                return 0;
            }
            case ["evaluate-intraday", var sessionsPath]:
            {
                var sessions = JsonSerializer.Deserialize<MarketBar[][]>(await File.ReadAllTextAsync(sessionsPath), json)
                    ?? throw new InvalidDataException("Sessions JSON is empty.");
                Print(new IntradayEvaluator().Evaluate(sessions, ExampleResearch.Config()), json);
                return 0;
            }
            case ["evaluate-study", var studyPath]:
            {
                var input = JsonSerializer.Deserialize<IntradayStudyInput>(await File.ReadAllTextAsync(studyPath), json)
                    ?? throw new InvalidDataException("Study JSON is empty.");
                Print(new IntradayStudyEvaluator().Evaluate(input.Sessions, input.Config, input.Plan), json);
                return 0;
            }
            case ["evaluate-walk-forward", var studyPath]:
            {
                string content = await File.ReadAllTextAsync(studyPath);
                var input = JsonSerializer.Deserialize<WalkForwardInput>(content, json)
                    ?? throw new InvalidDataException("Walk-forward input is empty.");
                Print(new { InputSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
                    Report = new WalkForwardEvaluator().Evaluate(input.Sessions, input.Config, input.Plan) }, json);
                return 0;
            }
            case ["universe", var referencePath, var asOfText]:
            {
                var reference = JsonSerializer.Deserialize<MarketReferenceData>(await File.ReadAllTextAsync(referencePath), json)
                    ?? throw new InvalidDataException("Market reference data is empty.");
                var asOf = DateTimeOffset.Parse(asOfText, System.Globalization.CultureInfo.InvariantCulture);
                Print(new { AsOf = asOf, Securities = new SecurityMaster(reference.Securities).UniverseAt(asOf) }, json);
                return 0;
            }
            case ["evaluate-long-term", var inputPath]:
            {
                var input = JsonSerializer.Deserialize<LongTermInput>(await File.ReadAllTextAsync(inputPath), json)
                    ?? throw new InvalidDataException("Long-term input JSON is empty.");
                Print(new LongTermEvaluator().Evaluate(input.DecisionTimes, input.Metrics, input.Prices,
                    input.BenchmarkSecurityId, input.InitialCapital, input.MaxHoldings,
                    input.EntryCostBps, input.ExitCostBps, input.FixedSellChargePerHolding, input.ReferenceData,
                    input.ReturnDataAsOf, input.InvestableBenchmark, input.EvidenceFacts, input.EvidenceDocuments), json);
                return 0;
            }
            case ["build-total-return", var inputPath]:
            {
                string content = await File.ReadAllTextAsync(inputPath);
                var input = JsonSerializer.Deserialize<TotalReturnBuildInput>(content, json)
                    ?? throw new InvalidDataException("Total-return input is empty.");
                Print(new { InputFileSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
                    Report = new TotalReturnBuilder().Build(input) }, json);
                return 0;
            }
            case ["replay", _, _, _] or ["replay", _, _, _, _]:
            {
                string barsPath = args[1], databasePath = args[2], stream = args[3];
                var bars = JsonSerializer.Deserialize<MarketBar[]>(await File.ReadAllTextAsync(barsPath), json)
                    ?? throw new InvalidDataException("Bars JSON is empty.");
                var config = ExampleResearch.Config();
                if (args.Length == 5)
                    config = config with { ReferenceData = JsonSerializer.Deserialize<MarketReferenceData>(await File.ReadAllTextAsync(args[4]), json)
                        ?? throw new InvalidDataException("Market reference data is empty.") };
                var result = new ReplayEngine().Run(bars, config);
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
                var store = new SqliteStore(databasePath, readOnly: true);
                var events = await store.ReadEventsAsync(stream);
                Print(new { Stream = stream, EventCount = events.Count, LastHash = events.LastOrDefault()?.Hash, Events = events }, json);
                return 0;
            }
            case ["import-research", var batchPath, var databasePath]:
            {
                string content = await File.ReadAllTextAsync(batchPath);
                var batch = JsonSerializer.Deserialize<ResearchImportBatch>(content, json)
                    ?? throw new InvalidDataException("Research batch JSON is empty.");
                if (batch.Documents.Any(d => d is null)) throw new InvalidDataException("Document input rows cannot be null.");
                var store = new SqliteStore(databasePath);
                await store.InitializeAsync();
                var docs = batch.Documents.Select(d => d.ToDocument()).ToArray();
                string id = batch.BatchId ?? "file-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
                var result = await store.ImportResearchAsync(new ResearchBatch(id, docs, batch.Facts, batch.Metrics));
                Print(result, json);
                return result.Status == ResearchImportStatus.Quarantined ? 1 : 0;
            }
            case ["health", var databasePath]:
            {
                var store = new SqliteStore(databasePath, readOnly: true);
                Print(await store.ReadResearchHealthSnapshotAsync(), json);
                return 0;
            }
            case ["research", _, _, _, _]:
            case ["export-research", _, _, _, _, _]:
            {
                var asOf = DateTimeOffset.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
                var report = await new SqliteStore(args[1], readOnly: true).ReadCompanySnapshotAsync(args[3], asOf, args[4]);
                if (args[0] == "export-research")
                {
                    string output = Path.GetFullPath(args[5]);
                    if (output == Path.GetFullPath(args[1]) || output == Path.GetFullPath(args[1]) + "-wal" || output == Path.GetFullPath(args[1]) + "-shm")
                        throw new ArgumentException("The output must not overwrite the research database or its journal files.");
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, ResearchJson.OutputOptions));
                    Print(new { Output = output, report.SchemaVersion, report.SecurityId, report.AsOf, report.Hash }, json);
                }
                else Print(report, ResearchJson.OutputOptions);
                return 0;
            }
            default:
                Console.Error.WriteLine("Usage: demo | prepare-ai <cases.json> | evaluate-ai <cases.json> <recordings.json> | export-dashboard <repository-root> <output.json> | build-total-return <input.json> | evaluate-intraday <sessions.json> | evaluate-study <input.json> | evaluate-walk-forward <input.json> | evaluate-long-term <input.json> | universe <reference.json> <as-of-ISO> | replay <bars.json> <db.sqlite> <stream> [reference.json] | journal <db.sqlite> <stream> | import-research <batch.json> <db.sqlite> | health <db.sqlite> | research <db.sqlite> <as-of-ISO> <security-id> <search-words> | export-research <db.sqlite> <as-of-ISO> <security-id> <search-words> <output.json>");
                return 2;
        }
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or JsonException or IOException or FormatException or SqliteException or OverflowException)
    {
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
        return 1;
    }
}

static void Print(object value, JsonSerializerOptions options) => Console.WriteLine(JsonSerializer.Serialize(value, options));
