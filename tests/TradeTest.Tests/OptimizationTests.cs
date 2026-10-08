using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TradeTest.Application;

namespace TradeTest.Tests;

public sealed class OptimizationTests
{
    // Frozen from eb4309c before replacing prefix scans with incremental state.
    [Theory]
    [InlineData("trade", "CA25BEEDBB31955863FFFDB0E56243BFAE3C1AB14ED1C9405C3AF2A6909C0FE7")]
    [InlineData("no-trade", "A97F989086D57E6C67550C7B54B362D384A50CA0F0478FD0C7DB8E1388E5D5B4")]
    [InlineData("rejected", "B1095215B56E1928A1BC4F165D2FFB7D1FC63017D73E8CB9118D82977EB7E34F")]
    [InlineData("partial", "4AC57FC19921CFC14573FE7789DF0A5480EE58FE65678A81498C28E9B97D6F7A")]
    public void Optimized_replay_preserves_frozen_decisions_fills_and_costs(string scenario, string expectedHash)
    {
        var bars = TradingTests.Bars().Select(b => b with { SecurityId = "SYNTH-ONE" }).ToArray();
        var policy = new TradeTest.Domain.RiskPolicy(5000m, 50m, 100m, 1, 1.5m, 30m, 5m,
            TimeSpan.FromSeconds(30), "bench-v1");
        var config = new SimulationConfig(5000m, 5m, 2m, int.MaxValue, new TimeSpan(9, 40, 0), policy);
        if (scenario == "no-trade") bars = bars.Select(b => b with { Open = 100m, High = 100.1m, Low = 99.9m, Close = 100m }).ToArray();
        if (scenario == "rejected") config = config with { Policy = policy with { MinimumRewardRisk = 3m } };
        if (scenario == "partial") config = config with { MaxFillQuantityPerBar = 2 };
        var report = new ReplayEngine().Run(bars, config);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report))));
        Assert.Equal(expectedHash, hash);
        var lean = new ReplayEngine().Run(bars, config, captureEvents: false);
        Assert.Empty(lean.Events);
        Assert.Equal(report.Trades, lean.Trades);
        Assert.Equal(report.NetPnl, lean.NetPnl);
    }

    [Fact]
    public void Incremental_session_remains_closed_after_a_missing_bar()
    {
        var bars = TradingTests.Bars();
        var session = new OpeningRangeStrategy().CreateSession();
        session.Append(bars[0], bars[0].EndsAt);
        session.Append(bars[2], bars[2].EndsAt);
        session.Append(bars[3], bars[3].EndsAt);
        Assert.Null(session.CurrentCandidate());
    }
}
