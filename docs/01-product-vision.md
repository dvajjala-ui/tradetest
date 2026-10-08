# Product Vision and Requirements

## Working name

TradeTest

## Problem statement

Evaluate whether a carefully constrained AI-assisted intraday trading system can demonstrate positive live expectancy on highly liquid Indian equities after all realistic costs.

The project must answer this empirically. It must not assume that using more capable models makes trading profitable.

## User objective

Initial experiment:

- ₹5,000 of trading capital.
- Infrastructure paid separately.
- Short daily involvement, ideally around a selective 20-minute trading/review window.
- Very low trade frequency.
- Small controlled losses.
- Potentially small daily gains, but **no mandatory daily profit target**.
- Fully logged system so failures can be traced to strategy, model, market, execution or software.

## Primary goals

1. Build a reliable Groww-connected .NET trading research platform.
2. Create reproducible live-data paper trading.
3. Model realistic brokerage, taxes, spread and slippage.
4. Prove or reject a strategy using evidence.
5. Use AI only where it adds reasoning/research value.
6. Keep risk and order permissions deterministic.
7. Maintain complete audit logs.
8. Make model/provider components replaceable.
9. Allow multiple research sources and models without creating a fragile live dependency.
10. Move to real money only after explicit validation gates.

## Non-goals for V1

- Guaranteed daily income.
- F&O/options trading.
- High-frequency trading.
- Martingale.
- Averaging down.
- Unbounded autonomous AI control.
- Browser-click automation as the primary broker interface.
- Letting an LLM change strategy rules during a live session.
- Training a custom predictive model before a baseline deterministic strategy has been tested.
- Scaling capital before the system demonstrates positive expectancy.

## Functional requirements

### Market data

The system should be able to collect:

- live price,
- OHLC candles,
- volume,
- index context,
- previous-day high/low,
- opening range,
- instrument metadata,
- broker positions/orders,
- fills/executions.

### Deterministic calculations

C# should calculate at minimum:

- VWAP,
- EMA 9/21 or configured periods,
- ATR,
- volume ratio,
- opening-range state,
- distance to previous-day levels,
- reward/risk,
- session timing,
- position P&L.

### Research packet

Each trading day should have a versioned research artifact containing:

- date/session,
- market regime notes,
- verified important news,
- company-specific material events for watched symbols,
- source URLs/identifiers,
- publication timestamps,
- confidence/provenance,
- known risks,
- symbols to avoid if evidence is unreliable.

AI-generated research must not be treated as fact unless source-backed.

### Candidate generation

The deterministic scanner should create candidates only when predefined strategy conditions are sufficiently close to valid.

Candidate data should be structured, not free-form.

### AI evaluation

Models may:

- reject low-quality candidates,
- identify contradictory evidence,
- evaluate contextual risk,
- explain reasons for/against the setup,
- recommend TAKE / REJECT / WAIT / ESCALATE.

Models may not:

- bypass position-size rules,
- bypass daily-loss rules,
- disable stop losses,
- enable F&O,
- exceed capital limits,
- place raw broker orders directly.

### Risk engine

Risk engine must enforce:

- allowed instruments,
- allowed product type,
- max position value,
- max open positions,
- max trades/session,
- daily realized/unrealized loss cap,
- minimum reward/risk,
- maximum acceptable spread/slippage,
- required stop loss,
- trading-window limits,
- kill switch,
- duplicate-order prevention,
- stale-data rejection.

### Execution

Execution must:

- use the broker API rather than browser clicking for production,
- generate idempotency/client order IDs where possible,
- reconcile broker state after placing orders,
- handle partial fills,
- handle rejected orders,
- persist exact broker responses,
- prevent repeated accidental entries.

### Monitoring

The system should track:

- current position,
- stop/target,
- realized/unrealized P&L,
- API connectivity,
- market-data freshness,
- AI provider status,
- broker status,
- kill-switch state.

The AI must not be needed to maintain or close a safety-critical position.

## Quality requirements

- Fail closed: when uncertain about state, do not open a new trade.
- Reproducible: same recorded candidate + strategy version should be replayable.
- Auditable: every decision should show its inputs and rule/model output.
- Provider-independent: AI providers should implement a common interface.
- Testable: broker adapter should support simulator and live implementations.
- Observable: logs and metrics should make live incidents diagnosable.
- Secure: secrets must never be committed to Git.
- Versioned: research, prompts, strategy parameters and risk config should be versioned.

## Initial live scope

- Indian equities only.
- Highly liquid instruments.
- One position at a time.
- 0–1 trade/day initially.
- F&O off.
- Leverage off initially.
- No averaging down.
- No martingale.
- Mandatory stop.
- Mandatory session end/exit handling.

## Definition of "success"

V1 success is not "made ₹5 today."

V1 succeeds if it demonstrates:

- stable operation,
- no unsafe orders,
- reproducible decisions,
- correct cost accounting,
- positive expectancy over a meaningful sample,
- controlled drawdown,
- and paper/live results that are not dramatically worse than backtest assumptions.

Only after those conditions are satisfied should capital scaling be considered.
