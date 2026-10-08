# Cost Model

## Capital separation
₹5,000 is reserved as trading capital. Infrastructure/API/AI costs are paid separately and must be tracked separately from broker P&L.

## Groww API snapshot
During planning, Groww pricing was referenced as ₹499 + tax/month or ₹4,999/year before tax. At 18% GST that is about ₹589/month, or about ₹492/month when the annual plan is averaged. Re-check before purchase.

## Static IP / hosting
Groww API order placement was discussed as requiring a whitelisted static IP. Planning ranges: ISP static IP roughly ₹300-₹1,000/month, or a small VPS roughly ₹500-₹1,000/month. No GPU is required.

## AI planning ranges
Groq GPT-OSS roughly ₹20-₹100/month; GPT-6 Sol roughly ₹50-₹200; GPT-6 Astra roughly ₹100-₹400; Claude/Gemini/Codex may initially use existing/free allowances for research. These are budgeting ranges, not guarantees.

## Likely monthly infrastructure
Lean VPS setup: Groww ~₹492 averaged + VPS ₹500-₹1,000 + Groq ₹20-₹100 + Sol ₹50-₹200 + Astra ₹100-₹400 + storage/monitoring ₹0-₹100 = approximately ₹1,160-₹2,300/month.

## Trading costs
The discussion referenced Groww equity intraday brokerage around ₹20 or 0.1% per executed order, whichever is lower, with a minimum around ₹5. Small trades may therefore pay about ₹5 entry + ₹5 exit before other statutory charges, spread and slippage.

## Economics
Broker performance = broker net P&L after trading costs. Project economics = broker net P&L - API subscription - hosting/static IP - AI APIs - other services. With only ₹5,000 capital, the trading strategy could be positive while the total R&D project remains negative because fixed infra is large relative to capital.

## Cost telemetry
Track per-model tokens/cost plus daily gross trading P&L, trading charges, net trading P&L, AI cost and allocated infrastructure cost. Do not confuse profitable trades with profitable project economics.