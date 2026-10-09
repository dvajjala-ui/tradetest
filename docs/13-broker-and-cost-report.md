# Broker procurement and cost report

Checked 2026-10-09. Public list prices and features may change before purchase; taxes, account type, exchange, turnover and promotions affect the final bill. This is a planning estimate, not a quote. [Document 24](24-setup-budget-and-learning.md) selects the initial runtime and monthly budget.

## Broker shortlist

| Broker | Public API/data offer seen | Best reason to test | Gap to verify before selection |
| --- | --- | --- | --- |
| **Upstox** | Free read-only [Analytics Token](https://upstox.com/developer/api-documentation/analytics-token/); cannot place or modify orders | Initial no-order data integration | Actual coverage, licence and request limits; adapter remains to build |
| **Groww** | ₹499 + tax/month covers order, live and historical APIs; live feed and cash OCO documented. Replacement candles endpoint documents data from 2020. [API](https://groww.in/trade-api), [history](https://groww.in/trade-api/docs/curl/backtesting), [OCO](https://groww.in/trade-api/docs/curl/smart-orders) | Existing simulator charge assumptions; single subscription for a later pilot | Auth approval workflow, actual history/adjustments, real cash/MIS OCO behavior, reference retry semantics, feed freshness and fill quality |
| **Zerodha Kite Connect** | Personal order/portfolio tier free; Connect with real-time WebSocket and historical candles ₹500/month. [Pricing](https://zerodha.com/products/api/) | Mature .NET client and long-running ecosystem | Historical depth/licence, order controls, total charges, actual measured latency and account setup |
| **DhanHQ** | Trading API described as free; data subscription ₹499 + applicable taxes/month. [Access](https://dhan.co/support/platforms/dhanhq-api/how-to-access-dhan-api/), [pricing](https://dhan.co/pricing/) | Trading/data split and documented APIs | Data entitlement/history, smart-order protections, brokerage and authenticated behavior |
| **FYERS** | Trading API advertised free with live/historical data; C# listed, Prime for expanded access. Static IP needed for order placement. FYERS also advertises an account-context MCP integration. [API](https://fyers.in/products/api) | Low public API subscription cost and direct C# path | Confirm data limits/licence, Prime need, exact order controls, latency and MCP permissions; MCP gets read-only research access at most in V1 |

**Latest user choice: Groww primary data/planned execution plus Upstox read-only cross-checks**, initially on the laptop. The user accepts Groww's monthly API cost; no account purchase or credentials have been configured. Keep market-data and execution adapters separate and compare measured coverage, reliability and after-cost fills before live integration. Groww's replacement docs say history from 2020, while its subscription page still advertises three months; verify entitlement instead of resolving that discrepancy by assumption. [Updated setup and keys](25-local-dual-source-and-ai-setup.md).

### Steps to obtain Groww access, after plan approval

1. Confirm Groww account/KYC, API eligibility and current [subscription checkout](https://groww.in/trade-api). Buy **one monthly** subscription first; the current public offer is ₹499 plus tax, about **₹588.82/month at 18% GST**. Do not assume the previously noted annual price is still offered because the current public page reviewed here only states the monthly offer.
2. Provision a stable public IP through an ISP or VPS and whitelist it in the Groww API-key dashboard. Groww's own guide estimates an ISP static IP at ₹300–₹1,000/month or a VPS at $5–$50/month; obtain an actual vendor quote. [Groww static-IP setup](https://groww.in/blog/static-ip-api-trading-setup).
3. Generate an API key/secret or TOTP credential in Groww. The documented key/secret flow requires **daily approval**; the TOTP flow is another documented option. Choose a supported flow, store secrets outside Git, and test unattended startup without trying to bypass the intended approval. [Groww authentication and limits](https://groww.in/trade-api/docs/python-sdk).
4. Start with read-only calls: instrument master, historical bars, feed, orders/positions. Check the documented shared rate limits: orders 10/second and 250/minute; live REST data 10/second and 300/minute; non-trading 20/second and 500/minute. [Groww API limits](https://groww.in/trade-api/docs/python-sdk).
5. In PAPER/SHADOW, measure feed gaps, order-intent timestamps and reconciliation. Then manually authorize a minimal live order test and verify exact fees, protective order status, partial fills and reference lookup. A fresh go-live review is still required.

India's retail algo framework and broker procedures need a final check at this step. SEBI's [2025 circular](https://www.sebi.gov.in/legal/circulars/feb-2025/safer-participation-of-retail-investors-in-algorithmic-trading_91614.html), its [implementation extension](https://www.sebi.gov.in/legal/circulars/sep-2025/extension-of-timeline-for-implementation-of-sebi-circular-dated-february-04-2025-on-safer-participation-of-retail-investors-in-algorithmic-trading-_96979.html), [NSE implementation standards](https://nsearchives.nseindia.com/content/circulars/INVG67858.pdf), and the broker's current instructions govern the actual account. This plan assumes a private client-direct system; if the product later distributes trade signals or manages others' money, seek a separate regulatory review.

## AI unit prices and illustrative monthly usage

Current public **standard, short-context** prices per one million input/output tokens: [Groq GPT-OSS 120B](https://console.groq.com/docs/models) $0.15/$0.60; [OpenAI GPT-6 Luna, GPT-6.1 Sol and GPT-6 Astra](https://developers.openai.com/api/docs/pricing) $0.10/$0.50, $2/$10 and $10/$50 respectively. Cached, batch, long-context, tool, search, taxes and exchange-rate effects differ. The old flat ₹20–₹400/model/month guesses were not tied to measured usage; replace them with token accounting.

| Example workload for 20 sessions | Input/output per call | Monthly calls | Example token bill |
| --- | --- | ---: | ---: |
| Groq screening | 2,000 / 300 | 200 | $0.096 |
| Sol candidate review | 4,000 / 800 | 60 | $0.96 |
| Astra weekly/deep review | 10,000 / 2,000 | 8 | $1.60 |
| Sol company research | 20,000 / 2,000 | 50 | $3.00 |
| **Example subtotal** | | | **$5.656/month** |

This is only a transparent arithmetic scenario. Real reasoning/output tokens, document parsing, web search, embeddings, data feeds and developer time may dominate it. Put spend caps and usage logs on every provider. A large company corpus should be indexed once and retrieved selectively, not resent in every call. A pending extra AI tool gets a separate price, privacy and quality review before subscription.

## Trading-cost example and percentage hurdle

For an illustrative ₹5,000 **NSE intraday equity buy then sell at the same price**, Groww's current [pricing page](https://groww.in/pricing) lists brokerage of the lower of ₹20 or 0.1% per executed order, with ₹5 minimum, plus STT, stamp, exchange, SEBI and GST. Under its displayed rates:

| Component | Example round trip |
| --- | ---: |
| Brokerage: ₹5 buy + ₹5 sell | ₹10.00 |
| STT: 0.025% of sell | ₹1.25 |
| Stamp: 0.003% of buy | ₹0.15 |
| NSE exchange: 0.00297% on both sides | ₹0.297 |
| SEBI: 0.0001% on both sides | ₹0.010 |
| GST: 18% of brokerage + exchange + SEBI | ₹1.855 |
| **Illustrative total before spread/slippage and other applicable charges** | **₹13.56, or 0.271% of ₹5,000** |

At a 0.1% combined spread/slippage allowance (₹5), break-even gross price movement is about **0.371%** for this one example. A gross +1% move (₹50) nets about **₹31.44** after that assumed ₹18.56 execution cost; a gross −1% move (−₹50) nets about **−₹68.56**. The asymmetry is why return claims must state costs. Use actual contract notes and broker charge API where available, round as the broker does, include any levies omitted above, and recalculate for actual fills and changed prices. Delivery selling has different STT/DP charges, so lane B needs a separate calculator. Groww also lists a ₹50 auto square-off charge per position; the simulator must model that failure path. [Groww charges](https://groww.in/pricing).

## Budget envelope and return reporting

| Item | Initial planning treatment |
| --- | --- |
| Trading capital | Earlier ₹5,000 pilot assumption, pending reconfirmation; never pay infrastructure from it |
| Groww API | ₹588.82/month at current ₹499 + 18% GST; use monthly during the spike |
| Stable IP/host | ISP ₹300–₹1,000/month or VPS $5–$50/month from Groww's guide; obtain a real quote |
| AI API | Metered; illustrative $5.656/month workload above, not a ceiling |
| Historical/corporate data, news, storage | **Unquoted**; NSE has a tariff and contractual licence, so budget only after coverage and permitted-use review. [NSE tariff](https://www.nseindia.com/static/market-data/products-tariff) |
| Other broker account, research tools, engineer time | Track separately; no zero-cost assumption |

Report each month: gross trading return %, rupee trading charges, broker net return %, fixed project costs, project net rupees, drawdown %, benchmark excess return %, and sample size. On ₹5,000 capital, the Groww API fee alone is **11.78% of capital per month** (₹588.82/₹5,000), although it is an external R&D bill and does not change the strategy's trade-level edge. Spending more on research may improve evidence quality but cannot be translated into a reliable percentage uplift in return before controlled tests.
