# TradeTest: implementation proposal for review

Status: **G0–G4 approved; offline implementation underway**. Research checked on 2026-10-08 (India). [Current implementation status](16-implementation-status.md) records progress and open gates. Broker purchases and real-money activation retain their separate G5/G6 reviews. The older conversation record in `00-conversation-context.md` remains a history of the original intraday idea.

## 1. Decision to make

Build one evidence and accounting platform with **two separately measured strategies**:

| Lane | Horizon | First experiment | Decision cadence | Capital and evaluation |
| --- | --- | --- | --- | --- |
| A: intraday research | minutes to one session | Liquid cash equities, one simple completed 5-minute-bar rule | Bounded event-driven worker; optional AI only before an order and only if signal remains fresh | Prior ₹5,000 live pilot remains a *planning assumption*, funded separately from infrastructure; no live order before gates |
| B: long-term discovery | quarters to years | Point-in-time quality/growth/momentum ranking plus a diversified index or ETF benchmark | Nightly/weekly research; manual approval for purchases in V1 | Separate capital allocation and risk limits to be chosen; measure total return and drawdown after all costs |

Do **not** mix the two P&L series or use an intraday model score as a long-term buy signal. A third, small-cap discovery track may be researched inside lane B, but liquidity, corporate actions, promoter/pledge data, dilution, governance, and manipulation checks are prerequisites before it can produce a purchase proposal. The user wants upside from exceptional companies; the system should search for evidence of durable growth without treating a single-digit share price as evidence of value. SEBI warns specifically about SME securities and unverified tips, while stock-return research shows large winners are rare in its US sample; the latter is a caution about selection, **not an India success probability**. [SEBI SME advisory](https://www.sebi.gov.in/media-and-notifications/press-releases/aug-2024/advisory-regarding-investment-in-securities-of-the-companies-listed-on-the-sme-segment-of-stock-exchanges_86205.html); [Bessembinder, *Do Stocks Outperform Treasury Bills?*](https://asu.elsevierpure.com/en/publications/do-stocks-outperform-treasury-bills/).

### What can be predicted

The platform may publish **scenario returns** and empirically calibrated probabilities after sufficient out-of-sample evidence. It must never display an LLM's confidence as the chance of profit. There is currently no defensible percentage chance that this project will be profitable. For perspective, ₹5,000 becoming ₹1 crore with no additional deposits requires **2,000×**, or **199,900%** cumulative growth: approximately **113.85% CAGR for 10 years**, **46.24% for 20**, or **28.84% for 30**. These are mathematical hurdles, not forecasts. A ₹5,000 account gaining 1%, 5%, or 10% makes ₹50, ₹250, or ₹500 before charges and taxes; losing those percentages costs the same rupee amounts.

## 2. Design rules

1. A model may explain, rank, reject, or propose. Only versioned deterministic code can size a position or authorize an order. Broker credentials exist only in the execution service.
2. Preserve source URL/document ID, publisher, publication time, *first-known time*, retrieval time, licence, checksum, parser version, and correction history for every external fact. Market facts are not promoted to verified by model consensus.
3. Separate `event_time` from `observed_at`. A backtest can use only information actually available before its simulated decision time. Store delisted stocks and the historical investable universe. Adjust prices and quantities for splits, bonuses, rights, dividends, and symbol changes when calculating returns.
4. An unavailable source, stale quote, unresolved order, missing position, broken clock, failed persistence, or required AI timeout stops **new entries**. Open-position exits and reconciliation do not depend on AI.
5. Research may be broad; each trading decision gets a small, relevant, versioned evidence packet. Token/context size is no substitute for retrieval quality or data freshness.
6. Run a rules-only baseline beside every AI-assisted variant. Keep prompts, models, thresholds, vendor versions, and source snapshots fixed during each evaluation period. Do not tune on the holdout.
7. `NO_TRADE` and `NO_BUY` are valid outcomes. There is no daily profit target, compulsory trade, martingale, averaging down, leverage, or F&O in the first release.
   For lane A, position notional may not exceed unencumbered cash even if a broker product offers intraday margin; lane B uses delivery holdings with a separate capital ledger.
8. Fixed infrastructure costs, broker trading costs, and portfolio returns are separate ledgers. Report both strategy net P&L and whole-project economics.

These safeguards address observed problems in [SEBI's equity intraday study](https://www.sebi.gov.in/media-and-notifications/press-releases/jul-2024/sebi-study-finds-that-7-out-of-10-individual-intraday-traders-in-equity-cash-segment-make-losses_84948.html), the [backtest-overfitting literature](https://escholarship.org/uc/item/4w1110bb), and anecdotal trader reports of [slippage](https://www.reddit.com/r/BhartiyaStockMarket/comments/1tfobo7/algo_trading_in_india_if_backtests_work_but_live/) and [misstated internal P&L](https://www.reddit.com/r/IndiaAlgoTrading/comments/1w1qbx3/finally_cracked_it_4_years_of_failure_to_success/). The Reddit accounts are failure-mode leads, not evidence of expected return.

## 3. System structure

```text
Licensed broker/exchange/company/regulator/news sources
  -> ingestion + immutable raw archive + source/version/licence register
  -> normalised security master, corporate actions, filings, bars, news
  -> point-in-time feature store + citation index + data-quality checks
       |                                |
       | pre-market/nightly             | research retrieval
       v                                v
  verified research packet       company dossier with dated claims
       |                                |
       v                                v
  A: stream -> 5m bars -> rules  B: weekly universe -> rank -> thesis
       |                                |
       v                                v
  optional bounded AI filter     AI research/review, human decision
       |                                |
       v                                v
  deterministic RiskEngine       separate portfolio policy and ledger
       |
       v
  order intent -> broker adapter -> broker fill/position reconciliation
       |
       v
  alerts, audit, dashboards, replay, baseline-vs-AI evaluation
```

### Storage and services

- .NET worker and API, shared domain/event contracts, separate ingestion, research, strategy, risk, execution, reconciliation, and analytics modules. Keep a modular monolith until measured load warrants distribution; this reduces failure and operational overhead.
- SQLite is enough for local replay and prototype. Use PostgreSQL for multi-process production transactions and immutable object storage for raw documents/market files. Keep a small in-memory view of the current session and a bounded queue; the database remains authoritative for order state.
- Full text/search plus embeddings for a **citation index**, keyed by company/security ID and as-of time. Retrieval must return source spans and timestamps. Structured facts and numeric features live in tables, not embeddings. Rebuildable indexes are cache, not truth.
- Use a stable security master: exchange, ISIN, symbol history, segment, listing/delisting dates, corporate action chain. Daily point-in-time universe snapshots prevent survivorship bias.
- Raw data access follows licence terms. NSE explicitly sets agreements and redistribution limits for market and corporate data. Public web pages are not presumed to grant bulk scraping or commercial reuse. [NSE data policy](https://www.nseindia.com/static/market-data/nse-data-policy); [NSE data products](https://www.nseindia.com/static/market-data/products-tariff).

### Proposed repo modules and interfaces

```text
src/TradeTest.Domain/          SecurityId, dated fact, event, bar, position, money
src/TradeTest.Data/            source adapters, raw archive, quality, security master
src/TradeTest.Research/        company dossiers, retrieval, citations, packets
src/TradeTest.Strategies/      lane A signals; lane B ranks; frozen manifests
src/TradeTest.Risk/            independent policies for trading and investing
src/TradeTest.Brokers/         market data, execution, simulator, Groww adapter
src/TradeTest.Worker/          scheduling, stream, bounded queues, reconciliation
src/TradeTest.Api/             read-only dashboard/controls with audit trail
tests/                        replay, point-in-time, cost, risk, fault tests
research/                     dated hypotheses and immutable result manifests
```

Core interfaces: `IMarketDataFeed`, `IHistoricalDataSource`, `IDocumentSource`, `IResearchRetriever`, `IStrategy`, `IAssessmentProvider`, `IRiskPolicy`, `IExecutionBroker`, `IReconciler`, `ICostModel`. An `OrderIntent` always includes session, strategy/risk version, security ID, candidate snapshot hash, quantity, stop/exit policy and unique reference. A `SourceFact` always includes source ID, first-known timestamp and verification state. No provider adapter can reference the execution interface.

### Data acquisition order

1. Start with Groww authenticated live/feed data, order/fill/position records, and its historical endpoints for the pilot. Groww documents up to 1,000 feed subscriptions; its 1/5/10-minute candle history reaches **the last 3 months**, while daily and weekly candles list **full history**. This is enough for connection validation, not necessarily a robust intraday multi-regime backtest. [Groww feed](https://groww.in/trade-api/docs/python-sdk/feed); [Groww historical data](https://groww.in/trade-api/docs/curl/historical-data).
2. Collect NSE/BSE filings, corporate actions and regulators' releases with timestamps and document IDs. Define licensing and a legal access method before automated bulk ingestion. Request a quote for longer intraday and point-in-time corporate data if the pilot's historical depth is insufficient. [NSE historical products](https://www.nseindia.com/static/market-data/eod-historical-data-subscription); [NSE corporate data](https://www.nseindia.com/static/market-data/corporate-data-subscription).
3. Add a paid news source only if a controlled test shows an incremental gain after its cost and publication delays. Community content is tagged unverified; it can nominate a claim for checking, never create a verified fact.
4. Run daily data-health reports: missing bars, duplicate events, gaps, clock skew, stale bid/ask, inconsistent adjusted prices, filing revisions, cross-source disagreements and parser failures. Quarantine affected symbols/periods.

### Decision timing

Microsecond end-to-end decisions are not realistic for a retail broker API and a remote LLM; five-minute-bar strategies do not require them. Optimise for **fresh decisions and reliable exits**. Precompute indicators incrementally and load the daily packet before market open. On a completed bar: rules and risk checks run locally, AI gets a strict deadline only if its extra value has been shown, then the candidate expires. Instrument `event->bar`, `bar->candidate`, model p50/p95/p99, `intent->ack`, `ack->fill`, feed age, reconnects and slippage. Set actual deadlines after measuring the broker/data path; do not promise a latency number from a vendor marketing claim. A later faster-than-bar strategy requires fresh evidence and another design review.

Normal run of day in **Asia/Kolkata**: pre-market ingest/quality checks and source packet; session-open health/reconciliation; approved monitoring window with candidate expiry and circuit breaker; end-of-session order/position/cash reconciliation; post-market source updates, report and anomaly queue. The operator's short daily review happens around the packet and exception report. A position may require monitoring beyond that review window, so unattended supervision and an explicit session-end exit policy remain mandatory.

### Broker order safety

Persist intent before submission, generate a stable reference ID, and query broker status by that reference on timeout before considering any retry. Groww documents an order reference field and lookup endpoint, but its exact duplicate-submission semantics must be integration-tested; a reference field alone is **not proof of idempotency**. Reconcile orders, trades, positions and cash at start, during session, after reconnect, and end of day. Test partial fills, duplicate callbacks, exchange rejection, client crash, token expiry, stale feed and stop/target race. Consider Groww OCO as broker-side protection only after testing supported cash/MIS combinations and activation timing. [Groww orders](https://groww.in/trade-api/docs/curl/orders); [Groww smart orders](https://groww.in/trade-api/docs/curl/smart-orders).

## 4. AI and agent roles

| Role | Proposed tool | Output | In live order path? |
| --- | --- | --- | --- |
| Numeric indicators, risk, P&L, broker state | .NET deterministic code | Typed events and decisions | Yes; mandatory |
| Large document extraction and low-cost research triage | Groq `openai/gpt-oss-120b` or OpenAI `gpt-6-luna` after benchmark | Claim candidates with citations, never verified facts by itself | No |
| Company/market thesis synthesis and difficult candidate review | OpenAI `gpt-6.1-sol` | Schema-validated assessment with cited evidence and counterevidence | Optional, with timeout and fail-closed rule |
| Deep contradictory-case/weekly failure review | OpenAI `gpt-6-astra` | Independent critique and testable hypotheses | No in first live release |
| Code, tests, backtest inspection, daily incident report | Codex; optional Claude/Gemini | Review artifacts, not orders | No |
| Additional AI tool mentioned by user | Name/capability pending | Evaluate with the same offline benchmark and privilege boundary | No until verified |

Model IDs and prices are documented by [Groq](https://console.groq.com/docs/models) and [OpenAI](https://developers.openai.com/api/docs/pricing). This revises the old default of always calling Groq then Sol then possibly Astra: the **rules-only** version is first, and each model enters only if a preregistered test shows better net decisions or useful research quality. Use strict JSON schema where supported, validate in .NET, cap tokens and deadlines, record the full model/version/prompt/input citation set, and treat prompt-injection text in filings/web pages as data. A consumer chat plan is not an automated API entitlement. AI must not have broker tools, secrets, or authority to change live rules.

## 5. Strategy research and proof

### Lane A: intraday

Baseline: a single long-only, liquid cash equity opening-range/VWAP hypothesis on **completed** 5-minute bars, with predefined universe, spread and turnover filters, session window, index context, stop/exit and one-position cap. Compare against (a) no trading, (b) the rules-only candidate stream, and (c) the same candidates filtered by AI. Do not select parameters using the evaluation set. Exchange charges, brokerage, GST, bid/ask, latency, incomplete fills and forced square-off must enter each replay.

### Lane B: long-term

Baseline: use **Nifty 500 Total Returns Index** as the broad India research benchmark, plus an actually investable broad index fund/ETF after its expense ratio and trading costs. The research strategy must beat the relevant benchmark on risk-adjusted **after-cost total return** before claiming value. NSE explains that total-return indices include reinvested dividends. [NSE TRI methodology](https://www.nseindia.com/static/products-services/indices-total-returns-index); [Nifty 500 index](https://www.niftyindices.com/indices/equity/broad-based-indices/nifty-500). Candidate screens are hypotheses: persistent sales/cash-flow growth, return on capital, balance-sheet quality, dilution, governance, valuation against growth, and medium-term trend/liquidity. Compare quality/growth, momentum and their combination as **separate frozen models**. Use quarterly filings available as of the decision date, explicit reporting lag, delisted names, dividends and corporate-action-adjusted returns. Review a thesis quarterly or on a verified material event; the first release generates recommendations for manual approval.

### Evaluation protocol

- Register each hypothesis, universe, feature definition, costs and stopping rule before testing. Use chronological train/validation/test periods, rolling walk-forward, untouched final holdout, and adverse regimes. Log every tried variant to expose selection bias.
- Measure trade-level net expectancy, its bootstrap uncertainty interval, profit factor, max drawdown, exposure, turnover, slippage and rejection rates. For long-term use total-return CAGR, drawdown, benchmark excess return, tracking error and concentration. Report sample counts alongside every percentage.
- At least **30 live-data paper sessions** is an operational floor from the earlier plan, not proof of positive expectancy. Continue until the confidence interval and stability evidence are adequate; 50–100 trades alone do not guarantee statistical power. Require a shadow run, crash/restart drills, broker reconciliation and a small real-fill pilot before scaling.
- Attribute the AI's incremental value on identical candidate snapshots. An AI filter that reduces trade count but worsens net expectancy is rejected. No model retraining or prompt edits while its evaluation is in progress.
- For long-term proposals, show bear/base/bull assumptions and the implied percentage gain/loss at 1, 3 and 5 years. These are **conditional scenarios** and must expose dilution, valuation and terminal assumptions; no precise probability until calibrated on past out-of-sample proposals.

## 6. Stage gates and deliverables

| Gate | Build/research deliverable | Exit evidence |
| --- | --- | --- |
| G0: source audit | Licence register, broker/API and charge sheet, initial company universe, data-quality specification | Legal source access, historical coverage and costs confirmed |
| G1: replay core | .NET domain, security master, immutable events, simulator, fee engine, risk engine, indicator and long-term feature calculators | Deterministic replay, no look-ahead, corporate-action and ledger tests pass |
| G2: research system | Ingestion, raw archive, dated company dossiers, retrieval/citations, nightly packets and quality dashboard | Sample claims trace to source and as-of time; parser corrections replay |
| G3: hypothesis tests | Frozen lane A and B baselines, walk-forward, final holdouts, cost sensitivity; optional AI ablations | Full report including failed variants, uncertainty and benchmark comparison |
| G4: paper/shadow | Live feed, simulated fills, no-order broker adapter; later authenticated read-only broker checks | Operational sessions, fault drills, measured p95/p99 latency and reconciliation |
| G5: broker integration | Authorized subscription, static IP, token flow, reference lookup, protective order tests | Sandbox/small controlled order tests and manual/live activation checklist |
| G6: restricted live | Explicit user approval, independently configured capital and loss caps | Broker-confirmed net P&L and incident-free sample; pause on any state mismatch |
| G7: scale decision | Compare net evidence with passive benchmark and fixed R&D costs | Stop, revise, or scale only by a new reviewed policy |

**Implementation authorization:** the user approved starting G0–G4 and pushing the work. G5 purchases and G6 real-money activation require their own concrete review of current terms, limits, account state and measured evidence. Track completed work and remaining evidence in [the implementation status](16-implementation-status.md).

## 7. Decisions still needed

1. Confirm whether the earlier **₹5,000 live pilot** still applies, given that infrastructure is unconstrained, and choose a separate long-term portfolio budget and benchmark.
2. Provide the exact name/link for the additional AI tool (heard as “jev”) and its intended role.
3. Choose whether to retain Groww as execution broker after an authenticated integration spike, or move to the comparison winner in `13-broker-and-cost-report.md`.
4. Define account ownership and whether outputs are for the owner's private use only; distributing signals/advice to others changes the regulatory review.
5. Choose a maximum tolerable drawdown, trade loss, annual research/data budget, and the level of manual approval for long-term purchases.
