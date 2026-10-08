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
    string AdjustmentVersion,
    bool IsTerminal = false,
    string? TerminalReason = null,
    IReadOnlyList<string>? SourceEvidenceIds = null);

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
    decimal BenchmarkTriReturnPercent,
    decimal InitialCapital = 0m,
    decimal FinalCapital = 0m,
    decimal TradingCostsRupees = 0m,
    decimal UnfundedExitCostsRupees = 0m,
    IReadOnlyList<string>? TerminalSecurities = null);

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
    bool PointInTimeUniverseSupplied = false,
    DateTimeOffset? ReturnDataAsOf = null,
    bool PortfolioDepleted = false,
    InvestableBenchmarkReport? InvestableBenchmark = null,
    bool SourceProvenanceValidated = false);
