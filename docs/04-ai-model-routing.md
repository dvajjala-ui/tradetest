# AI Model Routing

> Original model-routing hypothesis. The current proposal starts with a rules-only baseline and adds a model only when controlled evaluation supports it. See [12-master-implementation-plan.md](12-master-implementation-plan.md) and [14-research-ledger-and-failure-lessons.md](14-research-ledger-and-failure-lessons.md).

## Principle
Use exact C# computation first, then progressively more capable AI only when additional reasoning is useful. Never send every tick/candle to an LLM.

## Roles

### C# scanner
Computes VWAP, EMA, ATR, volume ratio, opening range, previous-day levels, reward/risk, spread, session time, P&L and all hard numeric checks. This layer eliminates most observations at zero AI cost.

### Groq / GPT-OSS fast filter
Cheap/fast first-pass review. Output only REJECT, CONTINUE, WAIT or ESCALATE plus reason codes. It has no broker tools.

### GPT-6 Sol primary reviewer
Receives only qualified candidates, exact market metrics and the verified daily research packet. Returns TAKE, REJECT, WAIT or ESCALATE with supporting factors, contradicting factors and risk flags.

### GPT-6 Astra deep reviewer
Called only on ambiguous/high-value cases: model disagreement, unusual regime, verified material news, conflicting stock/index signals or weekly post-market analysis. Astra still has no execution authority.

### Codex
Use for code review, log analysis, backtests, test generation, bug finding, daily/weekly technical analysis and strategy hypothesis generation. It must not be required to manage an open live position.

### Claude / Gemini / other AI
Useful for independent pre-market research and post-market review. Consumer/free chat sessions should not be a critical live dependency; use supported APIs for automation that must be reliable.

## No majority-vote BUY
Do not treat 3 AIs saying BUY as proof. Correlated models can repeat the same bad premise. Multiple models are for disagreement detection and research diversity, not naive voting.

## Fact vs metric vs judgement
FACT: source-backed external statement. DERIVED METRIC: computed by C#. MODEL JUDGEMENT: qualitative assessment such as breakout quality. Persist these separately.

## Failure policy
If fast model fails, optionally skip to Sol. If required Sol fails, reject. If Astra is required for an escalation and fails, reject. If a candidate becomes stale while waiting for AI, reject it.

## Cost/latency control
Use compact JSON, cached daily research, hard token limits, deterministic prefilters and no AI for ordinary position monitoring. A normal day should mean many code checks, a handful of Groq calls, perhaps 1-3 Sol calls and often zero Astra calls.

## Versioning
Persist provider, model ID/version, prompt version, reasoning setting, token usage, latency, estimated cost and outcome so model combinations can be compared from evidence.
