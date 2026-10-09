# G0 source register and data-quality contract

Checked 2026-10-09. `READY` means the published source can be reviewed; it does **not** mean TradeTest has an authenticated entitlement, redistribution licence, or a complete historical dataset. No bulk market data is bundled in this repository.

| Source | Intended information | Published coverage/access | Permitted-use status for this project | Implementation decision |
| --- | --- | --- | --- | --- |
| [Upstox Analytics Token](https://upstox.com/developer/api-documentation/analytics-token/) | Read-only market and historical data | Free account token; actual entitlement/coverage and usage terms to verify. | **NOT ENTITLED** in this workspace. | First proposed no-order data adapter; not implemented. |
| [Groww Trading API](https://groww.in/trade-api/docs/python-sdk) | Quotes/feed, instrument master, historical candles, orders and positions | Active paid subscription and account auth needed. [Replacement historical endpoint: documented from 2020](https://groww.in/trade-api/docs/curl/backtesting); old three-month intraday endpoint is deprecated. | **NOT ENTITLED** in this workspace; API terms and actual account access to verify before use. | Existing simulator charges are Groww assumptions; broker adapter not implemented. |
| [NSE EOD/historical product](https://www.nseindia.com/static/market-data/eod-historical-data-subscription) | Longer price/volume history and cross-check | Subscription/quote required for paid files. | **LICENCE REVIEW REQUIRED** under the [NSE data policy](https://www.nseindia.com/static/market-data/nse-data-policy). | Request a quote and permitted-use terms if Groww history is insufficient; no scraper. |
| [NSE corporate data](https://www.nseindia.com/static/market-data/corporate-data-subscription) | Filings, shareholding and fundamentals | Paid product or individually published filings. | **LICENCE REVIEW REQUIRED** for systematic ingestion. | Define document adapter, provenance and manual sample input before subscription. |
| [SEBI circulars and research](https://www.sebi.gov.in/) | Regulatory rules and historical studies | Public documents. | **PUBLIC REVIEW ONLY**; check usage terms before systematic replication. | Record exact document URL, publication date and revision; never use as a price feed. |
| [NSE Indices](https://www.nseindia.com/static/products-services/indices-total-returns-index) | Long-term total-return benchmark | Public methodology; historical series may need licence. | **BENCHMARK LICENCE TO VERIFY** before automated history. | Nifty 500 TRI is the proposed research benchmark; actual investable ETF/fund result is separate. |
| Company investor-relations documents | Annual/quarterly reports and presentations | Company-by-company URLs and publication timing. | **SITE TERMS TO VERIFY**; a public PDF does not imply unrestricted bulk copying. | Manual/approved document import initially; source timestamp, hash, ISIN and correction chain required. |
| News/community sources | Material event cross-checks and operational anecdotes | Source-specific. | **UNVERIFIED/UNLICENSED** until contract and provenance established. | Never promote to verified fact from model output or votes. |

## Point-in-time records

Every raw item has `source_id`, `document_id`, `source_url`, `publisher`, `published_at`, `effective_at`, `first_known_at`, `retrieved_at`, `content_sha256`, `licence_id`, `parser_version`, `supersedes_id` and an immutable raw copy where permitted. A derived metric additionally carries `algorithm_version` and the source fact IDs. Keep all timestamps UTC in storage; display trading-session times in Asia/Kolkata. A historical decision may see only items with `first_known_at <= decision_time`.

## Data-quality checks

1. Reject duplicate event IDs, non-finite or negative prices, invalid OHLC ordering, non-monotonic timestamps, unknown instrument IDs and rows outside the declared session calendar.
2. Flag missing bars, cross-source price divergence, large gaps, stale bid/ask, feed reconnects, inconsistent adjusted/unadjusted series and unexpectedly changed share counts. Never silently forward-fill a missing tradable price.
3. Corporate actions and symbol changes produce a dated mapping; historical raw prices remain unchanged, while derived total-return/adjusted series declare their method and version.
4. Filings can be revised or withdrawn. New versions supersede old ones from their actual first-known time; do not rewrite a backtest's historical knowledge.
5. Quarantine a symbol, day, or source when quality fails. Emit a health event and prevent a new order that depends on it.

## Acquisition gate

Before claiming a robust historical result, record the exact licensed dataset, vintage, universe (including delisted names), missing-data rate, adjustment method, and total price. The G0–G4 code can be tested on **clearly synthetic fixtures** and authorized small manual samples. Purchasing a data subscription, storing credentials or placing orders is deferred to a separate reviewed step.
