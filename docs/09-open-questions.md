# Open Questions and Decisions

The expanded plan's current decision list is in [12-master-implementation-plan.md](12-master-implementation-plan.md). Items below are detailed verification tasks, not a reason to begin implementation before approval.

## Scope and capital
Confirm the earlier ₹5,000 restricted intraday live pilot or replace it; set a **separate** long-term portfolio allocation, confirm the proposed Nifty 500 TRI benchmark, maximum drawdown and level of manual buy approval. Confirm the name/link of the additional AI tool mentioned by the user. Determine whether this remains strictly a private self-directed tool; sharing signals or advice would need its own regulatory review.

## Groww
Current docs show key/secret with daily approval or TOTP, live feed, static-IP registration, rate limits, reference-ID lookup, and cash OCO. Verify these **in the authenticated account** and test duplicate-submission semantics, cash/MIS protection timing and exact contract-note charges; a reference ID is not itself proof of idempotency. See [broker report](13-broker-and-cost-report.md).

## Market data
The original three-month Groww intraday claim referred to the now-deprecated historical endpoint. Its [replacement](https://groww.in/trade-api/docs/curl/backtesting) documents data from 2020, checked 2026-10-09. Confirm actual account coverage and permitted use before licensing missing point-in-time history/corporate data; define candle aggregation, historical warm-up and corporate-action handling.

## Strategy parameters
Backtest exact opening-range duration, EMA periods, ATR period, volume-ratio definition, minimum R:R, watchlist universe, long-only vs later shorting, exact 20-minute window and whether to avoid the first minutes after open.

## Risk parameters
Empirically set max rupee risk/trade, daily loss, max position value, max spread, max slippage, candidate expiry and trade count. ₹5,000 capital does not mean ₹5,000 should be used per trade.

## Research
Decide which official sources can be automated reliably, whether V1 should be price/volume-only before adding news, and what qualifies as VERIFIED.

## Models
Recheck model IDs and token pricing at build time. First decide whether each model beats a rules-only or simpler-model baseline on a dated evaluation set; then define fallback, deadlines and escalation. Assess the user's additional named AI tool after its identity is known.

## Infrastructure
The user delegated this choice. [The current setup](24-setup-budget-and-learning.md) selects a small persistent Linux VM for unattended operation, with local offline testing and Vercel for the dashboard. PostgreSQL, notifications and secret-store extensions remain later decisions based on measured needs.

## Compliance
Before real deployment, verify current Indian regulations, exchange/broker terms, retail API/algo rules, static-IP requirements and any registration/approval obligations using current primary sources.

## Live activation checklist
Require explicit manual LIVE enablement after checking strategy version, risk policy, current research packet, broker auth, data health, zero unresolved positions/orders, kill switch, max capital and F&O disabled.
