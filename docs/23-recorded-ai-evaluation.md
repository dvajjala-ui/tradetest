# Recorded AI assessments and evaluation

Checked 2026-10-09. This increment implements an offline contract harness from the approved G0–G4 plan. It replays frozen JSON responses; it does not contact Groq, OpenAI, a broker or the user's unnamed additional tool. The dashboard's **Strategy studies** view shows the synthetic cases and can export their evaluation.

## Reproduce it

From the repository root, using .NET 10:

~~~bash
dotnet build TradeTest.slnx -c Release
dotnet run --no-build -c Release --project src/TradeTest.Cli -- prepare-ai fixtures/synthetic-ai-cases.json
dotnet run --no-build -c Release --project src/TradeTest.Cli -- evaluate-ai fixtures/synthetic-ai-cases.json fixtures/synthetic-ai-recordings.json
dotnet test TradeTest.slnx -c Release --no-build
~~~

`prepare-ai` prints the provider requests and blocked-case statuses. `evaluate-ai` loads the separate recordings, checks their context binding, and compares their accepted continuations with the supplied rules-eligible candidates. Both commands perform local computation only. Inputs use strict camelCase JSON; unknown and duplicate properties are rejected. Files are limited to 8 MiB and a dataset to 1,000 unique chronological cases.

## Evidence and privilege boundary

The context contains a stable company ID, case/strategy/model/prompt versions, decision/review/expiry times, frozen bounds, and dated claims with source URL, publisher, publication time, content hash, licence ID and parser version. Outcomes never enter the provider request. Source hashes and correction histories are validated before selecting facts; future, unverified, stale and withdrawn facts are excluded. Source URLs containing account credentials are rejected. Timestamps in the context are normalized to UTC and facts are sorted by ID.

`contextHash` covers the exact UTF-8 context JSON, including the system prompt and policy. A changed model, strategy, prompt, source content, date or bound requires a new recording. The report also hashes the full dataset, including later outcomes, and the sorted recordings. These hashes establish reproducibility, not a source's truth or a digital signature.

The implemented [assessment schema](../contracts/ai-assessment-v1.schema.json) permits Continue, Reject, Wait and Escalate. It has no quantity, allocation, broker command, free-form factual summary or confidence/profit percentage. The .NET validator additionally checks exact identity/version/hash matches, active citation IDs, disjoint support/counterevidence lists and expiry. Continue needs cited support, the SupportsBaseline reason and no missing evidence. Source instructions remain untrusted data; a recorded test cannot establish a real model's resistance to prompt injection.

The gate skips rules-blocked, expired or unusable contexts without calling the provider interface. Invalid responses, missing recordings or usage, token-limit violations, provider failures and asynchronous timeouts cannot continue. Cancellation propagates; an asynchronous provider that ignores cancellation is still bounded by the gate's wait. A future adapter must return its task promptly: the deadline does not preempt arbitrary synchronous work inside an implementation. Candidate expiry is checked against the dataset's declared historical review time; no live-clock integration is claimed.

Continue permits **further deterministic review**, never an order or risk approval. The harness is not inserted into the existing replay, risk or position-management path. An open position therefore has no new provider dependency.

## What the example establishes

The ten invented cases include an accepted winning case, an accepted losing case, a rejection, a wait, a fabricated citation, an unauthorized quantity field, expired and unverified evidence, a rules-blocked case and a missing recording. Two continuations pass; two malformed responses are blocked; the missing recording has unknown usage metadata. Tests also cover future/corrected/stale evidence, altered versions and hashes, nulls/duplicates, missing fields, expired responses, oversized output, cancellation and a late provider response.

The rules-eligible candidate totals are **−₹10**, and the recorded-filter total is **₹40**, using the supplied trading costs. These deliberately chosen independent outcomes verify arithmetic. They are not portfolio returns, a market experiment, a win-rate estimate or evidence of AI uplift. A filtered candidate stream would need a full cash/position/execution replay before a portfolio comparison.

The **$0.012** and **3,000 input / 600 output tokens** are invented recording metadata, not bills. The report preserves known charges for invalid responses and counts unknown-charge calls rather than treating them as free. USD inference charges remain separate from rupee trading P&L; the reported difference is explicitly before inference costs. The application made zero external inference calls in this increment. No latency percentile or calibrated probability is calculated from the recordings.

The suite passes **81 .NET tests and eight web tests**. The browser panel validates case identities, continuation state, token/charge aggregates and candidate totals before replacing a report. Its displayed rows are capped at 50; export retains all cases. The public exporter uses only explicitly named synthetic fixtures.

## Remaining evidence

Actual provider adapters, vendor-confirmed token billing, measured p95/p99 latency, a registered model/prompt trial, independent citation relevance checks, licensed historical cases and full portfolio ablations remain pending. ImportedRecords is a user-declared label, not a verification of data quality or licence rights. A real model is promoted only after the same-case comparison and operational gates in [the master plan](12-master-implementation-plan.md). The additional AI tool still needs its exact name and capabilities.
