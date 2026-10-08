using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Equal-value fractional research portfolios, funded entry fees and explicit terminal outcomes.</summary>
public sealed class LongTermEvaluator
{
    private readonly LongTermRanker _ranker = new();

    public LongTermEvaluationReport Evaluate(
        IReadOnlyList<DateTimeOffset> decisionTimes, IReadOnlyList<CompanyMetric> metrics,
        IReadOnlyList<TotalReturnPrice> prices, string benchmarkSecurityId, decimal initialCapital,
        int maxHoldings, decimal entryCostBps, decimal exitCostBps, decimal fixedSellChargePerHolding,
        MarketReferenceData? referenceData = null, DateTimeOffset? returnDataAsOf = null,
        InvestableBenchmarkInput? investableBenchmark = null)
    {
        if (decisionTimes.Count < 2 || decisionTimes.Zip(decisionTimes.Skip(1)).Any(x => x.First >= x.Second))
            throw new ArgumentException("At least two strictly increasing decision times are required.", nameof(decisionTimes));
        if (initialCapital <= 0 || maxHoldings <= 0 || entryCostBps < 0 || exitCostBps is < 0 or >= 10_000 ||
            fixedSellChargePerHolding < 0 || string.IsNullOrWhiteSpace(benchmarkSecurityId))
            throw new ArgumentException("Invalid portfolio/cost configuration.");
        if (prices.Count == 0 || prices.Any(p => p is null || string.IsNullOrWhiteSpace(p.SecurityId) ||
            string.IsNullOrWhiteSpace(p.AdjustmentVersion) || p.AdjustedTotalReturnClose < 0 ||
            p.AdjustedTotalReturnClose == 0 && !p.IsTerminal || p.FirstKnownAt < p.CloseAt ||
            p.IsTerminal && string.IsNullOrWhiteSpace(p.TerminalReason)))
            throw new ArgumentException("Invalid dated total-return price or terminal provenance.", nameof(prices));
        DateTimeOffset cutoff = returnDataAsOf ?? prices.Max(p => p.FirstKnownAt);
        var bySecurity = prices.Where(p => p.FirstKnownAt <= cutoff).GroupBy(p => p.SecurityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new PriceHistory(g.OrderBy(p => p.CloseAt).ToArray()), StringComparer.OrdinalIgnoreCase);
        if (!bySecurity.TryGetValue(benchmarkSecurityId, out var benchmark) || benchmark.Terminal is not null)
            throw new ArgumentException("A non-terminal benchmark series known by the return-data cutoff is required.", nameof(prices));
        var master = referenceData is null ? null : new SecurityMaster(referenceData.Securities);
        decimal capital = initialCapital, benchmarkGrowth = 1m, peak = capital, drawdown = 0;
        var periods = new List<LongTermPeriodResult>(decisionTimes.Count - 1);
        for (int i = 0; i < decisionTimes.Count - 1; i++)
        {
            DateTimeOffset decision = decisionTimes[i], nextDecision = decisionTimes[i + 1];
            var entryBenchmark = benchmark.FirstAfter(decision) ?? throw new InvalidDataException("Missing benchmark entry close.");
            var exitBenchmark = benchmark.FirstAfter(nextDecision) ?? throw new InvalidDataException("Missing benchmark exit close.");
            if (entryBenchmark.CloseAt >= exitBenchmark.CloseAt)
                throw new InvalidDataException("Each rebalance period needs distinct entry and exit closes; sparse prices cannot define this schedule.");
            EnsureVersion(entryBenchmark, exitBenchmark, benchmarkSecurityId);
            decimal benchmarkReturn = exitBenchmark.AdjustedTotalReturnClose / entryBenchmark.AdjustedTotalReturnClose - 1m;
            benchmarkGrowth *= 1m + benchmarkReturn;

            var selected = capital == 0 ? [] : _ranker.Rank(metrics, decision)
                .Where(s => master is null || master.GetAt(s.SecurityId, decision) is { Segment: var segment } &&
                    segment.Equals("CASH_EQUITY", StringComparison.OrdinalIgnoreCase)).Take(maxHoldings).ToArray();
            decimal openingCapital = capital, grossFactor = selected.Length == 0 ? 1m : 0m;
            decimal invested = selected.Length == 0 ? 0m : capital / (1m + entryCostBps / 10_000m);
            decimal entryFees = selected.Length == 0 ? 0m : capital - invested, proceeds = 0m, exitFees = 0m;
            var terminalIds = new List<string>();
            foreach (var score in selected)
            {
                if (!bySecurity.TryGetValue(score.SecurityId, out var series))
                    throw new InvalidDataException($"No known total-return prices for {score.SecurityId}.");
                var entry = series.At(entryBenchmark.CloseAt)
                    ?? throw new InvalidDataException($"Missing entry close for {score.SecurityId}.");
                if (entry.IsTerminal || entry.AdjustedTotalReturnClose <= 0 || series.Terminal?.CloseAt <= entry.CloseAt)
                    throw new InvalidDataException($"Cannot buy terminal security {score.SecurityId}.");
                var exit = series.Terminal is { } terminal && terminal.CloseAt <= exitBenchmark.CloseAt
                    ? terminal : series.At(exitBenchmark.CloseAt)
                    ?? throw new InvalidDataException($"Missing exit/delisting value for {score.SecurityId}.");
                EnsureVersion(entry, exit, score.SecurityId);
                decimal factor = exit.AdjustedTotalReturnClose / entry.AdjustedTotalReturnClose;
                decimal holdingProceeds = invested / selected.Length * factor;
                grossFactor += factor / selected.Length;
                proceeds += holdingProceeds;
                if (exit.IsTerminal) terminalIds.Add(score.SecurityId);
                else if (holdingProceeds > 0) exitFees += holdingProceeds * exitCostBps / 10_000m + fixedSellChargePerHolding;
            }
            decimal unpaid = Math.Max(0m, exitFees - proceeds);
            if (selected.Length > 0) capital = Math.Max(0m, proceeds - exitFees);
            decimal net = openingCapital > 0 ? capital / openingCapital - 1m : 0m;
            decimal gross = grossFactor - 1m;
            peak = Math.Max(peak, capital);
            drawdown = Math.Max(drawdown, (peak - capital) / peak * 100m);
            periods.Add(new LongTermPeriodResult(decision, nextDecision, entryBenchmark.CloseAt,
                exitBenchmark.CloseAt, selected.Select(s => s.SecurityId).ToArray(),
                selected.SelectMany(s => s.EvidenceFactIds).Distinct().Order(StringComparer.Ordinal).ToArray(),
                gross * 100m, (gross - net) * 100m, net * 100m, benchmarkReturn * 100m,
                openingCapital, capital, entryFees + Math.Min(exitFees, proceeds), unpaid, terminalIds));
        }
        var fund = investableBenchmark is null ? null : new InvestableBenchmarkEvaluator().Evaluate(investableBenchmark,
            prices, periods[0].EntryCloseAt, periods[^1].ExitCloseAt, initialCapital, cutoff);
        return new LongTermEvaluationReport(LongTermRanker.Version, benchmarkSecurityId, initialCapital,
            capital, (capital / initialCapital - 1m) * 100m, (benchmarkGrowth - 1m) * 100m,
            drawdown, periods,
            "Verified metrics known by each decision; next supplied close execution; equal-value fractional research holdings with complete liquidation at each rebalance. Entry fees reduce invested cash, exit fees use sale proceeds. Explicit terminal values are carried as cash without broker sale fees. CostPercent measures return drag, while TradingCostsRupees reports cash fees. Drawdown is measured only at rebalances. TRI is not an investable after-fee fund return. Depletion stays at zero, with unfunded exit charges disclosed; these are assumptions, not executable account orders.",
            referenceData is not null, cutoff, capital == 0m, fund);
    }

