# TradeTest Agent Handoff

Read README.md and docs/00-conversation-context.md through docs/10-sources.md before making architectural changes.

## Mission
Build an auditable AI-assisted intraday trading research/execution platform for Groww in C#/.NET. Initial live capital target is ₹5,000; infrastructure is funded separately. The first objective is to prove or reject positive expectancy after real costs, not to guarantee daily income.

## Non-negotiable architecture
1. AI never gets unrestricted broker authority.
2. C# computes indicators and hard numeric rules.
3. AI returns structured assessments only.
4. Deterministic RiskEngine is final authority.
5. ExecutionService alone can submit Groww orders.
6. Open positions must remain safely manageable if all AI providers fail.
7. Default mode is OFF/PAPER, never LIVE.
8. F&O, leverage, averaging-down and martingale are disabled in V1.
9. Every decision/order/fill must be replayable and auditable.
10. Strategy changes require evidence/backtesting; models cannot mutate live strategy during a session.

## Intended model topology
Groq-hosted GPT-OSS = fast inexpensive screening. GPT-6 Sol = main candidate reasoning. GPT-6 Astra = rare escalation/deep analysis. Codex = engineering, log/backtest and offline research support. Claude/Gemini/other models = optional independent research/review. Provider/model availability and pricing must be re-verified before implementation.

## Research rule
Multiple AIs are not a truth oracle. Facts must carry provenance. Prefer exchange/company/regulator primary sources, then reputable news, then community evidence. Models reason over verified facts; they do not create market facts.

## Suggested first task
Do not start by wiring real Groww orders. Create the .NET solution, domain models, SimulatedBroker, indicator engine, one baseline scanner, deterministic RiskEngine and replay tests. Only then add AI providers and real broker integration.

## Safety gate
Do not enable real-money LIVE mode merely because code compiles or a backtest is positive. Require engineering fault tests, cost-aware backtesting, live-data paper trading and broker reconciliation first.