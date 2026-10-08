using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Conservative single-position bar simulator. Stops win when a bar touches both stop and target.</summary>
public sealed class SimulatedBroker
{
    private sealed class PendingEntry(OrderIntent intent)
    {
        public OrderIntent Intent { get; } = intent;
        public int FilledQuantity { get; set; }
    }

    private sealed class Position(OrderIntent intent, SimulatedFill firstFill)
    {
        public OrderIntent Intent { get; } = intent;
        public int Quantity { get; private set; } = firstFill.Quantity;
        public decimal EntryNotional { get; private set; } = firstFill.Price * firstFill.Quantity;
        public DateTimeOffset EnteredAt { get; } = firstFill.FilledAt;
        public decimal AverageEntry => EntryNotional / Quantity;
        public void Add(SimulatedFill fill)
        {
            Quantity += fill.Quantity;
            EntryNotional += fill.Price * fill.Quantity;
        }
    }

    private readonly GrowwIntradayCostModel _costs = new();
    private readonly Dictionary<string, OrderIntent> _submitted = new(StringComparer.Ordinal);
    private readonly List<SimulatedFill> _fills = [];
    private readonly List<CompletedTrade> _trades = [];
    private PendingEntry? _pending;
    private Position? _position;
    private DateTimeOffset? _lastBarEnd;
    private readonly decimal _slippageBps;
    private readonly decimal _spreadBps;
    private readonly int _maxFillQuantityPerBar;

    public SimulatedBroker(decimal slippageBps = 0m, int maxFillQuantityPerBar = int.MaxValue, decimal spreadBps = 0m)
    {
        if (slippageBps < 0 || slippageBps > 1000) throw new ArgumentOutOfRangeException(nameof(slippageBps));
        if (spreadBps < 0 || spreadBps > 1000) throw new ArgumentOutOfRangeException(nameof(spreadBps));
        if (maxFillQuantityPerBar < 1) throw new ArgumentOutOfRangeException(nameof(maxFillQuantityPerBar));
        _slippageBps = slippageBps;
        _spreadBps = spreadBps;
        _maxFillQuantityPerBar = maxFillQuantityPerBar;
    }

    public bool HasOpenPosition => _position is not null;
    public bool HasPendingEntry => _pending is not null;
    public IReadOnlyList<SimulatedFill> Fills => _fills;
    public IReadOnlyList<CompletedTrade> Trades => _trades;

    /// <returns>True for a new intent, false for an exact duplicate reference/intent.</returns>
    public bool Submit(OrderIntent intent)
    {
        if (intent.Quantity <= 0 || intent.Side != OrderSide.Buy || intent.ExpiresAt <= intent.CreatedAt)
            throw new ArgumentException("Invalid simulated buy intent.", nameof(intent));
        if (_submitted.TryGetValue(intent.ReferenceId, out var prior))
        {
            if (prior != intent) throw new InvalidOperationException("Reference ID reused for a different intent.");
            return false;
        }
        if (_pending is not null || _position is not null) throw new InvalidOperationException("Only one pending or open position is allowed.");
        _submitted.Add(intent.ReferenceId, intent);
        _pending = new PendingEntry(intent);
        return true;
    }

    public void ProcessBar(MarketBar bar)
    {
        if (DataQuality.Check(bar).Count != 0) throw new ArgumentException("Invalid or incomplete bar.", nameof(bar));
        if (_lastBarEnd is not null && bar.StartsAt < _lastBarEnd)
            throw new InvalidOperationException("Bars must be processed once in time order.");
        _lastBarEnd = bar.EndsAt;

        if (_pending is not null && bar.SecurityId == _pending.Intent.SecurityId && bar.StartsAt >= _pending.Intent.CreatedAt)
        {
            if (bar.StartsAt >= _pending.Intent.ExpiresAt)
            {
                _pending = null;
            }
            else
            {
                decimal price = bar.Open * (1m + (_spreadBps / 2m + _slippageBps) / 10_000m);
                if (price <= _pending.Intent.LimitPrice && price > _pending.Intent.StopPrice)
                {
                    int amount = Math.Min(_maxFillQuantityPerBar, _pending.Intent.Quantity - _pending.FilledQuantity);
                    var fill = new SimulatedFill(_pending.Intent.ReferenceId, bar.SecurityId, OrderSide.Buy, amount, price, bar.StartsAt);
                    _fills.Add(fill);
                    _pending.FilledQuantity += amount;
                    if (_position is null) _position = new Position(_pending.Intent, fill);
                    else _position.Add(fill);
                    if (_pending.FilledQuantity == _pending.Intent.Quantity) _pending = null;
                }
            }
        }

        if (_position is null || bar.SecurityId != _position.Intent.SecurityId) return;
        if (bar.Low <= _position.Intent.StopPrice)
        {
            decimal price = Math.Min(bar.Open, _position.Intent.StopPrice) * (1m - (_spreadBps / 2m + _slippageBps) / 10_000m);
            Close(price, bar.EndsAt, "STOP_FIRST");
        }
        else if (bar.High >= _position.Intent.TargetPrice)
        {
            decimal price = _position.Intent.TargetPrice * (1m - (_spreadBps / 2m + _slippageBps) / 10_000m);
            Close(price, bar.EndsAt, "TARGET");
        }
    }

    public void ForceExit(MarketBar lastBar)
    {
        if (_position is not null)
            Close(lastBar.Close * (1m - (_spreadBps / 2m + _slippageBps) / 10_000m), lastBar.EndsAt, "SESSION_END");
        _pending = null;
    }

    private void Close(decimal price, DateTimeOffset at, string reason)
    {
        var position = _position ?? throw new InvalidOperationException("No position to close.");
        var fill = new SimulatedFill(position.Intent.ReferenceId, position.Intent.SecurityId, OrderSide.Sell,
            position.Quantity, price, at);
        _fills.Add(fill);
        decimal gross = price * position.Quantity - position.EntryNotional;
        var costs = _costs.Calculate(position.EntryNotional, price * position.Quantity);
        _trades.Add(new CompletedTrade(position.Intent.CandidateId, position.Intent.SecurityId, position.Quantity,
            position.EnteredAt, at, position.AverageEntry, price, gross, costs, reason));
        _position = null;
        _pending = null; // Cancel unfilled entry quantity if a partial position exits.
    }
}
