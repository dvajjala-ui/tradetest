# System Architecture

> Original intraday architecture. The proposed two-lane architecture, dated research store and broker safety details are in [12-master-implementation-plan.md](12-master-implementation-plan.md).

## Design objective

Build a fail-closed, auditable, provider-independent intraday trading platform where AI assists research and candidate assessment but deterministic .NET code owns state, safety, risk and order execution.

## High-level flow

Market research -> verified daily research packet -> Groww live data -> C# indicators/scanner -> Groq fast filter -> GPT-6 Sol review -> optional GPT-6 Astra escalation -> C# RiskEngine -> Groww order API -> deterministic position supervisor -> reconciliation/logging.

## Core boundary

AI never calls Groww directly. Models return structured trade assessments only. The RiskEngine is the final authority and the ExecutionService is the only component allowed to submit broker orders.

## Operating modes

- OFF: no market actions.
- RESEARCH: collect/prepare research only.
- PAPER: live market pipeline with simulated execution.
- SHADOW: generate intended orders but do not submit them.
- LIVE: real orders permitted after explicit startup/risk checks.

Default startup must be OFF or PAPER, never LIVE.

## Candidate lifecycle

Detected -> RuleQualified -> FastModelReviewed -> MainModelReviewed -> OptionalDeepReview -> RiskApproved/RiskRejected -> Submitted -> Accepted/Rejected -> Filled/PartiallyFilled -> PositionOpen -> Closed -> Reconciled.

## Fail-closed behavior

- stale market data: no new entry
- Sol unavailable: reject new AI-dependent candidate
- Astra required but unavailable: reject escalation
- uncertain broker order state: reconcile before retry
- state/database failure: no new entry
- open-position safety must not depend on any LLM

## Idempotency

Persist order intent before submission. Include SessionId, CandidateId, StrategyVersion, Symbol, Side and sequence. On timeout, query broker state first; never assume timeout means the order failed.

## Auditability

Persist code version, strategy version, risk-policy version, model/provider, prompt version, research-packet hash, candidate snapshot, model response, risk decision, broker request/response, fills, exit reason, costs and final P&L.
