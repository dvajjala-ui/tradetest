# Laptop runtime, free hosting, two data sources and AI keys

Checked 2026-10-09 (India). The user prefers running through the laptop and accepts Groww's approximately ₹500 monthly API plan, with free Upstox access as a second source. This updates the previous VM-first recommendation in document 24. It records the setup direction; no account, subscription, credential or trading connection has been created in the workspace.

## Selected starting arrangement

**Laptop engine + existing Vercel dashboard + Groww primary data + Upstox read-only cross-checks.** Groww is also the preferred future execution broker, subject to the existing paper/integration/live gates. Offline testing remains free of new service purchases. A paid or free VM is optional while testing is supervised; unattended hosting is a later reliability decision.

Run .NET 10 and the research database on the laptop's SSD. Use the existing operating system; Ubuntu 24.04 is not a prerequisite for this cross-platform .NET app. On Windows, WSL2 is an optional way to get Ubuntu; native .NET avoids needing that extra VM. Eight GB of laptop RAM is a practical starting recommendation, with 16 GB useful for concurrent browser, development and research work; these are capacity recommendations, not a measured minimum for every workload. The current Docker API configuration reserves at most two CPUs and 2 GiB, and serves synthetic reports only.

Local reads remove the browser-to-engine network round trip and avoid service cold starts. **Local is not proven fastest end to end:** feed delivery, ISP routing, packet loss, AI response time and broker acknowledgement dominate different parts of the path. Benchmark feed age, request/response p50/p95/p99 and disconnects on the actual laptop/network before selecting a server region. Indicators and risk calculations stay local; relevant context is prepared before decisions and optional model calls have bounded deadlines. Remote LLMs do not deliver microsecond responses.

For a supervised market session, keep the laptop plugged in, prevent sleep/hibernation/lid suspension, and keep the engine process running for the complete monitoring period. Startup must check clock, feed freshness, storage and authentication. Reconnects require reconciliation before new entries. Those market-session controls and a live-feed worker remain to implement and drill; an open dashboard alone does not provide them.

## Free hosting options

