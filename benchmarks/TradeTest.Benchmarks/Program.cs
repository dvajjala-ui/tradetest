using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;
using TradeTest.Infrastructure;

if (args.Length != 2 || !int.TryParse(args[1], out int count) || count is < 1 or > 100_000)
{
    Console.Error.WriteLine("Usage: replay <sessions> | import <documents> | regression 1");
    return 2;
}
object report;
switch (args[0])
{
    case "replay":
    {
        var start = new DateTimeOffset(2026, 1, 5, 9, 15, 0, TimeSpan.FromHours(5.5));
        var bars = Enumerable.Range(0, 75).Select(i => new MarketBar("SYNTH-BENCH",
            start.AddMinutes(i * 5), TimeSpan.FromMinutes(5), 100m, 100.1m, 99.9m, 100m, 1000m)).ToArray();
        var config = Config(new TimeSpan(15, 30, 0));
        var engine = new ReplayEngine();
        for (int i = 0; i < 100; i++) engine.Run(bars, config);
        var samples = new double[count];
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        decimal checksum = 0;
        for (int i = 0; i < count; i++)
        {
            long sample = Stopwatch.GetTimestamp();
            checksum += engine.Run(bars, config).NetPnl;
            samples[i] = Stopwatch.GetElapsedTime(sample).TotalMicroseconds;
        }
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Array.Sort(samples);
        report = new { Workload = "synthetic-flat-75-bar-session", Sessions = count,
            ElapsedMs = elapsed, SessionsPerSecond = count / (elapsed / 1000),
            P50Microseconds = Percentile(samples, 0.50), P95Microseconds = Percentile(samples, 0.95),
            P99Microseconds = Percentile(samples, 0.99), AllocatedBytesPerSession = allocated / count,
            NetPnlChecksum = checksum, Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription, LogicalProcessors = Environment.ProcessorCount };
        break;
    }
    case "import":
    {
        string path = Path.Combine(Path.GetTempPath(), $"tradetest-bench-{Guid.NewGuid():N}.sqlite");
        try
        {
            var store = new SqliteStore(path);
            await store.InitializeAsync();
            var at = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            var docs = Enumerable.Range(0, count).Select(i => ResearchServices.CreateDocument(
                $"doc-{i}", $"SYNTH-{i}", new Uri($"https://example.com/{i}"), "Synthetic publisher",
                at, at, at, "SYNTHETIC_ONLY", "manual-v1", $"Synthetic revenue report {i}.")).ToArray();
            var facts = docs.Select(d => new SourceFact($"fact-{d.DocumentId}", d.DocumentId,
                d.SecurityId, "Synthetic revenue grew 10 percent.", at, VerificationState.Verified)).ToArray();
            var metrics = facts.Select(f => new CompanyMetric(f.SecurityId,
                CompanyMetricKind.RevenueGrowth3YPercent, 10m, at, f.FactId, VerificationState.Verified)).ToArray();
            long started = Stopwatch.GetTimestamp();
            var imported = await store.ImportResearchAsync(new ResearchBatch("synthetic-benchmark", docs, facts, metrics));
            if (imported.Status != ResearchImportStatus.Applied) throw new InvalidOperationException(imported.Error);
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            report = new { Workload = "synthetic-document-fact-metric-import", Documents = count,
                Rows = count * 3, ElapsedMs = elapsed, RowsPerSecond = count * 3 / (elapsed / 1000),
                StoredFacts = (await store.GetFactsAtAsync(at)).Count,
                Runtime = RuntimeInformation.FrameworkDescription };
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (string name in new[] { path, path + "-wal", path + "-shm" }) File.Delete(name);
        }
        break;
    }
    case "regression":
    {
        var bars = JsonSerializer.Deserialize<MarketBar[]>(await File.ReadAllTextAsync("fixtures/synthetic-bars.json"))!;
        var config = Config(new TimeSpan(9, 40, 0));
        var flat = bars.Select(b => b with { Open = 100m, High = 100.1m, Low = 99.9m, Close = 100m }).ToArray();
        report = new[] {
            Regression("trade", bars, config), Regression("no-trade", flat, config),
            Regression("rejected", bars, config with { Policy = config.Policy with { MinimumRewardRisk = 3m } }),
            Regression("partial", bars, config with { MaxFillQuantityPerBar = 2 })
        };
        break;
    }
    default: return 2;
}
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return 0;

static double Percentile(double[] values, double fraction) => values[(int)Math.Ceiling(values.Length * fraction) - 1];
static SimulationConfig Config(TimeSpan end) => new(5000m, 5m, 2m, int.MaxValue, end,
    new RiskPolicy(5000m, 50m, 100m, 1, 1.5m, 30m, 5m, TimeSpan.FromSeconds(30), "bench-v1"));
static object Regression(string name, MarketBar[] bars, SimulationConfig config)
{
    var result = new ReplayEngine().Run(bars, config);
    string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result))));
    return new { Name = name, Hash = hash, result.NetPnl, result.CandidateCount, result.SubmittedOrders };
}
