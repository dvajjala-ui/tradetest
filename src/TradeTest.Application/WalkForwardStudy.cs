using TradeTest.Domain;

namespace TradeTest.Application;

public sealed record WalkForwardPlan(int MinimumTrainingSessions, int ValidationSessions, int EvaluationSessions,
    DateOnly FinalHoldoutStartsAt, DateOnly FinalHoldoutEndExclusive, decimal CostStressMultiplier);

public sealed record WalkForwardFold(int Number, DateOnly TrainingStartsAt, DateOnly ValidationStartsAt,
    DateOnly EvaluationStartsAt, DateOnly EvaluationEndExclusive, IntradayEvaluationReport Training,
    IntradayEvaluationReport Validation, IntradayEvaluationReport Evaluation);

public sealed record WalkForwardReport(string StrategyVersion, string CostModelVersion, WalkForwardPlan Plan,
    SimulationConfig FrozenConfig, IReadOnlyList<WalkForwardFold> Folds,
    IntradayEvaluationReport CombinedOutOfSample, IntradayEvaluationReport FinalHoldout,
    IntradayEvaluationReport StressedFinalHoldout, string EvidenceNote);

/// <summary>Expanding training windows and disjoint evaluation windows for a fixed rules-only hypothesis.</summary>
public sealed class WalkForwardEvaluator
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public WalkForwardReport Evaluate(IReadOnlyList<IReadOnlyList<MarketBar>> sessions, SimulationConfig config, WalkForwardPlan plan)
    {
        if (plan.MinimumTrainingSessions < 1 || plan.ValidationSessions < 1 || plan.EvaluationSessions < 1 ||
            plan.FinalHoldoutStartsAt >= plan.FinalHoldoutEndExclusive || plan.CostStressMultiplier < 1m)
            throw new ArgumentException("Invalid walk-forward plan.", nameof(plan));
        if (sessions.Count == 0 || sessions.Any(s => s.Count == 0)) throw new ArgumentException("Nonempty sessions are required.");
        var dated = sessions.Select(s => (Date: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s[0].StartsAt, India).Date), Bars: s))
            .OrderBy(s => s.Date).ToArray();
        if (dated.Select(s => s.Date).Distinct().Count() != dated.Length || dated.Any(s => s.Date >= plan.FinalHoldoutEndExclusive))
            throw new ArgumentException("Study dates must be unique and inside the declared window.", nameof(sessions));
        int historyCount = dated.TakeWhile(s => s.Date < plan.FinalHoldoutStartsAt).Count();
        if (historyCount < (long)plan.MinimumTrainingSessions + plan.ValidationSessions + 1 || historyCount == dated.Length)
            throw new ArgumentException("Research data needs training, validation, evaluation, and a separate final holdout.");

        // The hypothesis does not fit parameters, so replay each input once and reuse immutable reports across folds.
        var engine = new ReplayEngine();
        var reference = config.ReferenceData is null ? null : new MarketReferenceValidator(config.ReferenceData);
        var reports = dated.Select(s => engine.RunCompiled(s.Bars, config, captureEvents: false, referenceValidator: reference)).ToArray();
        var folds = new List<WalkForwardFold>();
        int firstEvaluation = plan.MinimumTrainingSessions + plan.ValidationSessions;
        for (int start = firstEvaluation; start < historyCount;)
        {
            int end = start + Math.Min(plan.EvaluationSessions, historyCount - start);
            int validationStart = start - plan.ValidationSessions;
            folds.Add(new WalkForwardFold(folds.Count + 1, dated[0].Date, dated[validationStart].Date,
                dated[start].Date, dated[end - 1].Date.AddDays(1),
                IntradayEvaluator.Summarize(new ArraySegment<SimulationReport>(reports, 0, validationStart), config.InitialCash, includeBootstrap: false),
                IntradayEvaluator.Summarize(new ArraySegment<SimulationReport>(reports, validationStart, plan.ValidationSessions), config.InitialCash, includeBootstrap: false),
                IntradayEvaluator.Summarize(new ArraySegment<SimulationReport>(reports, start, end - start), config.InitialCash)));
            start = end;
        }
        var holdout = dated.Skip(historyCount).Select(s => s.Bars).ToArray();
        var stressed = config with { SpreadBps = config.SpreadBps * plan.CostStressMultiplier, SlippageBps = config.SlippageBps * plan.CostStressMultiplier };
        return new WalkForwardReport(OpeningRangeStrategy.Version, GrowwIntradayCostModel.Version, plan, config, folds,
            IntradayEvaluator.Summarize(new ArraySegment<SimulationReport>(reports, firstEvaluation, historyCount - firstEvaluation), config.InitialCash),
            IntradayEvaluator.Summarize(new ArraySegment<SimulationReport>(reports, historyCount, reports.Length - historyCount), config.InitialCash),
            new IntradayEvaluator().Evaluate(holdout, stressed),
            "One frozen rules-only strategy; no fitting or parameter search. Evaluation windows do not overlap and exclude the final holdout. Training/validation intervals are omitted; cost stress widens spread/slippage. Reusing reports is valid only for this fixed hypothesis. Synthetic samples do not establish profitability.");
    }
}
