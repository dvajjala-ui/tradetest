using TradeTest.Domain;

namespace TradeTest.Application;

public sealed class RiskEngine
{
    public RiskDecision Evaluate(TradeCandidate candidate, RiskPolicy policy, RiskContext context)
    {
        var reasons = new List<string>();
        if (context.Mode is not (OperatingMode.Paper or OperatingMode.Shadow or OperatingMode.Live)) reasons.Add("MODE_DISABLED");
        if (context.KillSwitchActive) reasons.Add("KILL_SWITCH");
        if (!context.StateHealthy) reasons.Add("STATE_UNHEALTHY");
        if (!context.BrokerHealthy) reasons.Add("BROKER_UNHEALTHY");
        if (!context.IsAllowedSecurity || candidate.SecurityId != context.Quote.SecurityId) reasons.Add("SECURITY_NOT_ALLOWED");
        if (context.HasOpenPosition) reasons.Add("POSITION_ALREADY_OPEN");
        if (context.TradesThisSession >= policy.MaxTradesPerSession) reasons.Add("TRADE_LIMIT");
        if (context.RealizedDailyPnl <= -policy.MaxDailyLossRupees) reasons.Add("DAILY_LOSS_LIMIT");
        if (context.Now < candidate.GeneratedAt || context.Now > candidate.ExpiresAt || context.Now >= context.SessionEndsAt)
            reasons.Add("CANDIDATE_EXPIRED");
        if (context.Quote.ObservedAt > context.Now || context.Now - context.Quote.ObservedAt > policy.MaxQuoteAge)
            reasons.Add("STALE_QUOTE");
        if (context.Quote.Bid <= 0 || context.Quote.Ask < context.Quote.Bid || context.Quote.SpreadBps > policy.MaxSpreadBps)
            reasons.Add("SPREAD_TOO_WIDE");
        if (candidate.Entry <= 0 || candidate.Stop <= 0 || candidate.Entry <= candidate.Stop || candidate.Target <= candidate.Entry)
            reasons.Add("INVALID_LEVELS");
        if (policy.MaxPositionRupees <= 0 || policy.MaxRiskPerTradeRupees <= 0 || policy.MaxDailyLossRupees <= 0 ||
            policy.MaxTradesPerSession <= 0 || policy.MinimumRewardRisk <= 0 || policy.MaxSpreadBps < 0 ||
            policy.MaxEntryDeviationBps < 0 || context.AvailableCash <= 0)
            reasons.Add("INVALID_POLICY_OR_CASH");

        int quantity = 0;
        if (!reasons.Contains("INVALID_LEVELS") && !reasons.Contains("INVALID_POLICY_OR_CASH"))
        {
            // A fill may be above the signal price, up to the order's limit price.
            decimal limitPrice = candidate.Entry * (1m + policy.MaxEntryDeviationBps / 10_000m);
            if (context.Quote.Ask > limitPrice) reasons.Add("ENTRY_PRICE_MOVED");
            decimal riskPerShare = limitPrice - candidate.Stop;
            decimal rewardRisk = (candidate.Target - limitPrice) / riskPerShare;
            if (rewardRisk < policy.MinimumRewardRisk) reasons.Add("REWARD_RISK_TOO_LOW");
            decimal affordable = Math.Min(context.AvailableCash, policy.MaxPositionRupees) / limitPrice;
            decimal riskSized = policy.MaxRiskPerTradeRupees / riskPerShare;
            decimal remainingDailyRisk = (policy.MaxDailyLossRupees + context.RealizedDailyPnl) / riskPerShare;
            quantity = (int)Math.Min(int.MaxValue,
                decimal.Floor(Math.Min(affordable, Math.Min(riskSized, remainingDailyRisk))));
            if (quantity <= 0) reasons.Add("INSUFFICIENT_CASH_OR_RISK_BUDGET");
        }
        if (reasons.Count > 0) quantity = 0;
        return new RiskDecision(reasons.Count == 0, quantity, reasons, policy.Version);
    }
}
