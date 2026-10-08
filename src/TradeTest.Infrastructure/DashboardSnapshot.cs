using System.Security.Cryptography;
using System.Text.Json;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public sealed record SnapshotInput(string File, string Sha256);
public sealed record DashboardGate(string Id, string Title, string State, string Detail);
public sealed record DashboardDocument(string DocumentId, string SecurityId, string Publisher, Uri SourceUrl,
    DateTimeOffset PublishedAt, DateTimeOffset FirstKnownAt, string LicenceId, string ContentSha256);
public sealed record DashboardResearch(DateTimeOffset AsOf, string PacketHash,
    IReadOnlyList<SourceFact> Facts, IReadOnlyList<CompanyMetric> Metrics, IReadOnlyList<CompanyScore> Companies,
    IReadOnlyList<DashboardDocument> Documents);
public sealed record DashboardSnapshot(string SchemaVersion, DateTimeOffset GeneratedAt, string DataKind,
    string Mode, string EvidenceNote, IReadOnlyList<SnapshotInput> Inputs, IReadOnlyList<MarketBar> Bars,
    SimulationReport Replay, IntradayStudyReport IntradayStudy, WalkForwardReport WalkForward,
    LongTermEvaluationReport LongTerm, DashboardResearch Research, JsonElement Performance,
    IReadOnlyList<DashboardGate> Gates, TotalReturnBuildReport? ReturnAdjustments = null);

/// <summary>Exports only bundled synthetic fixtures. Never discovers or publishes a private database.</summary>
public static class DashboardSnapshotBuilder
{
    public const string SchemaVersion = "tradetest-dashboard-v1";
    private static readonly string[] FixtureFiles =
    ["synthetic-bars.json", "synthetic-research.json", "synthetic-study.json", "synthetic-walk-forward.json", "synthetic-long-term.json", "synthetic-corporate-actions.json"];

    public static string FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TradeTest.slnx")) && Directory.Exists(Path.Combine(directory.FullName, "fixtures")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Set TRADETEST_ROOT to the directory containing the bundled fixtures and benchmark results.");
    }

    public static async Task<DashboardSnapshot> BuildSyntheticAsync(string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        string fixtures = Path.Combine(repositoryRoot, "fixtures");
        // These independent reads are small and local; no network, credentials, or live account is used.
        var barsTask = ResearchJson.ReadAsync<MarketBar[]>(Path.Combine(fixtures, FixtureFiles[0]), cancellationToken);
        var researchTask = ResearchJson.ReadAsync<ResearchImportBatch>(Path.Combine(fixtures, FixtureFiles[1]), cancellationToken);
        var studyTask = ResearchJson.ReadAsync<IntradayStudyInput>(Path.Combine(fixtures, FixtureFiles[2]), cancellationToken);
        var walkTask = ResearchJson.ReadAsync<WalkForwardInput>(Path.Combine(fixtures, FixtureFiles[3]), cancellationToken);
        var longTask = ResearchJson.ReadAsync<LongTermInput>(Path.Combine(fixtures, FixtureFiles[4]), cancellationToken);
        var actionsTask = ResearchJson.ReadAsync<TotalReturnBuildInput>(Path.Combine(fixtures, FixtureFiles[5]), cancellationToken);
        string performancePath = Path.Combine(repositoryRoot, "benchmarks", "results", "2026-10-08.json");
        var performanceTask = ResearchJson.ReadAsync<JsonElement>(performancePath, cancellationToken);
        await Task.WhenAll(barsTask, researchTask, studyTask, walkTask, longTask, actionsTask, performanceTask);
        var research = await researchTask;
        var study = await studyTask;
        var walk = await walkTask;
        var longTerm = await longTask;
        var asOf = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var packet = ResearchServices.BuildPacket(research.Facts, asOf);
        var inputs = new List<SnapshotInput>();
        foreach (string name in FixtureFiles)
        {
            byte[] content = await File.ReadAllBytesAsync(Path.Combine(fixtures, name), cancellationToken);
            inputs.Add(new SnapshotInput("fixtures/" + name, Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()));
        }
        inputs.Add(new SnapshotInput("benchmarks/results/2026-10-08.json",
            Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(performancePath, cancellationToken))).ToLowerInvariant()));
        return new DashboardSnapshot(SchemaVersion, DateTimeOffset.UtcNow, "Synthetic", "PAPER / OFFLINE",
            "All companies, prices, filings and returns shown are invented software fixtures. No broker is connected. These results do not estimate profit probability.",
            inputs, await barsTask, new ReplayEngine().Run(await barsTask, ExampleResearch.Config()),
            new IntradayStudyEvaluator().Evaluate(study.Sessions, study.Config, study.Plan),
            new WalkForwardEvaluator().Evaluate(walk.Sessions, walk.Config, walk.Plan),
            new LongTermEvaluator().Evaluate(longTerm.DecisionTimes, longTerm.Metrics, longTerm.Prices,
                longTerm.BenchmarkSecurityId, longTerm.InitialCapital, longTerm.MaxHoldings,
                longTerm.EntryCostBps, longTerm.ExitCostBps, longTerm.FixedSellChargePerHolding, longTerm.ReferenceData, longTerm.ReturnDataAsOf, longTerm.InvestableBenchmark),
            new DashboardResearch(asOf, packet.Hash, packet.Facts, research.Metrics,
                new LongTermRanker().Rank(research.Metrics, asOf),
                research.Documents.Select(d => d.ToDocument()).Select(d => new DashboardDocument(d.DocumentId,
                    d.SecurityId, d.Publisher, d.SourceUrl, d.PublishedAt, d.FirstKnownAt, d.LicenceId, d.ContentSha256)).ToArray()),
            await performanceTask,
            [
                new("g0", "Licensed data", "Pending", "Vendor access, historical coverage and licences must be confirmed."),
                new("g1", "Deterministic replay", "Implemented", "Offline simulation, fee-aware risk and hash-chained journal; synthetic verification."),
                new("g2", "Company evidence", "Partial", "Dated facts, citations, correction withdrawal and atomic imports; vendor parsers remain."),
                new("g3", "Strategy evidence", "Partial", "Frozen chronological studies and walk-forward. No market edge has been established."),
                new("g4", "Live-data paper sessions", "Pending", "Authenticated no-order feed, reconciliation, fault drills and operational sessions remain."),
                new("g5", "Broker orders", "Not enabled", "Requires separately reviewed integration and evidence gates."),
                new("g6", "Real-money activation", "Not enabled", "Requires explicit activation and account-specific risk limits.")
            ], new TotalReturnBuilder().Build(await actionsTask));
    }
}