    private static void EnsureVersion(TotalReturnPrice entry, TotalReturnPrice exit, string id)
    {
        if (entry.AdjustmentVersion != exit.AdjustmentVersion)
            throw new InvalidDataException($"Adjustment versions differ for {id}.");
    }

    private sealed class PriceHistory
    {
        private readonly TotalReturnPrice[] _prices;
        private readonly Dictionary<DateTimeOffset, TotalReturnPrice> _byClose;
        public TotalReturnPrice? Terminal { get; }
        public PriceHistory(TotalReturnPrice[] prices)
        {
            _prices = prices;
            if (prices.Select(p => p.CloseAt).Distinct().Count() != prices.Length)
                throw new ArgumentException("Duplicate security/close timestamp (case-insensitive security ID).");
            Terminal = prices.FirstOrDefault(p => p.IsTerminal);
            if (Terminal is not null && (prices.Count(p => p.IsTerminal) != 1 || prices[^1] != Terminal))
                throw new InvalidDataException("A terminal security cannot have later price observations or multiple terminal values.");
            _byClose = prices.ToDictionary(p => p.CloseAt);
        }
        public TotalReturnPrice? At(DateTimeOffset closeAt) => _byClose.GetValueOrDefault(closeAt);
        public TotalReturnPrice? FirstAfter(DateTimeOffset at)
        {
            int lo = 0, hi = _prices.Length;
            while (lo < hi)
            {
                int middle = lo + (hi - lo) / 2;
                if (_prices[middle].CloseAt <= at) lo = middle + 1;
                else hi = middle;
            }
            return lo < _prices.Length ? _prices[lo] : null;
        }
    }
}
