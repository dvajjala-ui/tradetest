using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Published Groww NSE intraday equity rates checked 2026-10-08; verify against contract notes.</summary>
public sealed class GrowwIntradayCostModel
{
    public const string Version = "groww-nse-cash-intraday-2026-10-08";

    public RoundTripCosts Calculate(decimal buyNotional, decimal sellNotional, decimal spreadAndSlippage = 0m)
    {
        if (buyNotional <= 0 || sellNotional <= 0 || spreadAndSlippage < 0)
            throw new ArgumentOutOfRangeException(nameof(buyNotional), "Notionals must be positive and extra costs nonnegative.");
        static decimal Brokerage(decimal notional) => Math.Max(5m, Math.Min(20m, notional * 0.001m));
        decimal brokerage = Brokerage(buyNotional) + Brokerage(sellNotional);
        decimal stt = sellNotional * 0.00025m;
        decimal stamp = buyNotional * 0.00003m;
        decimal exchange = (buyNotional + sellNotional) * 0.0000297m;
        decimal sebi = (buyNotional + sellNotional) * 0.000001m;
        decimal gst = (brokerage + exchange + sebi) * 0.18m;
        return new RoundTripCosts(brokerage, stt, stamp, exchange, sebi, gst, spreadAndSlippage);
    }
}
