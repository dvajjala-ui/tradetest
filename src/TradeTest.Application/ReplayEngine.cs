using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TradeTest.Domain;

namespace TradeTest.Application;

public sealed record SimulationEvent(string Type, DateTimeOffset OccurredAt, string Json);

public sealed record SimulationConfig(
    decimal InitialCash,
    decimal SpreadBps,
    decimal SlippageBps,
    int MaxFillQuantityPerBar,
    TimeSpan SessionEndLocalTime,
    RiskPolicy Policy,
    MarketReferenceData? ReferenceData = null);

public sealed record SimulationReport(
    string StrategyVersion,
    string CostModelVersion,
    decimal InitialCash,
    int CandidateCount,
    int RiskRejectedCount,
    int SubmittedOrders,
    IReadOnlyList<CompletedTrade> Trades,
    IReadOnlyList<SimulationEvent> Events,
    decimal NetPnl,
    decimal NetReturnPercent,
    decimal MaxClosedEquityDrawdownPercent);

public sealed class ReplayEngine
{
    private readonly OpeningRangeStrategy _strategy = new();
    private readonly RiskEngine _risk = new();
    private readonly TimeZoneInfo _india = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public SimulationReport Run(IReadOnlyList<MarketBar> bars, SimulationConfig config, bool captureEvents = true) =>
        RunCompiled(bars, config, captureEvents, referenceValidator: null);

    internal SimulationReport RunCompiled(IReadOnlyList<MarketBar> bars, SimulationConfig config, bool captureEvents,
        MarketReferenceValidator? referenceValidator)
    {
        if (bars.Count == 0) throw new ArgumentException("At least one bar is required.", nameof(bars));
        if (config.InitialCash <= 0 || config.SpreadBps < 0 || config.SlippageBps < 0)
            throw new ArgumentException("Invalid simulation configuration.", nameof(config));
        var problems = DataQuality.CheckSeries(bars, bars[^1].EndsAt);
        if (problems.Count > 0) throw new ArgumentException($"Invalid input bars: {problems[0].Code}.", nameof(bars));
        if (bars.Select(b => TimeZoneInfo.ConvertTime(b.StartsAt, _india).Date).Distinct().Count() != 1)
            throw new ArgumentException("This replay runs exactly one Indian market session.", nameof(bars));

        var broker = new SimulatedBroker(config.SlippageBps, config.MaxFillQuantityPerBar, config.SpreadBps);
        var events = new List<SimulationEvent>();
        int candidates = 0, rejected = 0, orders = 0, lastFillCount = 0, lastTradeCount = 0;
        decimal realized = 0;
        var sessionDate = TimeZoneInfo.ConvertTime(bars[0].StartsAt, _india).Date;
        var endLocal = DateTime.SpecifyKind(sessionDate + config.SessionEndLocalTime, DateTimeKind.Unspecified);
        var sessionEndsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, _india), TimeSpan.Zero);
        var marketReference = referenceValidator ?? (config.ReferenceData is null ? null : new MarketReferenceValidator(config.ReferenceData));
        marketReference?.ValidateReplay(bars, sessionEndsAt);
        int sessionBarCount = 0;
        while (sessionBarCount < bars.Count && bars[sessionBarCount].EndsAt <= sessionEndsAt) sessionBarCount++;
        if (sessionBarCount == 0 || bars[sessionBarCount - 1].EndsAt != sessionEndsAt)
            throw new InvalidDataException("Input bars end before the configured session window closes.");
        var strategySession = _strategy.CreateSession();

        void CaptureBrokerEvents()
        {
            for (int i = lastFillCount; i < broker.Fills.Count; i++)
            {
                var fill = broker.Fills[i];
                Add("FILL", fill.FilledAt, fill);
            }
            lastFillCount = broker.Fills.Count;
            for (int i = lastTradeCount; i < broker.Trades.Count; i++)
            {
                var trade = broker.Trades[i];
                Add("TRADE_CLOSED", trade.ExitedAt, trade);
                realized += trade.NetPnl;
            }
            lastTradeCount = broker.Trades.Count;
        }
        void Add(string type, DateTimeOffset at, object value)
        {
            if (captureEvents) events.Add(new SimulationEvent(type, at, JsonSerializer.Serialize(value)));
        }

        for (int index = 0; index < sessionBarCount; index++)
        {
            var bar = bars[index];
            strategySession.Append(bar, bar.EndsAt);
            broker.ProcessBar(bar);
            CaptureBrokerEvents();
            if (bar.EndsAt >= sessionEndsAt || broker.HasOpenPosition || broker.HasPendingEntry || orders >= config.Policy.MaxTradesPerSession)
                continue;

            var candidate = strategySession.CurrentCandidate();
            if (candidate is null) continue;
            candidates++;
            Add("CANDIDATE", candidate.GeneratedAt, candidate);
            decimal halfSpread = candidate.Entry * config.SpreadBps / 20_000m;
            var quote = new MarketQuote(candidate.SecurityId, candidate.GeneratedAt,
                candidate.Entry - halfSpread, candidate.Entry + halfSpread);
            var context = new RiskContext(OperatingMode.Paper, candidate.GeneratedAt, sessionEndsAt, quote,
                config.InitialCash + realized, realized, broker.HasOpenPosition, orders,
                true, true, true, false);
            var decision = _risk.Evaluate(candidate, config.Policy, context);
            Add("RISK_DECISION", candidate.GeneratedAt, decision);
            if (!decision.Approved)
            {
                rejected++;
                continue;
            }
            string reference = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.CandidateId)))[..16];
            var intent = new OrderIntent(reference, candidate.CandidateId, candidate.SecurityId, OrderSide.Buy,
                decision.Quantity, candidate.GeneratedAt, candidate.ExpiresAt,
                candidate.Entry * (1m + config.Policy.MaxEntryDeviationBps / 10_000m), candidate.Stop,
                candidate.Target, candidate.StrategyVersion, decision.PolicyVersion);
            broker.Submit(intent);
            orders++;
            Add("ORDER_INTENT", intent.CreatedAt, intent);
        }

        broker.ForceExit(bars[sessionBarCount - 1]);
        CaptureBrokerEvents();
        decimal peak = config.InitialCash, equity = peak, maxDrawdown = 0;
        foreach (var trade in broker.Trades)
        {
            equity += trade.NetPnl;
            peak = Math.Max(peak, equity);
            maxDrawdown = Math.Max(maxDrawdown, (peak - equity) / peak * 100m);
        }
        return new SimulationReport(OpeningRangeStrategy.Version, GrowwIntradayCostModel.Version,
            config.InitialCash, candidates, rejected, orders, broker.Trades.ToArray(), events,
            realized, realized / config.InitialCash * 100m, maxDrawdown);
    }
}
