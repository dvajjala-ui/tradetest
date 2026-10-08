using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Tests;

public sealed class EvaluationTests
{
    [Fact]
    public void Intraday_evaluation_keeps_no_trade_days_and_reports_after_cost_results()
    {
        var first = TradingTests.Bars();
        var second = first.Select(b => b with
        {
            StartsAt = b.StartsAt.AddDays(1), Open = 100m, High = 100.1m,
            Low = 99.9m, Close = 100m, Volume = 1000m
        }).ToArray();
        var policy = new RiskPolicy(5000m, 50m, 100m, 1, 1.5m, 30m, 5m,
            TimeSpan.FromSeconds(30), "test-v1");
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0), policy);
        var report = new IntradayEvaluator().Evaluate([first, second], config);
        Assert.Equal(2, report.Sessions);
        Assert.Equal(1, report.NoTradeSessions);
        Assert.Equal(1, report.Trades);
        Assert.True(report.GrossPnl > report.NetPnl);
        Assert.Null(report.ExploratoryBootstrapMeanLower95); // One trade cannot establish a confidence interval.
    }

    [Fact]
    public void Intraday_study_preserves_holdout_dates_and_replays_higher_costs()
    {
        var first = TradingTests.Bars();
        MarketBar[] OnDay(int offset) => first.Select(b => b with { StartsAt = b.StartsAt.AddDays(offset) }).ToArray();
        var policy = new RiskPolicy(5000m, 50m, 100m, 1, 1.5m, 30m, 5m,
            TimeSpan.FromSeconds(30), "frozen-test-v1");
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0), policy);
        var plan = new IntradayStudyPlan(new DateOnly(2026, 1, 6), new DateOnly(2026, 1, 7),
            new DateOnly(2026, 1, 8), 1.1m);
        var study = new IntradayStudyEvaluator();
        var report = study.Evaluate([OnDay(0), OnDay(1), OnDay(2)], config, plan);
        Assert.Equal(1, report.Training.Sessions);
        Assert.Equal(1, report.Validation.Sessions);
        Assert.Equal(1, report.Holdout.Sessions);
        Assert.Equal(1, report.Holdout.Trades);
        Assert.True(report.StressedHoldout.NetPnl < report.Holdout.NetPnl);
        Assert.Throws<ArgumentException>(() => study.Evaluate([OnDay(0), OnDay(1), OnDay(1)], config, plan));
        Assert.Throws<ArgumentException>(() => study.Evaluate([OnDay(0), OnDay(1), OnDay(3)], config, plan));
    }

    [Fact]
    public void Walk_forward_uses_disjoint_evaluation_dates_and_a_separate_final_holdout()
    {
        var bars = TradingTests.Bars();
        IReadOnlyList<MarketBar>[] sessions = new[] { 0, 1, 2, 3, 4, 7 }.Select(offset =>
            (IReadOnlyList<MarketBar>)bars.Select(b => b with { StartsAt = b.StartsAt.AddDays(offset) }).ToArray()).ToArray();
        var policy = new RiskPolicy(5000m, 50m, 100m, 1, 1.5m, 30m, 5m, TimeSpan.FromSeconds(30), "frozen-test-v1");
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0), policy);
        var plan = new WalkForwardPlan(1, 1, 1, new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 13), 1.1m);
        var evaluator = new WalkForwardEvaluator();
        var report = evaluator.Evaluate(sessions, config, plan);
        Assert.Equal([1, 2, 3], report.Folds.Select(f => f.Training.Sessions));
        Assert.Equal(3, report.CombinedOutOfSample.Sessions);
        Assert.Equal(1, report.FinalHoldout.Sessions);
        Assert.All(report.Folds, f => Assert.True(f.EvaluationEndExclusive <= plan.FinalHoldoutStartsAt));
        Assert.True(report.StressedFinalHoldout.NetPnl < report.FinalHoldout.NetPnl);
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(sessions[..^1], config, plan));
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate([.. sessions, sessions[0]], config, plan));
    }

    [Fact]
    public void Long_term_evaluation_does_not_select_future_information_and_requires_exit_price()
    {
        var decision = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var next = decision.AddMonths(3);
        var entry = decision.AddDays(1).AddHours(10);
        var exit = next.AddDays(1).AddHours(10);
        CompanyMetric[] Metrics(string id, DateTimeOffset knownAt, decimal growth) =>
        [
            new(id, CompanyMetricKind.RevenueGrowth3YPercent, growth, knownAt, id + "-1", VerificationState.Verified),
            new(id, CompanyMetricKind.ReturnOnCapitalPercent, 20m, knownAt, id + "-2", VerificationState.Verified),
            new(id, CompanyMetricKind.FreeCashFlowMarginPercent, 8m, knownAt, id + "-3", VerificationState.Verified),
            new(id, CompanyMetricKind.NetDebtToEbitda, 0.4m, knownAt, id + "-4", VerificationState.Verified),
            new(id, CompanyMetricKind.ShareDilution3YPercent, 1m, knownAt, id + "-5", VerificationState.Verified),
            new(id, CompanyMetricKind.Momentum12MPercent, 15m, knownAt, id + "-6", VerificationState.Verified),
            new(id, CompanyMetricKind.AverageDailyTurnoverRupees, 12_000_000m, knownAt, id + "-7", VerificationState.Verified)
        ];
        var metrics = Metrics("SYNTH", decision.AddDays(-1), 18m)
            .Concat(Metrics("FUTURE", decision.AddDays(1), 40m)).ToArray();
        TotalReturnPrice Price(string id, DateTimeOffset at, decimal value) =>
            new(id, at, at.AddHours(1), value, "synthetic-v1");
        var prices = new[]
        {
            Price("TRI", entry, 100m), Price("TRI", exit, 105m),
            Price("SYNTH", entry, 100m), Price("SYNTH", exit, 120m),
            Price("FUTURE", entry, 100m), Price("FUTURE", exit, 200m)
        };
        var evaluator = new LongTermEvaluator();
        var report = evaluator.Evaluate([decision, next], metrics, prices, "TRI", 5000m, 3, 10m, 10m, 20m);
        Assert.Equal(["SYNTH"], report.Periods[0].Securities);
        Assert.Equal(19.4m, report.TotalNetReturnPercent);
        Assert.Equal(5m, report.BenchmarkTriReturnPercent);
        Assert.Throws<InvalidDataException>(() => evaluator.Evaluate([decision, next], metrics,
            prices.Where(p => p.SecurityId != "SYNTH" || p.CloseAt != exit).ToArray(),
            "TRI", 5000m, 3, 10m, 10m, 20m));
    }
}
