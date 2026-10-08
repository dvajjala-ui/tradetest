using TradeTest.Domain;

namespace TradeTest.Application;

public sealed record IntradayStudyPlan(
    DateOnly TrainingEndExclusive,
    DateOnly ValidationEndExclusive,
    DateOnly HoldoutEndExclusive,
    decimal CostStressMultiplier);

public sealed record IntradayStudyReport(
    string StrategyVersion,
    string CostModelVersion,
    IntradayStudyPlan Plan,
    SimulationConfig FrozenConfig,
    IntradayEvaluationReport Training,
    IntradayEvaluationReport Validation,
    IntradayEvaluationReport Holdout,
    IntradayEvaluationReport StressedHoldout,
    decimal NoTradeBaselineReturnPercent,
    string EvidenceNote);

/// <summary>Runs a fixed hypothesis on chronological partitions; it does not tune parameters.</summary>
public sealed class IntradayStudyEvaluator
{
    private readonly TimeZoneInfo _india = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public IntradayStudyReport Evaluate(
        IReadOnlyList<IReadOnlyList<MarketBar>> sessions, SimulationConfig config, IntradayStudyPlan plan)
    {
        if (plan.TrainingEndExclusive >= plan.ValidationEndExclusive ||
            plan.ValidationEndExclusive >= plan.HoldoutEndExclusive ||
            plan.CostStressMultiplier < 1m)
            throw new ArgumentException("Study boundaries must increase and cost stress must be at least one.", nameof(plan));
        if (sessions.Count == 0 || sessions.Any(s => s.Count == 0))
            throw new ArgumentException("Nonempty sessions are required.", nameof(sessions));

        var dated = sessions.Select(s => (Date: DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(s[0].StartsAt, _india).Date), Bars: s)).ToArray();
        if (dated.Select(x => x.Date).Distinct().Count() != dated.Length ||
            dated.Any(x => x.Date >= plan.HoldoutEndExclusive))
            throw new ArgumentException("Sessions must have distinct dates inside the declared study window.", nameof(sessions));

        IReadOnlyList<MarketBar>[] train = dated.Where(x => x.Date < plan.TrainingEndExclusive)
            .Select(x => x.Bars).ToArray();
        IReadOnlyList<MarketBar>[] validation = dated.Where(x => x.Date >= plan.TrainingEndExclusive &&
            x.Date < plan.ValidationEndExclusive).Select(x => x.Bars).ToArray();
        IReadOnlyList<MarketBar>[] holdout = dated.Where(x => x.Date >= plan.ValidationEndExclusive)
            .Select(x => x.Bars).ToArray();
        if (train.Length == 0 || validation.Length == 0 || holdout.Length == 0)
            throw new ArgumentException("Training, validation, and holdout each need at least one session.", nameof(sessions));

        var evaluator = new IntradayEvaluator();
        var stressed = config with
        {
            SpreadBps = config.SpreadBps * plan.CostStressMultiplier,
            SlippageBps = config.SlippageBps * plan.CostStressMultiplier
        };
        return new IntradayStudyReport(OpeningRangeStrategy.Version, GrowwIntradayCostModel.Version,
            plan, config, evaluator.Evaluate(train, config), evaluator.Evaluate(validation, config),
            evaluator.Evaluate(holdout, config), evaluator.Evaluate(holdout, stressed), 0m,
            "Chronological partitions use one frozen strategy and risk config. Stress widens simulated spread and slippage only. Synthetic or small samples cannot establish an edge; keep the final holdout untouched during development.");
    }
}
