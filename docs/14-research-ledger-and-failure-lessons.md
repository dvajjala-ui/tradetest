# Research ledger and lessons to encode

As-of 2026-10-08. This ledger separates direct evidence from anecdotes and project hypotheses. It is a starting research base, not a claim that an intraday or stock-picking edge has been found.

| Finding | Evidence quality | What TradeTest will do |
| --- | --- | --- |
| SEBI reported **7 in 10** individual equity-cash intraday traders lost money in its study. | Regulator's historical base rate; does not estimate this system's success probability. [SEBI](https://www.sebi.gov.in/media-and-notifications/press-releases/jul-2024/sebi-study-finds-that-7-out-of-10-individual-intraday-traders-in-equity-cash-segment-make-losses_84948.html) | Treat no-trade and the passive benchmark as serious alternatives; measure after-cost expectancy. |
| Historical strategy selection can overfit badly when many variants are tried. | Published research. [Bailey et al.](https://escholarship.org/uc/item/4w1110bb) | Register trials, freeze holdout, use walk-forward and disclose all attempted variants. |
| A small set of firms accounted for US long-run stock wealth creation; this is **not an India frequency estimate**. | Published US-market paper. [Bessembinder](https://asu.elsevierpure.com/en/publications/do-stocks-outperform-treasury-bills/) | Keep a diversified benchmark and test whether a growth screen finds rare winners without taking uncompensated concentration risk. |
| A Groww-linked Reddit poster described four years of unsuccessful experiments/costs and a very profitable 37-day options run, then corrected an internal P&L calculation. | Self-report, short observation window, different asset/horizon from this project. [Post](https://www.reddit.com/r/IndiaAlgoTrading/comments/1w1qbx3/finally_cracked_it_4_years_of_failure_to_success/) | Log full R&D costs and reconcile against broker records; never infer a sustainable return from a short run. |
| A Dhan-linked bot poster identified a **rolling-strike/contract identity bug** that made earlier logged P&L false. The later posted ₹8,691 day was expressly a **live-data paper-trading** result with a flat assumed cost, not broker-confirmed profit. | Self-report and forward paper experiment. [Bug correction](https://www.reddit.com/r/IndiaAlgoTrading/comments/1udf1qv/day_0/), [paper result clarification](https://www.reddit.com/r/IndiaAlgoTrading/comments/1uedlwj/day_1_more_info_on_my_profile/) | Immutable instrument IDs, actual fills, contract-note reconciliation, and clear PAPER versus LIVE labels everywhere. The earlier repo summary blurred this distinction. |
| Traders describe spread, latency and slippage erasing backtest profits. | Anecdote consistent with execution mechanics, not an estimated failure rate. [Reddit discussion](https://www.reddit.com/r/BhartiyaStockMarket/comments/1tfobo7/algo_trading_in_india_if_backtests_work_but_live/) | Collect bid/ask and intent/ack/fill timestamps; stress slippage and compare simulated versus actual fills. |
| SEBI warned investors about risks in SME securities and unverified social media narratives. | Regulator advisory. [SEBI](https://www.sebi.gov.in/media-and-notifications/press-releases/aug-2024/advisory-regarding-investment-in-securities-of-the-companies-listed-on-the-sme-segment-of-stock-exchanges_86205.html) | Small-cap screen requires liquidity, governance, source provenance, dilution and surveillance checks; no penny-price shortcut. |
| Groww's intraday history reaches only the last three months, while daily/weekly history lists full history. | Current primary API docs. [Groww](https://groww.in/trade-api/docs/curl/historical-data) | Price a longer licensed intraday dataset before claiming a multi-regime backtest; lane B can initially use daily data after quality checks. |
| Exchange market/corporate data reuse is subject to product terms and agreements. | Exchange policy. [NSE](https://www.nseindia.com/static/market-data/nse-data-policy) | Maintain source/licence registry and seek a licensed feed for scale. |

## Research workflow before writing a strategy

1. Record each question as a falsifiable claim: e.g., “a completed opening-range signal on a predeclared liquid universe has positive net expectancy,” or “a quality/growth rank improves the risk-adjusted return over a broad index.”
2. Create a **source record**: publisher, URL/document ID, publication and effective times, retrieval time, author, scope, market period, raw hash, applicable licence, extraction method, corrections, and whether the statement is observed data, author interpretation or anecdote.
3. Write a one-page evidence card per company with financial history, filings, management/governance events, share count changes, debt and pledge changes, price/volume history, peer/sector context, invalidating evidence, and explicit unknowns. Every line links to its original source.
4. Use AI to extract candidate claims and challenge a thesis. A second model should independently review *the same source documents* and identify disagreements. Human or deterministic validation resolves numbers and documents; voting does not.
5. Record a test specification and code/data versions. Inspect data gaps, corporate actions and timestamps before backtesting. Corrected documents create a new version, not silent historical edits.
6. Compare frozen alternatives with common costs and identical observation timestamps. Publish the negative results and the full list of parameters tried. Promote no strategy based on a backtest alone.
7. After live-data paper and broker shadow, publish a broker-confirmed ledger separate from estimates. Stop on unexplained mismatches.

## AI evaluation harness

Construct a dated set of candidate/company cases with original source excerpts and later outcomes hidden from the model. Include contradictory filings, stale news, fabricated social claims, split-adjusted chart traps, exchange holidays, flat/volatile days, partial fills and missing data. Evaluate: unsupported factual claims, citation accuracy, calibrated scores, reject/abstain quality, incremental net P&L after costs, latency p95/p99, token spend, and stability across model/prompt updates. Security test prompt injection inside retrieved pages and leaked broker-tool requests. Only then decide whether Groq, Luna, Sol, Astra, Codex, the user's additional named AI tool, or no LLM should occupy each role.

## Evidence needed for percentage claims

- **Descriptive percentages** (e.g., win rate, after-cost return) require period, market, capital, sample size, compounding rule and costs.
- **Probability of profit** requires a calibrated model and out-of-sample validation; report confidence/uncertainty intervals, base rate and calibration chart. Never translate a model's verbal certainty directly to a percentage.
- **Expected long-term upside** should initially be low/base/high *conditional scenarios* with explicit revenue growth, margins, valuation, dilution, exit date and downside case. The output cannot promise a “next multibagger.”
- **AI uplift** means measured difference versus a rules-only baseline on the *same dated cases*; it is unknown until that test is run.
