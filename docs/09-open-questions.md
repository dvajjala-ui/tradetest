# Open Questions and Decisions

## Groww
Verify current auth flow, live-feed capabilities, static-IP registration, rate limits, idempotency/client-order support, smart-order/OCO support, and exact current charges.

## Market data
Decide whether Groww alone is sufficient or a secondary source is needed; define candle aggregation, historical warm-up and corporate-action handling.

## Strategy parameters
Backtest exact opening-range duration, EMA periods, ATR period, volume-ratio definition, minimum R:R, watchlist universe, long-only vs later shorting, exact 20-minute window and whether to avoid the first minutes after open.

## Risk parameters
Empirically set max rupee risk/trade, daily loss, max position value, max spread, max slippage, candidate expiry and trade count. ₹5,000 capital does not mean ₹5,000 should be used per trade.

## Research
Decide which official sources can be automated reliably, whether V1 should be price/volume-only before adding news, and what qualifies as VERIFIED.

## Models
Verify exact current Groq/OpenAI model IDs and pricing at build time. Decide fast-model fallback, Astra trigger bands and whether material model disagreement automatically rejects.

## Infrastructure
Choose local PC + ISP static IP vs VPS, Windows vs Linux, SQLite vs PostgreSQL later, notifications and secret storage.

## Compliance
Before real deployment, verify current Indian regulations, exchange/broker terms, retail API/algo rules, static-IP requirements and any registration/approval obligations using current primary sources.

## Live activation checklist
Require explicit manual LIVE enablement after checking strategy version, risk policy, current research packet, broker auth, data health, zero unresolved positions/orders, kill switch, max capital and F&O disabled.