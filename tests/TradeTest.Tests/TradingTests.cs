using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Tests;

public sealed class TradingTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 5, 9, 15, 0, TimeSpan.FromHours(5.5));

    internal static MarketBar[] Bars() =>
    [
        new("SYNTH", Start, TimeSpan.FromMinutes(5), 100m, 100.3m, 99.9m, 100.1m, 1000m),
        new("SYNTH", Start.AddMinutes(5), TimeSpan.FromMinutes(5), 100.1m, 100.5m, 100m, 100.4m, 1000m),
        new("SYNTH", Start.AddMinutes(10), TimeSpan.FromMinutes(5), 100.4m, 100.6m, 100.2m, 100.5m, 1000m),
        new("SYNTH", Start.AddMinutes(15), TimeSpan.FromMinutes(5), 100.5m, 101.1m, 100.4m, 101m, 2000m),
        new("SYNTH", Start.AddMinutes(20), TimeSpan.FromMinutes(5), 100.99m, 101.9m, 100.8m, 101.7m, 1500m)
    ];

    private static RiskPolicy Policy() => new(5000m, 50m, 100m, 1, 1.5m, 30m, 5m, TimeSpan.FromSeconds(30), "test-v1");

    [Fact]
    public void Scanner_uses_only_completed_dated_bars()
    {
        var bars = Bars();
        var scanner = new OpeningRangeStrategy();
        Assert.Null(scanner.Scan(bars[..3], bars[2].EndsAt));
        Assert.Null(scanner.Scan(bars[..4], bars[2].EndsAt));
        var candidate = scanner.Scan(bars[..4], bars[3].EndsAt);
        Assert.NotNull(candidate);
        Assert.Equal(101m, candidate.Entry);
        Assert.Equal(100.6m, candidate.Stop);
        Assert.Equal(101.8m, candidate.Target);
        Assert.Equal(candidate, scanner.Scan(bars[..4], bars[3].EndsAt));
        Assert.Null(scanner.Scan(bars[..4], bars[3].EndsAt.AddSeconds(-1)));
    }

    [Fact]
    public void Data_quality_blocks_gaps_and_incomplete_candles()
    {
        var bars = Bars();
        var gap = new[] { bars[0], bars[2] };
        Assert.Contains(DataQuality.CheckSeries(gap, bars[2].EndsAt), x => x.Code == DataIssueCode.MissingBar);
        Assert.Contains(DataQuality.Check(bars[0] with { IsComplete = false }), x => x.Code == DataIssueCode.IncompleteBar);
        Assert.Contains(DataQuality.Check(bars[0] with { High = 99m }), x => x.Code == DataIssueCode.InvalidOhlc);
    }

    [Fact]
    public void Risk_engine_fails_closed_and_caps_quantity_by_cash_and_stop_distance()
    {
        var candidate = new OpeningRangeStrategy().Scan(Bars()[..4], Bars()[3].EndsAt)!;
        var quote = new MarketQuote(candidate.SecurityId, candidate.GeneratedAt, 100.99m, 101.01m);
        var context = new RiskContext(OperatingMode.Paper, candidate.GeneratedAt,
            candidate.GeneratedAt.AddHours(1), quote, 5000m, 0m, false, 0,
            true, true, true, false);
        var engine = new RiskEngine();
        var approved = engine.Evaluate(candidate, Policy(), context);
        Assert.True(approved.Approved);
        Assert.Equal(49, approved.Quantity);
        Assert.Contains("STALE_QUOTE", engine.Evaluate(candidate, Policy(),
            context with { Now = context.Now.AddMinutes(1) }).Reasons);
        Assert.Contains("KILL_SWITCH", engine.Evaluate(candidate, Policy(),
            context with { KillSwitchActive = true }).Reasons);
        Assert.Contains("MODE_DISABLED", engine.Evaluate(candidate, Policy(),
            context with { Mode = OperatingMode.Off }).Reasons);
        Assert.Contains("POSITION_ALREADY_OPEN", engine.Evaluate(candidate, Policy(),
            context with { HasOpenPosition = true }).Reasons);
        var remaining = engine.Evaluate(candidate, Policy(), context with { RealizedDailyPnl = -90m });
        Assert.True(remaining.Approved);
        Assert.Equal(22, remaining.Quantity); // The submitted limit has ₹0.4505/share of stop risk.
        Assert.Contains("ENTRY_PRICE_MOVED", engine.Evaluate(candidate, Policy(),
            context with { Quote = quote with { Ask = 102m } }).Reasons);
    }

    [Fact]
    public void Published_costs_match_illustrative_round_trip()
    {
        var costs = new GrowwIntradayCostModel().Calculate(5000m, 5000m);
        Assert.Equal(10m, costs.Brokerage);
        Assert.Equal(1.25m, costs.Stt);
        Assert.Equal(13.56226m, costs.Total);
    }

    [Fact]
    public void Simulator_prefers_stop_when_bar_touches_both_levels()
    {
        var broker = new SimulatedBroker();
        var intent = new OrderIntent("TEST-12345", "c1", "SYNTH", OrderSide.Buy, 2,
            Start.AddMinutes(20), Start.AddMinutes(30), 102m, 100m, 103m, "s1", "r1");
        Assert.True(broker.Submit(intent));
        Assert.False(broker.Submit(intent));
        Assert.Throws<InvalidOperationException>(() => broker.Submit(intent with { Quantity = 3 }));
        broker.ProcessBar(new MarketBar("SYNTH", Start.AddMinutes(20), TimeSpan.FromMinutes(5),
            101m, 104m, 99m, 102m, 1000m));
        Assert.Single(broker.Trades);
        Assert.Equal("STOP_FIRST", broker.Trades[0].ExitReason);
        Assert.True(broker.Trades[0].NetPnl < 0);
        Assert.False(broker.HasOpenPosition);
    }

    [Fact]
    public void Simulator_tracks_partial_fills_and_exits_actual_quantity()
    {
        var broker = new SimulatedBroker(maxFillQuantityPerBar: 2);
        var intent = new OrderIntent("TEST-12345", "c1", "SYNTH", OrderSide.Buy, 5,
            Start.AddMinutes(20), Start.AddMinutes(40), 102m, 100m, 103m, "s1", "r1");
        broker.Submit(intent);
        for (int i = 4; i < 7; i++)
            broker.ProcessBar(new MarketBar("SYNTH", Start.AddMinutes(5 * i), TimeSpan.FromMinutes(5),
                101m, 102m, 100.5m, 101m, 1000m));
        Assert.False(broker.HasPendingEntry);
        Assert.True(broker.HasOpenPosition);
        broker.ForceExit(new MarketBar("SYNTH", Start.AddMinutes(30), TimeSpan.FromMinutes(5),
            101m, 102m, 100.5m, 101m, 1000m));
        Assert.Equal(5, broker.Trades[0].Quantity);
        Assert.Equal(3, broker.Fills.Count(x => x.Side == OrderSide.Buy));
        Assert.Equal(5, broker.Fills.Single(x => x.Side == OrderSide.Sell).Quantity);
    }

    [Fact]
    public void Replay_is_deterministic_and_net_of_realistic_costs()
    {
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0), Policy());
        var first = new ReplayEngine().Run(Bars(), config);
        var second = new ReplayEngine().Run(Bars(), config);
        Assert.Equal(first.NetPnl, second.NetPnl);
        Assert.Equal(first.Events, second.Events);
        Assert.Single(first.Trades);
        Assert.True(first.Trades[0].Costs.Total > 10m);
        Assert.True(first.Trades[0].NetPnl < first.Trades[0].GrossPnl);
    }

    [Fact]
    public void Replay_exits_at_configured_time_even_when_input_has_later_bars()
    {
        var bars = Bars();
        bars[4] = bars[4] with { High = 101.5m, Close = 101.2m };
        var later = new MarketBar("SYNTH", Start.AddMinutes(25), TimeSpan.FromMinutes(5),
            101.2m, 110m, 101m, 109m, 1000m);
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue,
            new TimeSpan(9, 40, 0), Policy());
        var report = new ReplayEngine().Run([.. bars, later], config);
        Assert.Single(report.Trades);
        Assert.Equal("SESSION_END", report.Trades[0].ExitReason);
        Assert.Equal(bars[4].EndsAt, report.Trades[0].ExitedAt);
        Assert.Throws<InvalidDataException>(() => new ReplayEngine().Run(bars[..4], config));
    }
}
