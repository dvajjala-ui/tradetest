# Turn-by-Turn Idea Evolution

> Historical handoff. Current expanded proposal: [12-master-implementation-plan.md](12-master-implementation-plan.md). The Dhan result mentioned in the original discussion was a *paper* result, as corrected in [14-research-ledger-and-failure-lessons.md](14-research-ledger-and-failure-lessons.md).

This is a compact chronological handoff of the actual conversation so later agents understand why the current design exists. docs/00-conversation-context.md contains the fuller reconstruction.

## Turn 1
User asked whether a Groq/Grok-style bot could trade using their computer. Discussion separated Groq API from computer/browser agents and introduced API-first execution.

## Turn 2
User asked whether it could fully automate Groww intraday trading with access to the account money. Answer: technically automation is possible, but unrestricted AI money access is unsafe; use C# risk controls between model and broker.

## Turn 3
User proposed starting with ₹500 and trying to make roughly ₹5-₹10/day, and asked AI to design the strategy. Transaction-cost math showed ₹500 is too small for a useful intraday profitability test. Paper trading first was recommended.

## Turn 4
User asked whether Groq could simply use the browser instead of Groww API. Browser automation was considered technically possible but too fragile for primary live execution; broker API remained preferred.

## Turn 5
User asked for Reddit/internet evidence of people using Groww/Indian broker APIs, including profits and losses. Research found Groww/Dhan/other algo examples, large experimentation losses, accounting bugs, slippage/latency issues and SEBI evidence that most retail intraday traders lose.

## Turn 6
User asked which AI/model would be best, with a short ~20-minute daily trading approach, total cost and agent-harness ideas. Architecture shifted toward deterministic scanning plus selective AI calls, not AI watching every tick.

## Turn 7
User asked specifically about Astra. Astra was positioned as an expensive/selective senior reviewer and post-market analyst rather than a per-candle model.

## Turn 8
User proposed Groq + Astra + Sol + Codex daily runner, synchronized to plan/research and execute quickly, with about ₹5,000. Important correction: Groq does not provide Sol/Astra; use separate Groq and OpenAI providers behind one C# router.

## Turn 9
User clarified ₹5,000 is market capital only; infra is paid separately. User also proposed free Claude/other AI accounts and Codex/Groq for multi-source research, then using that daily research as live context. Final refinement: multiple models help research/disagreement detection, but source verification and deterministic market data remain authoritative.

## Turn 10
User provided github.com/dvajjala-ui/tradetest and requested the entire evolved idea, findings, user/assistant reasoning, architecture and development base be written into multiple Markdown files and pushed. This repository documentation is that handoff.

## Current project statement
Build a research-grade, cost-aware, fully logged .NET intraday trading platform that can eventually place tightly constrained Groww orders. AI assists research and candidate reasoning. Deterministic code owns safety, sizing, execution permissions, position state and reconciliation. Validate on paper/live data before using the ₹5,000 live account.
