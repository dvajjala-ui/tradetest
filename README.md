# TradeTest — India trading and long-term investing research

This repository contains the [approved G0–G4 plan](docs/12-master-implementation-plan.md), its supporting research, and an initial **offline** .NET implementation. It can replay synthetic market sessions, record a tamper-evident event journal, store dated research, and run preliminary strategy evaluations. It cannot connect to a broker or place orders.

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
16. [Source register and data contract](docs/15-source-register-and-data-contract.md)
17. [Implementation status and next gates](docs/16-implementation-status.md)

Documents 00–11 preserve the original intraday discussion and early planning assumptions. Where an early snapshot differs from the expanded proposal or current vendor documentation, use documents 12–16.

## Run the offline prototype

Install the .NET 10 SDK, then run from the repository root:

~~~bash
dotnet test TradeTest.slnx
dotnet run --project src/TradeTest.Cli -- demo
dotnet run --project src/TradeTest.Cli -- evaluate-study fixtures/synthetic-study.json
dotnet run --project src/TradeTest.Cli -- evaluate-long-term fixtures/synthetic-long-term.json
~~~

To inspect the dated research example locally:

~~~bash
dotnet run --project src/TradeTest.Cli -- import-research fixtures/synthetic-research.json research.sqlite
dotnet run --project src/TradeTest.Cli -- research research.sqlite 2026-04-01T00:00:00Z SYNTH-ONE revenue
~~~

All bundled prices, documents, companies, and returns are **synthetic**. The example ₹5,000 is a research configuration, not an activated trading budget. The CLI has no live broker adapter, API credentials, or order route. Repeating an import into the same SQLite database will reject duplicate IDs; use a fresh database for each example run.

## Core architectural principle

The AI may recommend. **The AI never has unrestricted authority over money.**

The final execution path must always be:

~~~text
Verified dated data -> strategy/rules -> optional AI assessment
  -> deterministic risk engine -> broker API -> deterministic reconciliation
~~~

The risk engine can reject any AI recommendation.

## Important status

This is an **offline prototype**, not a validated trading system or evidence of profitability. [Implementation status](docs/16-implementation-status.md) tracks completed code and the missing licensed data, historical validation, live-feed paper sessions, fault drills, and broker reconciliation.

Model prices, broker charges, rate limits and API rules can change. Re-verify them before implementation or deployment.
