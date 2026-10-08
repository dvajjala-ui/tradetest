# TradeTest — AI-Assisted Intraday Trading Research Project

This repository is the working source of truth for the trading-bot idea developed in the ChatGPT discussion on 2026-10-08.

## Current concept

Build a **C#/.NET automated intraday research and execution system for Groww** with:

- ₹5,000 reserved strictly as initial live trading capital.
- Infrastructure/API costs paid separately.
- A short, selective trading session rather than continuous overtrading.
- Groww Trading API for market data and order execution.
- Deterministic C# code for indicators, risk controls, order validation, position monitoring, kill switches and audit logging.
- Groq-hosted GPT-OSS as a fast/cheap AI screening layer.
- OpenAI GPT-6 Sol as the main reasoning/review layer.
- GPT-6 Astra only for difficult or ambiguous cases and deeper post-market analysis.
- Codex, Claude, Gemini and other available AI tools as research/review assistants where useful.
- Multiple independent sources for research, with official exchange/company/regulatory data preferred over AI-generated claims.
- Paper trading first, then tightly controlled live trading.
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

## Core architectural principle

The AI may recommend. **The AI never has unrestricted authority over money.**

The final execution path must always be:

~~~text
Market data
  -> deterministic scanner
  -> AI evaluation
  -> deterministic risk engine
  -> broker API
  -> deterministic position monitor
~~~

The risk engine can reject any AI recommendation.

## Important status

This repository currently contains the **planning/research baseline only**. No production trading logic should be considered validated until it has passed backtesting, live-data paper trading, cost/slippage modelling, fault testing and explicit live-trading safeguards.

Model prices, broker charges, rate limits and API rules can change. Re-verify them before implementation or deployment.
