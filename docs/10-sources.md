# Source Links Referenced During Planning

Current, evaluated links and their implications are in [12-master-implementation-plan.md](12-master-implementation-plan.md), [13-broker-and-cost-report.md](13-broker-and-cost-report.md) and [14-research-ledger-and-failure-lessons.md](14-research-ledger-and-failure-lessons.md). The links below include sources from the original conversation and are not all current price/entitlement evidence.

## Additional primary sources checked 2026-10-08
- Groww historical intervals: https://groww.in/trade-api/docs/curl/historical-data
- Groww feed: https://groww.in/trade-api/docs/python-sdk/feed
- Groww auth and rate limits: https://groww.in/trade-api/docs/python-sdk
- Zerodha API tiers: https://zerodha.com/products/api/
- Dhan data API pricing: https://dhan.co/support/platforms/dhanhq-api/how-to-access-dhan-api/
- FYERS API tiers, C# and MCP: https://fyers.in/products/api
- NSE data sharing policy: https://www.nseindia.com/static/market-data/nse-data-policy
- NSE market-data tariff: https://www.nseindia.com/static/market-data/products-tariff
- NSE historical/EOD products: https://www.nseindia.com/static/market-data/eod-historical-data-subscription
- NSE corporate data: https://www.nseindia.com/static/market-data/corporate-data-subscription
- NSE total returns index methodology: https://www.nseindia.com/static/products-services/indices-total-returns-index
- Nifty 500 index description: https://www.niftyindices.com/indices/equity/broad-based-indices/nifty-500
- SEBI retail algo framework: https://www.sebi.gov.in/legal/circulars/feb-2025/safer-participation-of-retail-investors-in-algorithmic-trading_91614.html
- SEBI extension: https://www.sebi.gov.in/legal/circulars/sep-2025/extension-of-timeline-for-implementation-of-sebi-circular-dated-february-04-2025-on-safer-participation-of-retail-investors-in-algorithmic-trading-_96979.html
- SEBI SME investor advisory: https://www.sebi.gov.in/media-and-notifications/press-releases/aug-2024/advisory-regarding-investment-in-securities-of-the-companies-listed-on-the-sme-segment-of-stock-exchanges_86205.html
- Published stock-return paper: https://asu.elsevierpure.com/en/publications/do-stocks-outperform-treasury-bills/
- Published backtest-overfitting paper: https://escholarship.org/uc/item/4w1110bb
- OpenAI current API prices: https://developers.openai.com/api/docs/pricing

Time-sensitive pages must be re-verified before implementation.

## Groww
- https://groww.in/trade-api
- https://groww.in/trade-api/docs
- https://groww.in/trade-api/docs/python-sdk
- https://groww.in/trade-api/docs/curl/orders
- https://groww.in/trade-api/docs/curl/smart-orders
- https://groww.in/blog/api-trading-on-groww
- https://groww.in/blog/static-ip-api-trading-setup
- https://groww.in/pricing
- https://groww.in/help/stocks/sx-pricing/what-are-intraday-charges

## Groq
- https://console.groq.com/docs/models
- https://console.groq.com/docs/model/openai/gpt-oss-120b
- https://console.groq.com/docs/tool-use/overview
- https://console.groq.com/docs/tool-use/built-in-tools/browser-automation

## OpenAI
- https://developers.openai.com/api/docs/models
- https://developers.openai.com/api/docs/pricing

## Anthropic
- https://support.anthropic.com/en/articles/11049762-choosing-a-claude-ai-plan

## SEBI
- https://www.sebi.gov.in/media-and-notifications/press-releases/jul-2024/sebi-study-finds-that-7-out-of-10-individual-intraday-traders-in-equity-cash-segment-make-losses_84948.html
- https://www.sebi.gov.in/reports-and-statistics/research/aug-2026/study-profitability-of-individual-traders-in-the-equity-derivatives-segment-fy25-fy26-_103835.html

## Reddit examples
- https://www.reddit.com/r/IndiaAlgoTrading/comments/1w1qbx3/finally_cracked_it_4_years_of_failure_to_success/
- https://www.reddit.com/r/IndiaAlgoTrading/comments/1udf1qv/day_0/
- https://www.reddit.com/r/IndiaAlgoTrading/comments/1uedlwj/day_1_more_info_on_my_profile/
- https://www.reddit.com/r/BhartiyaStockMarket/comments/1tfobo7/algo_trading_in_india_if_backtests_work_but_live/

## Source policy
Prefer official primary documentation; verify date; treat Reddit as anecdotal operational evidence; never use an LLM answer alone as a factual market/broker/API source.
