using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Tests;

public sealed class MarketReferenceTests
{
    private static readonly DateTimeOffset Start = TradingTests.Bars()[0].StartsAt;

    [Fact]
    public void Security_history_preserves_old_symbols_and_excludes_future_or_delisted_names()
    {
        var initial = Security("v1", "OLD", Start.AddDays(-1), Start.AddDays(-2));
        var renamed = Security("v2", "NEW", Start.AddDays(1), Start.AddDays(1));
        var delisted = Security("v3", "NEW", Start.AddDays(2), Start.AddDays(3)) with { IsListed = false };
        var master = new SecurityMaster([initial, renamed, delisted]);
        Assert.Equal("SYNTH", master.ResolveSymbol("NSE", "OLD", Start)!.SecurityId);
        Assert.Null(master.ResolveSymbol("NSE", "NEW", Start));
        Assert.Equal("NEW", master.GetAt("SYNTH", Start.AddDays(2))!.Symbol);
        Assert.Empty(master.UniverseAt(Start.AddDays(4)));
        Assert.Equal("OLD", master.GetAt("SYNTH", Start)!.Symbol); // Later knowledge cannot rewrite this snapshot.
        var duplicate = initial with { VersionId = "other", SecurityId = "OTHER" };
        Assert.Throws<InvalidDataException>(() => new SecurityMaster([initial, duplicate]).ResolveSymbol("NSE", "OLD", Start));
    }

    [Fact]
    public void Calendar_requires_explicit_known_sessions_and_accepts_supplied_special_sessions()
    {
        var regular = Session(Start, Start.AddHours(1));
        var sunday = Start.AddDays(6).AddHours(9);
        var special = Session(sunday, sunday.AddHours(1));
        var calendar = new TradingCalendar([regular, special]);
        Assert.Null(calendar.GetAt("NSE", regular.SessionDate, regular.FirstKnownAt.AddSeconds(-1)));
        Assert.NotNull(calendar.GetAt("NSE", special.SessionDate, special.OpensAt));
        Assert.Null(calendar.GetAt("NSE", regular.SessionDate.AddDays(1), Start.AddDays(1)));
    }

    [Fact]
    public void Long_term_selection_excludes_a_future_listing_even_when_metrics_already_exist()
    {
        var decision = Start;
        var next = Start.AddMonths(3);
        CompanyMetric[] Metrics(string id, decimal growth) => Enum.GetValues<CompanyMetricKind>().Select(kind =>
            new CompanyMetric(id, kind, kind switch
            {
                CompanyMetricKind.AverageDailyTurnoverRupees => 12_000_000m,
                CompanyMetricKind.NetDebtToEbitda => 0.5m,
                CompanyMetricKind.RevenueGrowth3YPercent => growth,
                _ => 10m
            }, decision.AddDays(-2), id + kind, VerificationState.Verified)).ToArray();
        var future = Security("future", "FUTURE", decision.AddDays(1), decision.AddDays(-1)) with { SecurityId = "FUTURE" };
        var reference = new MarketReferenceData([Security("old", "SYNTH", decision.AddDays(-1), decision.AddDays(-2)), future], []);
        var entry = decision.AddDays(1);
        var exit = next.AddDays(1);
        TotalReturnPrice Price(string id, DateTimeOffset at, decimal value) => new(id, at, at, value, "synthetic-v1");
        var report = new LongTermEvaluator().Evaluate([decision, next], [.. Metrics("SYNTH", 10m), .. Metrics("FUTURE", 40m)],
            [Price("TRI", entry, 100m), Price("TRI", exit, 105m), Price("SYNTH", entry, 100m), Price("SYNTH", exit, 110m)],
            "TRI", 5000m, 1, 0m, 0m, 0m, reference);
        Assert.True(report.PointInTimeUniverseSupplied);
        Assert.Equal(["SYNTH"], report.Periods[0].Securities);
    }

    [Fact]
    public void Replay_rejects_unknown_calendar_late_listing_information_and_missing_open()
    {
        var bars = TradingTests.Bars();
        var data = new MarketReferenceData([Security("v1", "SYNTH", Start.AddDays(-1), Start.AddDays(-2))],
            [Session(Start, Start.AddHours(1))]);
        var policy = new RiskPolicy(5000m, 50m, 100m, 1, 1.5m, 30m, 5m, TimeSpan.FromSeconds(30), "test-v1");
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0), policy, data);
        Assert.Single(new ReplayEngine().Run(bars, config).Trades);
        Assert.Throws<InvalidDataException>(() => new ReplayEngine().Run(bars, config with { ReferenceData = data with { Sessions = [] } }));
        Assert.Throws<InvalidDataException>(() => new ReplayEngine().Run(bars, config with
        {
            ReferenceData = data with { Securities = [data.Securities[0] with { FirstKnownAt = Start.AddMinutes(1) }] }
        }));
        Assert.Throws<InvalidDataException>(() => new ReplayEngine().Run(bars[1..], config));
    }

    private static SecurityVersion Security(string id, string symbol, DateTimeOffset effective, DateTimeOffset known) =>
        new(id, "SYNTH", "SYNTHETIC-ISIN", "NSE", symbol, "CASH_EQUITY", effective, known, true,
            new Uri("https://example.com/synthetic-security"), "SYNTHETIC_ONLY", VerificationState.Verified);
    private static ExchangeSession Session(DateTimeOffset open, DateTimeOffset close) =>
        new("NSE", DateOnly.FromDateTime(open.Date), open, close, Start.AddDays(-2),
            new Uri("https://example.com/synthetic-calendar"), "SYNTHETIC_ONLY", VerificationState.Verified);
}
