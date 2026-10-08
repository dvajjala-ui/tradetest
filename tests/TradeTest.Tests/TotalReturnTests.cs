using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Tests;

public sealed class TotalReturnTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 5, 15, 30, 0, TimeSpan.FromHours(5.5));
    private static readonly Uri Source = new("https://example.com/synthetic-actions");
    private static RawCloseObservation Close(int day, decimal price) => new("close-" + day, "SYNTH", Start.AddDays(day),
        Start.AddDays(day).AddMinutes(1), price, Source, "SYNTHETIC_ONLY", VerificationState.Verified);
    private static CorporateAction Action(string id, int day, CorporateActionKind kind, decimal shares, decimal cash, int sequence = 0) =>
        new(id, "SYNTH", kind, Start.AddDays(day).AddHours(-6), Start.AddDays(-1), sequence,
            shares, cash, Source, "SYNTHETIC_ONLY", VerificationState.Verified);
    private static TotalReturnBuildInput Input(RawCloseObservation[] closes, CorporateAction[] actions, int lastDay = 3) =>
        new("SYNTH", closes, actions, new("SYNTH", Start.AddDays(-1), Start.AddDays(lastDay), Start.AddDays(lastDay).AddMinutes(2),
            Source, "SYNTHETIC_ONLY", VerificationState.Verified), Start.AddDays(10));

    [Fact]
    public void Split_bonus_and_dividend_preserve_wealth_and_the_dividend_is_reinvested()
    {
        var input = Input([Close(0, 100m), Close(1, 50m), Close(2, 25m), Close(3, 24m), Close(4, 48m)],
            [Action("split", 1, CorporateActionKind.Split, 2m, 0m), Action("bonus", 2, CorporateActionKind.Bonus, 2m, 0m),
                Action("dividend", 3, CorporateActionKind.CashDividend, 1m, 1m)], lastDay: 4);
        var report = new TotalReturnBuilder().Build(input);
        Assert.Equal([100m, 100m, 100m, 100m, 200m], report.Prices.Select(p => p.AdjustedTotalReturnClose));
        Assert.Contains("dividend", report.Prices[3].SourceEvidenceIds!);
        Assert.All(report.Prices, p => Assert.True(p.FirstKnownAt >= input.Coverage.FirstKnownAt));
    }

    [Fact]
    public void Same_day_sequence_defines_whether_dividend_terms_are_before_or_after_a_split()
    {
        var input = Input([Close(0, 100m), Close(1, 49m)],
            [Action("split", 1, CorporateActionKind.Split, 2m, 0m, 0), Action("dividend", 1, CorporateActionKind.CashDividend, 1m, 1m, 1)]);
        Assert.Equal(100m, new TotalReturnBuilder().Build(input).Prices[^1].AdjustedTotalReturnClose);
        Assert.Throws<InvalidDataException>(() => new TotalReturnBuilder().Build(input with
        {
            Actions = input.Actions.Select(a => a with { Sequence = 0 }).ToArray()
        }));
    }

    [Fact]
    public void Dividend_reinvestment_requires_its_ex_date_close_and_declared_coverage()
    {
        var input = Input([Close(0, 100m), Close(3, 99m)], [Action("dividend", 1, CorporateActionKind.CashDividend, 1m, 1m)]);
        Assert.Throws<InvalidDataException>(() => new TotalReturnBuilder().Build(input));
        Assert.Throws<InvalidDataException>(() => new TotalReturnBuilder().Build(input with { Coverage = input.Coverage with { ThroughInclusive = Start.AddDays(1) } }));
        Assert.Throws<InvalidDataException>(() => new TotalReturnBuilder().Build(input with { Coverage = input.Coverage with { Verification = VerificationState.Unverified } }));
    }

    [Fact]
    public void A_future_correction_cannot_rewrite_an_earlier_snapshot_and_unverified_corrections_withdraw_terms()
    {
        var first = Action("split-v1", 1, CorporateActionKind.Split, 2m, 0m);
        var correction = first with { ActionId = "split-v2", FirstKnownAt = Start.AddDays(5), SharesAfterPerShareBefore = 3m, SupersedesActionId = first.ActionId };
        var input = Input([Close(0, 100m), Close(1, 50m)], [first, correction]);
        var builder = new TotalReturnBuilder();
        var old = builder.Build(input with { AsOf = Start.AddDays(4) });
        var current = builder.Build(input);
        Assert.Equal(100m, old.Prices[^1].AdjustedTotalReturnClose);
        Assert.Equal(150m, current.Prices[^1].AdjustedTotalReturnClose);
        Assert.NotEqual(old.InputSha256, current.InputSha256);
        Assert.Throws<InvalidDataException>(() => builder.Build(input with { Actions = [first, correction with { Verification = VerificationState.Unverified }] }));
    }

    [Theory]
    [InlineData(CorporateActionKind.TerminalWriteOff, 0)]
    [InlineData(CorporateActionKind.TerminalCashSettlement, 40)]
    public void Terminal_outcomes_are_explicit_and_a_security_cannot_resurrect(CorporateActionKind kind, int cash)
    {
        var terminal = Action("terminal", 2, kind, 0m, cash);
        var input = Input([Close(0, 100m), Close(1, 100m)], [terminal]);
        var result = new TotalReturnBuilder().Build(input).Prices;
        Assert.True(result[^1].IsTerminal);
        Assert.Equal(cash, result[^1].AdjustedTotalReturnClose);
        Assert.Throws<InvalidDataException>(() => new TotalReturnBuilder().Build(input with { Closes = [.. input.Closes, Close(3, 50m)] }));
    }

    [Fact]
    public void Long_term_writeoff_reports_a_full_loss_and_remains_depleted_at_later_rebalances()
    {
        DateTimeOffset decision = Start.AddHours(-15), next = decision.AddMonths(3), last = next.AddMonths(3);
        DateTimeOffset entry = decision.AddDays(1), exit = next.AddDays(1), end = last.AddDays(1);
        TotalReturnPrice Price(string id, DateTimeOffset at, decimal value) => new(id, at, at, value, "test-v1");
        TotalReturnPrice[] prices = [Price("TRI", entry, 100m), Price("TRI", exit, 110m), Price("TRI", end, 120m),
            Price("SYNTH", entry, 100m), Price("SYNTH", entry.AddDays(1), 0m) with { IsTerminal = true, TerminalReason = "TerminalWriteOff" }];
        var report = new LongTermEvaluator().Evaluate([decision, next, last], Metrics(decision), prices, "TRI", 5000m, 1, 10m, 10m, 20m);
        Assert.True(report.PortfolioDepleted);
        Assert.Equal(0m, report.FinalCapital);
        Assert.Equal(-100m, report.TotalNetReturnPercent);
        Assert.Equal(100m, report.MaximumRebalanceDrawdownPercent);
        Assert.Equal(["SYNTH"], report.Periods[0].TerminalSecurities);
        Assert.Empty(report.Periods[1].Securities);
        Assert.Equal(0m, report.Periods[1].TradingCostsRupees);
        Assert.Throws<InvalidDataException>(() => new LongTermEvaluator().Evaluate([decision, next], Metrics(decision), prices,
            "TRI", 5000m, 1, 0m, 0m, 0m, returnDataAsOf: entry));
    }

    [Fact]
    public void No_holdings_stays_in_cash_and_does_not_charge_entry_fees()
    {
        DateTimeOffset decision = Start.AddHours(-15), next = decision.AddMonths(3);
        var report = new LongTermEvaluator().Evaluate([decision, next], [],
            [new("TRI", decision.AddDays(1), decision.AddDays(1), 100m, "v1"),
                new("TRI", next.AddDays(1), next.AddDays(1), 110m, "v1")], "TRI", 5000m, 1, 50m, 50m, 20m);
        Assert.Equal(5000m, report.FinalCapital);
        Assert.Equal(0m, report.Periods[0].TradingCostsRupees);
        Assert.Equal(0m, report.Periods[0].CostPercent);
    }

    [Fact]
    public void Terminal_cash_is_carried_without_a_broker_sale_and_missing_terminal_flags_fail()
    {
        DateTimeOffset decision = Start.AddHours(-15), next = decision.AddMonths(3), entry = decision.AddDays(1), exit = next.AddDays(1);
        TotalReturnPrice[] prices = [new("TRI", entry, entry, 100m, "v1"), new("TRI", exit, exit, 110m, "v1"),
            new("SYNTH", entry, entry, 100m, "v1"), new("SYNTH", entry.AddDays(1), entry.AddDays(1), 40m, "v1", true, "TerminalCashSettlement")];
        var evaluator = new LongTermEvaluator();
        var report = evaluator.Evaluate([decision, next], Metrics(decision), prices, "TRI", 5000m, 1, 0m, 100m, 100m);
        Assert.Equal(2000m, report.FinalCapital);
        Assert.Equal(-60m, report.TotalNetReturnPercent);
        Assert.Equal(0m, report.Periods[0].TradingCostsRupees);
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate([decision, next], Metrics(decision),
            prices.Select(p => p.SecurityId == "SYNTH" && p.IsTerminal ? p with { IsTerminal = false, AdjustedTotalReturnClose = 0m } : p).ToArray(),
            "TRI", 5000m, 1, 0m, 0m, 0m));
    }

    [Fact]
    public void Unfunded_sell_charges_are_reported_and_case_duplicate_prices_are_rejected()
    {
        DateTimeOffset decision = Start.AddHours(-15), next = decision.AddMonths(3), entry = decision.AddDays(1), exit = next.AddDays(1);
        TotalReturnPrice[] prices = [new("TRI", entry, entry, 100m, "v1"), new("TRI", exit, exit, 110m, "v1"),
            new("SYNTH", entry, entry, 100m, "v1"), new("SYNTH", exit, exit, 100m, "v1")];
        var evaluator = new LongTermEvaluator();
        var report = evaluator.Evaluate([decision, next], Metrics(decision), prices, "TRI", 1m, 1, 0m, 0m, 20m);
        Assert.True(report.PortfolioDepleted);
        Assert.Equal(19m, report.Periods[0].UnfundedExitCostsRupees);
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate([decision, next], Metrics(decision),
            [.. prices, prices[2] with { SecurityId = "synth" }], "TRI", 5000m, 1, 0m, 0m, 0m));
    }

    private static CompanyMetric[] Metrics(DateTimeOffset at) => Enum.GetValues<CompanyMetricKind>().Select(kind => new CompanyMetric(
        "SYNTH", kind, kind switch { CompanyMetricKind.AverageDailyTurnoverRupees => 12_000_000m, CompanyMetricKind.NetDebtToEbitda => 0.4m, _ => 10m },
        at.AddDays(-1), "fact-" + kind, VerificationState.Verified)).ToArray();
}
