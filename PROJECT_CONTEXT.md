# TradeTest Agent Handoff

Read README.md, the historical context in docs/00-conversation-context.md, and the current proposal in docs/12-master-implementation-plan.md through docs/14-research-ledger-and-failure-lessons.md before making architectural changes.

## Mission
Plan an auditable India-focused research platform with **separate** intraday and long-term investing lanes. Groww and a ₹5,000 initial intraday live pilot are provisional assumptions; infrastructure is funded separately. Prove or reject improvement after real costs against rules-only and passive baselines.

## Non-negotiable architecture
1. AI never gets unrestricted broker authority.
2. C# computes indicators and hard numeric rules.
3. AI returns structured assessments only.
4. Deterministic RiskEngine is final authority.
5. ExecutionService alone can submit broker orders.
6. Open positions must remain safely manageable if all AI providers fail.
7. Default mode is OFF/PAPER, never LIVE.
8. F&O, leverage, averaging-down and martingale are disabled in V1.
9. Every decision/order/fill must be replayable and auditable.
10. Strategy changes require evidence/backtesting; models cannot mutate live strategy during a session.

## Intended model topology
Rules-only is the initial baseline. Groq GPT-OSS and OpenAI Luna/Sol/Astra have replaceable, testable research/review roles only if they add measured value. Codex and other assistants can support engineering and offline research. No AI provider is required to manage an open position. Confirm the user's additional named AI tool before assigning it a role. See docs/12-master-implementation-plan.md.

## Research rule
Multiple AIs are not a truth oracle. Facts must carry provenance. Prefer exchange/company/regulator primary sources, then reputable news, then community evidence. Models reason over verified facts; they do not create market facts.

## Next task after the user's plan approval
Follow the G0–G4 sequence in docs/12-master-implementation-plan.md. Start with data/source/licence audit and a replayable rules-only core; add AI only after baseline measurement. Do not wire real orders during the planning phase.

## Safety gate
Do not enable real-money LIVE mode merely because code compiles or a backtest is positive. Require engineering fault tests, cost-aware backtesting, live-data paper trading and broker reconciliation first.
