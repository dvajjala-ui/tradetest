using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Tests;

public sealed class InvestableBenchmarkTests
{
    [Fact]
    public void Passive_fund_uses_net_expense_levels_and_includes_intermediate_observed_drawdown()
    {
        DateTimeOffset start = new(2026, 1, 6, 10, 0, 0, TimeSpan.Zero), end = start.AddYears(2);
        var config = new InvestableBenchmarkInput("FUND", "Synthetic fund", true, 10m, 10m, 20m,
            new Uri("https://example.com/fund"), "SYNTHETIC_ONLY", VerificationState.Verified);
        TotalReturnPrice Point(DateTimeOffset at, decimal value) => new("FUND", at, at, value, "fund-v1");
        var prices = new[] { Point(start, 100m), Point(start.AddMonths(3), 80m), Point(end, 120m) };
        var report = new InvestableBenchmarkEvaluator().Evaluate(config, prices, start, end, 5000m, end);
        decimal invested = 5000m / 1.001m;
        decimal final = invested * 1.2m * 0.999m - 20m;
        Assert.Equal(final, report.FinalCapital);
        Assert.True(report.MaximumObservedDrawdownPercent >= 20m);
        Assert.NotNull(report.AnnualizedNetReturnPercent);
        Assert.Throws<ArgumentException>(() => new InvestableBenchmarkEvaluator().Evaluate(config with
        {
            SeriesNetOfFundExpensesAndIncludesDistributions = false
        }, prices, start, end, 5000m, end));
        Assert.Throws<InvalidDataException>(() => new InvestableBenchmarkEvaluator().Evaluate(config, prices, start, end, 5000m, end.AddDays(-1)));
    }

    [Fact]
    public void Short_periods_are_not_annualized_and_duplicate_or_missing_endpoint_data_is_rejected()
    {
        var start = DateTimeOffset.Parse("2026-01-06T10:00:00Z");
        var end = start.AddMonths(3);
        var config = new InvestableBenchmarkInput("FUND", "Synthetic fund", true, 0m, 0m, 0m,
            new Uri("https://example.com/fund"), "SYNTHETIC_ONLY", VerificationState.Verified);
        TotalReturnPrice[] prices = [new("FUND", start, start, 100m, "v1"), new("FUND", end, end, 105m, "v1")];
        var evaluator = new InvestableBenchmarkEvaluator();
        var report = evaluator.Evaluate(config, prices, start, end, 5000m, end);
        Assert.Null(report.AnnualizedNetReturnPercent);
        Assert.Equal(5m, report.NetReturnPercent);
        Assert.Throws<InvalidDataException>(() => evaluator.Evaluate(config, [.. prices, prices[0]], start, end, 5000m, end));
        Assert.Throws<InvalidDataException>(() => evaluator.Evaluate(config, prices[..1], start, end, 5000m, end));
    }
}