| Option | Current free offer / limit | Fit for TradeTest |
| --- | --- | --- |
| Local laptop | No new hosting bill; electricity, internet and backup storage remain costs | Selected for development and supervised paper testing |
| [Vercel](https://vercel.com/docs/functions/limitations) | Dashboard hosting under applicable plan limits; function execution is duration-bounded | Keep the static dashboard here; the present persistent .NET worker is not a Vercel function |
| [Render](https://render.com/docs/free) | Free web services sleep after 15 idle minutes and lose local changes on restart/redeploy/spin-down; no free persistent disk | Demo API experiments only; current SQLite/WAL accounting needs durable storage |
| [Railway](https://docs.railway.com/pricing/free-trial) | $5 trial for up to 30 days, then $1 monthly credit; anonymous VM trials have separate short limits and deletion | Temporary experiments; this is not an allowance for the recommended VM to run continuously |
| [Oracle Always Free](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm) | Current A1 allowance: 1,500 OCPU-hours and 9,000 GB-hours/month, equivalent to 2 OCPUs / 12 GB total; home-region capacity and idle reclamation restrictions | Actual Linux VM option for research if capacity is available; rebuild/test ARM64 dependencies and restore behavior first |
| [Google Cloud free tier](https://docs.cloud.google.com/free/docs/free-cloud-features) | Eligible `e2-micro` compute in selected US regions, with product-specific allowances | Small experiments; the eligible regions are not an India latency choice |

Oracle publishes an [Ubuntu 24.04 ARM64 image](https://docs.oracle.com/en-us/iaas/images/ubuntu-2404/canonical-ubuntu-24-04-aarch64-2026-02-28-0.htm), including Mumbai/Hyderabad image IDs. Select a currently eligible image/shape and check the console's allowances. The earlier 4-OCPU/24-GB claims still found online are not the allowance in the current Always Free document. The app has been container-tested on x64, not on an Oracle ARM VM. Availability, an idle instance being reclaimed and recovery from a backup are material to using it as a market-session host. No free cloud host has been provisioned or benchmarked.

## Groww plus Upstox

The current [Groww offer](https://groww.in/trade-api) is ₹499 plus taxes monthly, approximately ₹588.82 at 18% GST. The user accepts that cost. Confirm checkout and account API entitlement before activating it. The [key/secret authentication flow](https://groww.in/trade-api/docs/python-sdk) requires daily approval; a supported TOTP flow is also documented. Protect credential seeds and choose one supported flow for the authenticated spike.

For eventual Groww API orders, [Groww requires a whitelisted static public IP](https://groww.in/blog/static-ip-api-trading-setup). A laptop's private Wi-Fi address is not its internet egress address. Ask the ISP about static public IPv4 and CGNAT; obtain an account-specific quote. A dynamic home IP, dynamic DNS name or unregistered backup connection does not establish the required order origin. Read-only data behavior still needs authenticated testing; no order call is needed to test historical data.

Upstox's [Analytics Token](https://upstox.com/developer/api-documentation/analytics-token/) is free, read-only and valid for one year. Market/historical access needs no static IP; account/portfolio reads do. Use it as a secondary data source, with its actual limits and permitted use verified. It cannot place or modify orders.

The proposed two-source adapter must:

1. Map both providers to stable security IDs, exchange, segment and dated symbols. Normalize timezones and candle boundaries explicitly.
2. Preserve each raw response, provider timestamp, observation time and source hash separately. Compare the same venue, time window and price definition; LTP, bid/ask and adjusted candles are different observations.
3. Record agreement, disagreement, stale and unavailable states, using thresholds fixed after measurement. Never average differing prices into a fabricated market observation or silently replace the source during a replay/session.
4. Keep the primary strategy stream identifiable, schedule secondary checks within limits and define which checks are required for new entries. Required stale/missing evidence blocks a new entry; optional checks produce a clearly labeled degraded state. Broker outage and open-position recovery are separately handled.
5. Obtain authoritative company/exchange filings for corporate facts. Two brokers may distribute the same exchange data; they are not independent evidence that a corporate claim is true.

Groww's [replacement historical API](https://groww.in/trade-api/docs/curl/backtesting) documents data from 2020, while its subscription page still advertises up to three months. Record this inconsistency and test actual dates/intervals with the subscribed account before promising coverage. The old historical endpoint is deprecated. Neither broker adapter is implemented yet.

## AI providers and keys

Only **two AI-provider keys** are planned. One OpenAI project key can access the allowed models for that account; Sol, Luna and Astra do not require separate keys. Model access, quotas and billing are account-specific. The current app uses recorded offline responses, so adding a key today does not enable an adapter.

| Role | Planned model / tool | Credential | Initial use |
| --- | --- | --- | --- |
| Numeric features, sizing, fees, accounting and final risk | Deterministic .NET code | None | Existing baseline; no model call |
| Cheap extraction / evidence triage | Groq `openai/gpt-oss-120b`; evaluate `openai/gpt-oss-20b` separately if useful | `GROQ_API_KEY`, from [Groq keys](https://console.groq.com/keys) | Offline research first; a candidate filter enters only after an ablation |
| Company thesis and difficult evidence review | OpenAI `gpt-6.1-sol` | `OPENAI_API_KEY`, from [OpenAI keys](https://platform.openai.com/api-keys) | Main proposed research reviewer |
| Alternative cheap extraction | OpenAI `gpt-6-luna` | Same OpenAI key | Compare against Groq before selecting either; not an extra mandatory stage |
| Deep contradictions, weekly failure review | OpenAI `gpt-6-astra` | Same OpenAI key | Optional offline escalation |
| Code, tests and engineering review | Existing Codex; optional other assistants | Existing developer access | Outside market execution; no additional app key needed for this chat's work |

Current [Groq prices](https://console.groq.com/docs/models) per million input/output tokens are $0.15/$0.60 for GPT-OSS 120B and $0.075/$0.30 for 20B. A [free tier has rate limits](https://console.groq.com/docs/rate-limits); inspect the account's actual limits before automation. Current [OpenAI standard short-context prices](https://developers.openai.com/api/docs/pricing) are Luna $0.10/$0.50, Sol $2/$10 and Astra $10/$50. Long context, cache writes, tools and other service tiers differ. This plan uses API billing; [ChatGPT and API billing are separate](https://help.openai.com/en/articles/9039756-managing-billing-settings-on-chatgpt-web-and-platform).

Start with free Groq experiments where eligible and a **$20 total monthly AI planning allowance** if using paid calls. Implement usage accounting and provider limits before automated spend. The allowance is not an already enforced application cap. No role sends the entire corpus or every market tick to a model. Baseline comparisons, schema validation, citations, prompt/model/input hashes, candidate expiry and existing fail-closed gates remain mandatory.

## Credential checklist

| Service | What the user will supply privately | Current state |
| --- | --- | --- |
| Groww | Account/API entitlement and one supported authentication flow: key/secret with daily approval, or supported TOTP credentials | Cost accepted; no account token configured |
| Upstox | Account/KYC and Analytics Token | No account token configured |
| Groq | Project API key | Provider adapter not implemented |
| OpenAI | Project API key, enabled model access and API billing | Provider adapter not implemented |
| Local read-only API | Separate service token from `scripts/init-runtime-env.py` | Existing helper; not a broker or AI credential |

The uppercase AI names above are proposed adapter settings, not variables consumed by the current offline app. Keep secrets in an OS secret store or private ignored file available only to the backend; never chat, browser bundles, URL parameters or public Vercel variables. Groww credentials can authorize trading operations, so future data ingestion must expose only its approved data/auth endpoints and keep execution privileges isolated. A user does not need any provider key for today's synthetic dashboard or quick check.

## Context collection and the research book

The planned “context gainer” is **our ingestion, evidence store and retrieval pipeline**, not another mandatory subscription. Existing dated SQLite facts/metrics, hashes, corrections, full-text search and as-of report exports are the foundation. Planned source adapters collect permitted company/exchange/regulator material and broker records; quality checks quarantine failures. Models may extract claim candidates, with numeric/source verification before promotion. Reddit/blogs can suggest questions and operational lessons; they do not establish verified financial facts.

Index records by company/security, sector, topic, event, source version and first-known time. Retain contradictory evidence and failed experiments. Retrieve a small cited packet per question/decision, cache by data/model/prompt versions and expire it appropriately. Begin with existing full-text search; add local embeddings or a search API only if measured retrieval coverage warrants it. No vector-database subscription, paid web-search account or full model fine-tuning is required for the initial book. Historical model knowledge can contain future outcomes, so prospective frozen-version paper testing remains essential.

Continuous source collection, model extraction, a scheduled worker, two-provider reconciliation, actual model requests and automatic experiment/incident indexing are still planned. More stored context supplies evidence to retrieve; it does not automatically train a model or establish profitability.

## Revised monthly budget and next milestone

| Item | Laptop-first allowance |
| --- | ---: |
| Additional VM bill | ₹0 |
| Groww API | ₹499 plus taxes; ₹588.82 if 18% GST applies |
| Upstox Analytics Token API fee | ₹0 |
| Eligible personal Vercel dashboard | $0 within applicable allowances |
| AI experiments | Free Groq quotas where eligible; optional $20 total paid-usage allowance |
| Electricity/internet, backups, static IP, licensed datasets/news and trading charges | Separate; actual amounts not yet quoted |

The prior $31.20 VM-plus-backups allowance applies only if choosing the paid VM fallback; add broker and AI costs to that alternative. No real trading capital is needed for validation. The next implementation milestone is one no-order Groww data adapter, then Upstox comparison on matching observations, a data-health report and real-data paper sessions. Keep actual model adapters and context collection independently testable. This is a staged implementation, not an already connected two-broker or self-learning product.
