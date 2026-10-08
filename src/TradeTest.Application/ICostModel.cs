using TradeTest.Domain;

namespace TradeTest.Application;

public interface ICostModel
{
    RoundTripCosts Calculate(decimal buyNotional, decimal sellNotional, decimal spreadAndSlippage = 0m);
}
