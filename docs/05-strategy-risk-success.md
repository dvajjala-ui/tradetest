# Strategy, Risk and Success Metrics

## Initial research strategy
Start with one simple explainable intraday momentum/opening-range + VWAP family using 5-minute candles, price vs VWAP, EMA relationship, volume expansion, ATR, previous-day levels, index direction and optionally verified material news. Exact thresholds must come from backtesting.

## Current live assumptions
- ₹5,000 trading capital; infrastructure paid separately.
- Indian equity cash/intraday only for V1.
- F&O OFF.
- Leverage OFF initially.
- One open position maximum.
- 0-1 trade/day initially.
- Averaging down forbidden.
- Martingale forbidden.
- Mandatory stop and mandatory session/exit policy.
- Emergency kill switch.

## No forced daily profit
Do not encode 'must make ₹5 today' or 'must trade today'. NO TRADE is a valid successful outcome. The objective is to take only validated setups.

## RiskEngine checks
Before any entry verify: LIVE mode, kill switch off, allowed time, fresh data, healthy broker connection, no unresolved prior order, allow-listed symbol/product, daily trade count, daily loss cap, position-value cap, mandatory stop, valid stop distance, minimum reward/risk, acceptable spread/slippage, candidate age, valid AI schema and all required escalations.

Any failed check means reject.

## Position safety
After a fill, persist actual fill, supervise stop/target without an LLM, prevent duplicate exits, reconcile broker state and close according to emergency/session rules even if every AI provider is offline.

## Paper validation
Run roughly 30+ market sessions using live data, realistic brokerage, spread/slippage and the same production strategy/risk code. Record rejected candidates as well as accepted ones.

## Metrics
Trading: gross/net P&L, win rate, average win/loss, payoff ratio, profit factor, expectancy, max drawdown, consecutive losses, MAE/MFE, duration, no-trade days.
Execution: signal-to-order latency, acknowledgement/fill latency, slippage, rejections, partial fills, reconciliation mismatches.
AI: decision ratios, disagreement/escalation rate, latency, tokens/cost, confidence calibration, outcomes by decision.

## Validation gates
Gate 0 engineering correctness: no duplicate orders, restart recovery, P&L reconciliation, stale-signal rejection, kill switch.
Gate 1 historical/replay: plausible positive expectancy after costs without look-ahead/obvious overfit.
Gate 2 live-data paper: stable operation, acceptable drawdown, promising net expectancy, no critical safety bugs.
Gate 3 ₹5,000 live: restrictive real-fill validation and capital preservation.
Gate 4 scale only after sufficient live evidence.

## Success definition
The project succeeds when it demonstrates positive expectancy after all costs with controlled drawdown and reliable execution. It does not succeed merely because a few days or trades were profitable.