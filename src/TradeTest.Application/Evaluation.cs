using TradeTest.Domain;

namespace TradeTest.Application;

public sealed record IntradayEvaluationReport(
    int Sessions,
    int NoTradeSessions,
    int Candidates,
    int Trades,
    decimal GrossPnl,
    decimal TradingCosts,
    decimal NetPnl,
    decimal FixedCapitalReturnPercent,
    decimal? WinRatePercent,
    decimal? NetPnlPerTrade,
    decimal? ProfitFactor,
    decimal MaxClosedEquityDrawdownPercent,
    decimal? ExploratoryBootstrapMeanLower95,
    decimal? ExploratoryBootstrapMeanUpper95,
    string EvidenceNote);

public sealed class IntradayEvaluator
{
    public IntradayEvaluationReport Evaluate(IEnumerable<IReadOnlyList<MarketBar>> sessions, SimulationConfig config)
    {
        var ordered = sessions.Select(s => new { Bars = s, First = s.Count > 0 ? s[0].StartsAt : DateTimeOffset.MinValue })
            .OrderBy(s => s.First).ToArray();
        if (ordered.Length == 0 || ordered.Any(s => s.Bars.Count == 0))
            throw new ArgumentException("At least one nonempty session is required.", nameof(sessions));
        if (ordered.Select(s => s.First.Date).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Duplicate session date.", nameof(sessions));
        var engine = new ReplayEngine();
        var reference = config.ReferenceData is null ? null : new MarketReferenceValidator(config.ReferenceData);
        var reports = ordered.Select(s => engine.RunCompiled(s.Bars, config, captureEvents: false, referenceValidator: reference)).ToArray();
        return Summarize(reports, config.InitialCash);
    }

    internal static IntradayEvaluationReport Summarize(IReadOnlyList<SimulationReport> reports, decimal initialCash,
        bool includeBootstrap = true)
    {
        var trades = reports.SelectMany(r => r.Trades).ToArray();
        decimal gross = trades.Sum(t => t.GrossPnl);
        decimal costs = trades.Sum(t => t.Costs.Total);
        decimal net = trades.Sum(t => t.NetPnl);
        decimal gains = trades.Where(t => t.NetPnl > 0).Sum(t => t.NetPnl);
        decimal losses = -trades.Where(t => t.NetPnl < 0).Sum(t => t.NetPnl);
        decimal? profitFactor = losses > 0 ? gains / losses : null;
        decimal peak = initialCash, equity = peak, drawdown = 0;
        foreach (var trade in trades)
        {
            equity += trade.NetPnl;
            peak = Math.Max(peak, equity);
            drawdown = Math.Max(drawdown, (peak - equity) / peak * 100m);
        }
        (decimal Lower, decimal Upper)? interval = includeBootstrap && trades.Length >= 30
            ? BootstrapMean(trades.Select(t => t.NetPnl).ToArray()) : null;
        return new IntradayEvaluationReport(reports.Count, reports.Count(r => r.Trades.Count == 0),
            reports.Sum(r => r.CandidateCount), trades.Length, gross, costs, net,
            net / initialCash * 100m,
            trades.Length > 0 ? trades.Count(t => t.NetPnl > 0) * 100m / trades.Length : null,
            trades.Length > 0 ? net / trades.Length : null, profitFactor, drawdown,
            interval?.Lower, interval?.Upper,
            "Fixed starting capital per session; no compounding. Bootstrap interval is exploratory and assumes independent trades; it cannot establish a durable edge.");
    }

    private static (decimal Lower, decimal Upper) BootstrapMean(decimal[] values)
    {
        var random = new Random(90417);
        var means = new decimal[1000];
        for (int iteration = 0; iteration < means.Length; iteration++)
        {
            decimal sum = 0;
            for (int i = 0; i < values.Length; i++) sum += values[random.Next(values.Length)];
            means[iteration] = sum / values.Length;
        }
        Array.Sort(means);
        return (means[25], means[974]);
    }
}

/// <summary>Point-in-time adjusted total-return observation for research, not an executable quote.</summary>
public sealed record TotalReturnPrice(
    string SecurityId,
    DateTimeOffset CloseAt,
    DateTimeOffset FirstKnownAt,
    decimal AdjustedTotalReturnClose,
    string AdjustmentVersion);

public sealed record LongTermPeriodResult(
    DateTimeOffset DecisionAt,
    DateTimeOffset NextDecisionAt,
    DateTimeOffset EntryCloseAt,
    DateTimeOffset ExitCloseAt,
    IReadOnlyList<string> Securities,
    IReadOnlyList<string> EvidenceFactIds,
    decimal GrossReturnPercent,
    decimal CostPercent,
    decimal NetReturnPercent,
    decimal BenchmarkTriReturnPercent);

public sealed record LongTermEvaluationReport(
    string StrategyVersion,
    string BenchmarkSecurityId,
    decimal InitialCapital,
    decimal FinalCapital,
    decimal TotalNetReturnPercent,
    decimal BenchmarkTriReturnPercent,
    decimal MaximumRebalanceDrawdownPercent,
    IReadOnlyList<LongTermPeriodResult> Periods,
    string EvidenceNote,
    bool PointInTimeUniverseSupplied = false);

/// <summary>Research backtest with next available close execution and explicit data failures.</summary>
public sealed class LongTermEvaluator
{
    private readonly LongTermRanker _ranker = new();

