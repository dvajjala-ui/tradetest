# TradeTest — India trading and long-term investing research plan

This repository records the original trading-bot discussion and the expanded implementation proposal. **The current proposal for approval is [docs/12-master-implementation-plan.md](docs/12-master-implementation-plan.md).** This is a planning repository; no trading software or live orders have been implemented.

## Current concept

Build a **C#/.NET evidence, backtesting and execution platform** for Indian markets with:

- A proposed ₹5,000 restricted intraday live pilot, pending reconfirmation; a separately budgeted long-term investing lane.
- Infrastructure/API costs paid separately.
- A short, selective trading session rather than continuous overtrading.
- Groww Trading API as the provisional first broker, with an adapter boundary and a broker comparison before purchase.
- Deterministic C# code for indicators, risk controls, order validation, position monitoring, kill switches and audit logging.
- A rules-only baseline, with Groq-hosted GPT-OSS and OpenAI Luna/Sol/Astra added only for roles that pass an evidence-based evaluation.
- Codex, Claude, Gemini and other available AI tools as research/review assistants where useful.
- Multiple independent sources for research, with official exchange/company/regulatory data preferred over AI-generated claims.
- A point-in-time company/filing research corpus and separate long-term strategy benchmark.
- Paper and shadow trading first, then tightly controlled live trading after explicit review.
- No assumption that AI or automation guarantees profitability.

## Repository docs

1. [Conversation context](docs/00-conversation-context.md)
2. [Product vision and requirements](docs/01-product-vision.md)
3. [Research findings and evidence](docs/02-research-findings.md)
4. [System architecture](docs/03-system-architecture.md)
5. [AI model routing](docs/04-ai-model-routing.md)
6. [Strategy, risk and success metrics](docs/05-strategy-risk-success.md)
7. [Cost model](docs/06-cost-model.md)
8. [Development roadmap](docs/07-development-roadmap.md)
9. [Prompts and JSON contracts](docs/08-prompts-and-contracts.md)
10. [Open questions and decisions](docs/09-open-questions.md)
11. [Source links](docs/10-sources.md)
12. [Turn-by-turn historical handoff](docs/11-chat-turns.md)
13. [Expanded implementation proposal](docs/12-master-implementation-plan.md)
14. [Broker and cost report](docs/13-broker-and-cost-report.md)
15. [Research ledger and failure lessons](docs/14-research-ledger-and-failure-lessons.md)

Documents 00–11 preserve the original intraday discussion and early planning assumptions. Where an early snapshot differs from the expanded proposal or current vendor documentation, use documents 12–14.

## Core architectural principle

The AI may recommend. **The AI never has unrestricted authority over money.**

The final execution path must always be:

~~~text
Verified dated data -> strategy/rules -> optional AI assessment
  -> deterministic risk engine -> broker API -> deterministic reconciliation
~~~

The risk engine can reject any AI recommendation.

## Important status

This repository currently contains the **planning/research baseline only**. No production trading logic should be considered validated until it has passed backtesting, live-data paper trading, cost/slippage modelling, fault testing and explicit live-trading safeguards.

Model prices, broker charges, rate limits and API rules can change. Re-verify them before implementation or deployment.
