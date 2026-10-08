namespace TradeTest.Domain;

public enum OperatingMode { Off, Research, Paper, Shadow, Live }
public enum OrderSide { Buy, Sell }
public enum OrderState { Pending, Accepted, Filled, PartiallyFilled, Rejected, Cancelled }

public sealed record TradeCandidate(
    string CandidateId,
    string SecurityId,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt,
    decimal Entry,
    decimal Stop,
    decimal Target,
    decimal VolumeRatio,
    decimal Vwap,
    string StrategyVersion);

public sealed record RiskPolicy(
    decimal MaxPositionRupees,
    decimal MaxRiskPerTradeRupees,
    decimal MaxDailyLossRupees,
    int MaxTradesPerSession,
    decimal MinimumRewardRisk,
    decimal MaxSpreadBps,
    decimal MaxEntryDeviationBps,
    TimeSpan MaxQuoteAge,
    string Version);

public sealed record RiskContext(
    OperatingMode Mode,
    DateTimeOffset Now,
    DateTimeOffset SessionEndsAt,
    MarketQuote Quote,
    decimal AvailableCash,
    decimal RealizedDailyPnl,
    bool HasOpenPosition,
    int TradesThisSession,
    bool IsAllowedSecurity,
    bool BrokerHealthy,
    bool StateHealthy,
    bool KillSwitchActive);

public sealed record RiskDecision(bool Approved, int Quantity, IReadOnlyList<string> Reasons, string PolicyVersion);

public sealed record OrderIntent(
    string ReferenceId,
    string CandidateId,
    string SecurityId,
    OrderSide Side,
    int Quantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    decimal LimitPrice,
    decimal StopPrice,
    decimal TargetPrice,
    string StrategyVersion,
    string RiskPolicyVersion);

public sealed record SimulatedFill(
    string ReferenceId,
    string SecurityId,
    OrderSide Side,
    int Quantity,
    decimal Price,
    DateTimeOffset FilledAt);

public sealed record RoundTripCosts(
    decimal Brokerage,
    decimal Stt,
    decimal StampDuty,
    decimal ExchangeCharge,
    decimal SebiCharge,
    decimal Gst,
    decimal SpreadAndSlippage)
{
    public decimal Total => Brokerage + Stt + StampDuty + ExchangeCharge + SebiCharge + Gst + SpreadAndSlippage;
}

public sealed record CompletedTrade(
    string CandidateId,
    string SecurityId,
    int Quantity,
    DateTimeOffset EnteredAt,
    DateTimeOffset ExitedAt,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal GrossPnl,
    RoundTripCosts Costs,
    string ExitReason)
{
    public decimal NetPnl => GrossPnl - Costs.Total;
}