    public LongTermEvaluationReport Evaluate(
        IReadOnlyList<DateTimeOffset> decisionTimes,
        IReadOnlyList<CompanyMetric> metrics,
        IReadOnlyList<TotalReturnPrice> prices,
        string benchmarkSecurityId,
        decimal initialCapital,
        int maxHoldings,
        decimal entryCostBps,
        decimal exitCostBps,
        decimal fixedSellChargePerHolding,
        MarketReferenceData? referenceData = null)
    {
        if (decisionTimes.Count < 2 || decisionTimes.Zip(decisionTimes.Skip(1)).Any(x => x.First >= x.Second))
            throw new ArgumentException("At least two strictly increasing decision times are required.", nameof(decisionTimes));
        if (initialCapital <= 0 || maxHoldings <= 0 || entryCostBps < 0 || exitCostBps < 0 || fixedSellChargePerHolding < 0)
            throw new ArgumentException("Invalid portfolio/cost configuration.");
        if (prices.Any(p => p.AdjustedTotalReturnClose <= 0 || p.FirstKnownAt < p.CloseAt))
            throw new ArgumentException("Invalid dated total-return price.", nameof(prices));
        if (prices.GroupBy(p => (p.SecurityId, p.CloseAt)).Any(g => g.Count() > 1))
            throw new ArgumentException("Duplicate security/close timestamp.", nameof(prices));

        var bySecurity = prices.GroupBy(p => p.SecurityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CloseAt).ToArray(), StringComparer.OrdinalIgnoreCase);
        if (!bySecurity.TryGetValue(benchmarkSecurityId, out var benchmark))
            throw new ArgumentException("Benchmark price series is required.", nameof(prices));
        var master = referenceData is null ? null : new SecurityMaster(referenceData.Securities);
        decimal capital = initialCapital, benchmarkGrowth = 1m, peak = capital, drawdown = 0;
        var periods = new List<LongTermPeriodResult>();
        for (int i = 0; i < decisionTimes.Count - 1; i++)
        {
            DateTimeOffset decision = decisionTimes[i], nextDecision = decisionTimes[i + 1];
            var entryBenchmark = benchmark.FirstOrDefault(p => p.CloseAt > decision)
                ?? throw new InvalidDataException("Missing benchmark entry close.");
            var exitBenchmark = benchmark.FirstOrDefault(p => p.CloseAt > nextDecision)
                ?? throw new InvalidDataException("Missing benchmark exit close.");
            if (entryBenchmark.AdjustmentVersion != exitBenchmark.AdjustmentVersion)
                throw new InvalidDataException("Benchmark adjustment versions differ.");
            decimal benchmarkReturn = exitBenchmark.AdjustedTotalReturnClose / entryBenchmark.AdjustedTotalReturnClose - 1m;
            benchmarkGrowth *= 1m + benchmarkReturn;

            var selected = _ranker.Rank(metrics, decision)
                .Where(s => master is null || master.GetAt(s.SecurityId, decision) is { Segment: var segment } &&
                    segment.Equals("CASH_EQUITY", StringComparison.OrdinalIgnoreCase)).Take(maxHoldings).ToArray();
            decimal gross = 0m;
            foreach (var score in selected)
            {
                if (!bySecurity.TryGetValue(score.SecurityId, out var series))
                    throw new InvalidDataException($"No total-return prices for {score.SecurityId}.");
                var entry = series.SingleOrDefault(p => p.CloseAt == entryBenchmark.CloseAt)
                    ?? throw new InvalidDataException($"Missing entry close for {score.SecurityId}.");
                var exit = series.SingleOrDefault(p => p.CloseAt == exitBenchmark.CloseAt)
                    ?? throw new InvalidDataException($"Missing exit/delisting value for {score.SecurityId}.");
                if (entry.AdjustmentVersion != exit.AdjustmentVersion)
                    throw new InvalidDataException($"Adjustment versions differ for {score.SecurityId}.");
                gross += exit.AdjustedTotalReturnClose / entry.AdjustedTotalReturnClose - 1m;
            }
            if (selected.Length > 0) gross /= selected.Length;
            decimal cost = selected.Length == 0 ? 0m :
                (entryCostBps + exitCostBps) / 10_000m + selected.Length * fixedSellChargePerHolding / capital;
            decimal net = gross - cost;
            capital *= 1m + net;
            if (capital <= 0) throw new InvalidDataException("Portfolio depleted under supplied cost/return assumptions.");
            peak = Math.Max(peak, capital);
            drawdown = Math.Max(drawdown, (peak - capital) / peak * 100m);
            periods.Add(new LongTermPeriodResult(decision, nextDecision, entryBenchmark.CloseAt,
                exitBenchmark.CloseAt, selected.Select(s => s.SecurityId).ToArray(),
                selected.SelectMany(s => s.EvidenceFactIds).Distinct().Order(StringComparer.Ordinal).ToArray(),
                gross * 100m, cost * 100m, net * 100m, benchmarkReturn * 100m));
        }
        return new LongTermEvaluationReport(LongTermRanker.Version, benchmarkSecurityId, initialCapital,
            capital, (capital / initialCapital - 1m) * 100m, (benchmarkGrowth - 1m) * 100m,
            drawdown, periods,
            "Rank uses only verified facts known by each decision time. Execution uses the next supplied adjusted close. Drawdown is measured only at rebalances. TRI is a research benchmark, not an investable after-fee fund return; costs are supplied assumptions.",
            referenceData is not null);
    }
}
