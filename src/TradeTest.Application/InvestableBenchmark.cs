using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Caller-supplied fund/ETF total-return series. Fund expenses and distributions must already be reflected in its levels.</summary>
public sealed record InvestableBenchmarkInput(string SecurityId, string Name, bool SeriesNetOfFundExpensesAndIncludesDistributions,
    decimal EntryCostBps, decimal ExitCostBps, decimal FixedExitCostRupees,
    Uri SourceUrl, string LicenceId, VerificationState Verification);

public sealed record InvestableBenchmarkReport(string SecurityId, string Name, DateTimeOffset EntryCloseAt,
    DateTimeOffset ExitCloseAt, int Observations, decimal InitialCapital, decimal FinalCapital,
    decimal NetReturnPercent, decimal TradingCostsRupees, decimal UnfundedExitCostsRupees,
    decimal MaximumObservedDrawdownPercent, decimal? AnnualizedNetReturnPercent,
    Uri SourceUrl, string LicenceId, string EvidenceNote);

public sealed class InvestableBenchmarkEvaluator
{
    public InvestableBenchmarkReport Evaluate(InvestableBenchmarkInput input, IReadOnlyList<TotalReturnPrice> supplied,
        DateTimeOffset entryAt, DateTimeOffset exitAt, decimal initialCapital, DateTimeOffset dataAsOf)
    {
        if (!input.SeriesNetOfFundExpensesAndIncludesDistributions || initialCapital <= 0 || input.EntryCostBps < 0 ||
            input.ExitCostBps is < 0 or >= 10_000 || input.FixedExitCostRupees < 0 || entryAt >= exitAt ||
            string.IsNullOrWhiteSpace(input.SecurityId) || string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Provide a net-expense total-return fund series and valid benchmark costs/times.");
        if (!input.SourceUrl.IsAbsoluteUri || input.SourceUrl.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(input.LicenceId) || input.Verification != VerificationState.Verified)
            throw new ArgumentException("Investable benchmark needs verified source and licence metadata.");
        var prices = supplied.Where(p => p.FirstKnownAt <= dataAsOf && p.SecurityId.Equals(input.SecurityId, StringComparison.OrdinalIgnoreCase)
            && p.CloseAt >= entryAt && p.CloseAt <= exitAt).OrderBy(p => p.CloseAt).ToArray();
        if (prices.Length < 2 || prices[0].CloseAt != entryAt || prices[^1].CloseAt != exitAt ||
            prices.Select(p => p.CloseAt).Distinct().Count() != prices.Length)
            throw new InvalidDataException("Investable benchmark needs unique observations on the same entry and exit timestamps.");
        if (prices.Any(p => p.IsTerminal || p.AdjustedTotalReturnClose <= 0 || p.FirstKnownAt < p.CloseAt ||
            p.AdjustmentVersion != prices[0].AdjustmentVersion))
            throw new InvalidDataException("Invalid investable benchmark values, timing or adjustment versions.");
        decimal invested = initialCapital / (1m + input.EntryCostBps / 10_000m);
        decimal entryFees = initialCapital - invested, peak = initialCapital, drawdown = 0m;
        foreach (var price in prices)
        {
            decimal equity = invested * price.AdjustedTotalReturnClose / prices[0].AdjustedTotalReturnClose;
            peak = Math.Max(peak, equity);
            drawdown = Math.Max(drawdown, (peak - equity) / peak * 100m);
        }
        decimal proceeds = invested * prices[^1].AdjustedTotalReturnClose / prices[0].AdjustedTotalReturnClose;
        decimal exitFees = proceeds * input.ExitCostBps / 10_000m + input.FixedExitCostRupees;
        decimal final = Math.Max(0m, proceeds - exitFees);
        drawdown = Math.Max(drawdown, (peak - final) / peak * 100m);
        decimal? annualized = (exitAt - entryAt).TotalDays >= 365.2425
            ? (decimal)((Math.Pow((double)(final / initialCapital), 365.2425 / (exitAt - entryAt).TotalDays) - 1d) * 100d) : null;
        return new(input.SecurityId, input.Name, entryAt, exitAt, prices.Length, initialCapital, final,
            (final / initialCapital - 1m) * 100m, entryFees + Math.Min(exitFees, proceeds), Math.Max(0m, exitFees - proceeds),
            drawdown, annualized, input.SourceUrl, input.LicenceId,
            "Passive buy-and-hold on supplied total-return levels net of fund expenses, with separately funded entry and exit costs. No second expense-ratio subtraction. Fractional research units; no tax or execution-capacity model. Drawdown uses supplied observations and can miss intraperiod losses. Annualization is omitted for periods shorter than one year. Verified source metadata is supplied by the caller and requires independent reconciliation.");
    }
}
