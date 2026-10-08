# Development Roadmap

> Original intraday roadmap. Follow the current stage gates G0–G7 in [12-master-implementation-plan.md](12-master-implementation-plan.md); implementation begins only after plan approval.

## Phase 0 - documentation
Maintain requirements, architecture, research findings, risk rules, sources and decisions. No live trading.

## Phase 1 - .NET skeleton
Create Domain, Application, Infrastructure, Worker, API and test projects. Default mode OFF/PAPER. Add health checks, DI, strongly typed config, structured logging and secret exclusion.

## Phase 2 - persistence/replay
SQLite initially. Persist sessions, research packets, candidates, AI assessments, risk decisions, intents, broker orders, fills, positions, P&L and incidents. A completed session must be replayable.

## Phase 3 - broker abstraction
Define market-data and execution interfaces. Build SimulatedBroker first, including rejection, fill, cost/slippage model and later partial fills. Entire pipeline must work without Groww credentials.

## Phase 4 - indicators/scanner
Implement candles, VWAP, EMA, ATR, volume ratio, opening range and previous levels. Add one baseline strategy with strong unit tests.

## Phase 5 - risk engine
Implement hard safety before live AI/broker integration: size, max loss, duplicate, stale candidate, product restrictions, time window, required stop, reward/risk and kill switch. AI cannot override it.

## Phase 6 - AI abstraction
Implement Groq and OpenAI providers behind an ITradeAssessmentProvider-style interface. Structured JSON only, with schema validation and deterministic fake provider for tests.

## Phase 7 - daily research packet
Build versioned source-backed research JSON. Initially semi-automated is acceptable. Preserve provenance and verification state.

## Phase 8 - live-data paper trading
Use live data with simulated execution for 30+ sessions. Measure candidate frequency, model disagreement, costs, latency, slippage assumptions and failures. Do not tune after every loss.

## Phase 9 - post-market analytics
Generate daily reports. Codex/Astra may propose hypotheses, but every change follows hypothesis -> backtest -> compare -> accept/reject.

## Phase 10 - Groww execution
Implement auth, static IP, orders, positions/fills and reconciliation. Run SHADOW mode before real orders.

## Phase 11 - ₹5,000 live experiment
Only after safety/paper gates. One position, 0-1 trade/day, no F&O/leverage/averaging, mandatory stop and small configured loss cap.

## Phase 12 - evaluate
Compare historical, paper, shadow and live. Attribute degradation to strategy, slippage, fees, model choices, latency, regime or bugs before deciding to stop, iterate or scale.

## Best first coding milestone
Build domain models + simulator + indicator engine + one strategy + risk engine + replay tests before touching real broker execution.
