# Setup, budget and continuous-learning plan

Checked 2026-10-09. The user delegated the computer, broker and hosting recommendation. This document selects the initial research setup; it does not purchase an account or activate trading. [Current implementation status](16-implementation-status.md) remains the readiness record.

Latest user update: [document 25](25-local-dual-source-and-ai-setup.md) selects supervised laptop testing, Groww primary data and Upstox cross-checks. The VM budget below is now an optional unattended-hosting alternative. The offline helper, Compose instructions and learning protocol remain applicable.

## Selected setup

Use the existing **Vercel dashboard** and the **laptop for initial supervised testing**. If unattended hosting is later needed, a persistent Ubuntu 24.04 VM with 2 vCPUs and 4 GiB RAM is the paid alternative; DigitalOcean Bengaluru is one region to benchmark. Compare feed freshness and network tail latency against the laptop before treating any region as optimal. Groww primary data and Upstox read-only cross-checks are the latest source choices.

Use a normal VM and Docker Compose. Managing microVM infrastructure, a GPU, Kubernetes or distributed queues adds no demonstrated benefit to the present single-worker workload. Start with .NET, SQLite WAL/FTS5 and bounded in-memory caches. Move storage or services only after measurements show a bottleneck.

Upstox's [Analytics Token](https://upstox.com/developer/api-documentation/analytics-token/) is free, valid for one year and read-only. Market/historical GET access needs no static IP; account/portfolio reads do. The token cannot submit or modify orders. **Its adapter is not implemented**, and actual data coverage and permitted use must be checked with the account. Existing simulated charges still use Groww assumptions.

## Optional paid VM budget and initial capital

| Item | Initial monthly allowance | Basis |
| --- | ---: | --- |
| Basic VM: 2 vCPU, 4 GiB RAM, 80 GiB disk | **$24** | [DigitalOcean published size](https://www.digitalocean.com/pricing/droplets) |
| Daily VM backups | **$7.20** | [30% of VM price](https://docs.digitalocean.com/products/backups/details/pricing/) |
| Upstox Analytics Token | **₹0 API fee** | Account charges and data-use rights are separate |
| Vercel Hobby dashboard | **$0 if eligible** | [Personal/non-commercial use and allowances](https://vercel.com/docs/plans/hobby) |
| Optional AI experiments | **$20 budget limit** | An allowance, not a subscription price or measured usage |
| **VM plus daily backups** | **$31.20** | Before taxes, currency conversion and extra usage |
| **VM, backups and full AI allowance** | **$51.20** | Same exclusions; licensed datasets/news are not included |

This finalizes the **initial budget**, not an all-inclusive invoice. Historical and corporate-data licence quotes remain unknown. Commercial hosting would add [Vercel Pro's $20 base plus usage](https://vercel.com/docs/plans/pro-plan); it is not needed for the eligible private prototype. Do not buy paid AI tools or a second broker subscription before a benchmark shows their value. Provider-side limits and application accounting both need implementation before automated paid calls; the $20 row is not an already enforced app limit.

With the latest two-source choice, this table is only the optional hosting/AI subtotal: add Groww's subscription if enabled. The current laptop-first budget is in document 25.

The user now accepts Groww as the primary source at [₹499 plus taxes/month](https://groww.in/trade-api), or ₹588.82 if checkout applies 18% GST. Its newer [historical candles endpoint](https://groww.in/trade-api/docs/curl/backtesting) documents data from 2020, while the subscription page still advertises up to three months. Test actual entitlement, completeness, adjustments, delisted coverage and rights before promising depth. Execution integration retains its evidence gates.

**Put ₹0 of real trading capital into this app while validating it.** The ₹5,000 examples are virtual research settings. The initial cash commitment is the VM bill if chosen; local offline testing needs no new service purchase. Long-term portfolio capital is a separate decision based on the owner's finances, horizon and loss tolerance. Infrastructure spending and a positive synthetic report do not justify a portfolio allocation.

## What the user needs to do

1. **Test now:** open [the dashboard](https://tradetest-dashboard.vercel.app/#studies). It needs no broker login. For fresh local reports, install the .NET 10 SDK and Python 3, clone this repository and run `python3 scripts/run-offline-check.py` at its root. Build dependency downloads may use the network; the workflows make no broker or model API calls.
2. **For unattended operation:** create the VM above with an SSH key and daily backups. Install Git, Python 3 and Docker with the Compose plugin. Keep SSH restricted to the operator. No public API port is needed for the initial setup.
3. **For real-data work:** prepare Groww API entitlement and a supported authentication flow, plus an Upstox account/Analytics Token for cross-checks. [Document 25](25-local-dual-source-and-ai-setup.md) maps these credentials and the two AI-provider keys. Keep them in a private secret store; account access alone does not connect the current app.
4. **Optional deployment convenience:** connect `dvajjala-ui/tradetest` in [Vercel Git settings](https://vercel.com/dvajjala-2765s-projects/tradetest-dashboard/settings/git) and verify push-triggered deployment. Manual deployment already works; this is independent of broker setup.

The VM's role today is to host the read-only demo API. A scheduled ingestion/paper worker, real-data adapter, remote HTTPS access and restore drills remain to build and verify.

## Repeatable local or VM runtime

From the repository root:

~~~bash
python3 scripts/init-runtime-env.py
docker compose --env-file deploy/.env -f deploy/compose.yaml up -d --build
curl http://127.0.0.1:5080/health
~~~

The generator creates a random API token in ignored `deploy/.env`, with owner-only POSIX permissions, and refuses to overwrite it. This is a service token, not a broker credential. Do not print resolved Compose configuration into logs: it contains that token. The container runs as non-root with a read-only root filesystem, bounded memory/CPU/logs, dropped capabilities and restart policy. Its port is bound to **127.0.0.1**, and `/api/*` requires the bearer token. The initial Compose service mounts no private database and performs no ingestion or order execution.

For a VM, forward the private port to your computer:

~~~bash
ssh -N -L 5080:127.0.0.1:5080 your-user@your-vm
~~~

Use the local dashboard (`cd web`, `npm ci`, `npm run dev`) and its Deployment view to read the forwarded API, entering the service token locally. A hosted dashboard reaching loopback depends on browser permission. Remote HTTPS/private networking is a later setup task; do not expose plain HTTP by changing the Compose bind address. To stop this service, use `docker compose --env-file deploy/.env -f deploy/compose.yaml down`.

The offline check creates a fresh ignored `artifacts/offline-check-*` directory with walk-forward, long-term, total-return, recorded AI, company and dashboard reports, an idempotent import check and persisted replay journal. `manifest.json` records report hashes and synthetic/offline limitations. Open its `company.json` in Company research. `--no-build` reuses an existing Release CLI build. These checks are a quick usability/integration test, not market evidence.

Verified in the workspace on 2026-10-09: the helper completed and produced 14 hashed JSON reports; the Compose image built and started as UID 1654 with read-only root, a 2-GiB memory limit and two-CPU limit. The API returned OFFLINE with no broker, rejected an unauthenticated report read with 401, allowed an authenticated read with 200 and an ETag re-read with 304. The token file had mode 0600 and a second generation attempt left it untouched. Only the test Compose project was stopped afterward. This verifies the container configuration in this environment, not operation on a purchased VM.

## The indexed research book

The existing foundation is dated facts/metrics, source hashes and corrections, SQLite full-text search, consistent as-of company snapshots and private export. Extend it with these separately indexed records:

| Index | What it preserves |
| --- | --- |
| Company/security | Stable ID/ISIN, dated symbols, filings, financials, dilution, governance, thesis and invalidating evidence |
| Sector/event | Peers, macro events, market regimes, cross-company exposures and publication delays |
| Sources/corrections | Publisher, first-known time, licence, raw hash, parser version, replacement/withdrawal and contradictions |
| Experiments | Hypothesis, data/model/prompt/code versions, parameters, evaluation dates, costs and every failed variant |
| Incidents/lessons | Symptom, root cause, affected period, corrective change, reproduction and regression evidence |

Numeric features stay in typed tables. Retrieval uses security/topic/time filters before full-text search, then bounded source excerpts with citations. Cache keys include source and parser versions; corrections invalidate dependent packets. Benchmark embeddings against this baseline before adding another service. The book can grow while each decision stays small and fast. Storing more material supplies retrieval context; it does not automatically retrain a model or improve predictions.

## How learning and testing will work

1. **Acquire and reconcile real data.** Implement no-order Groww ingestion, then Upstox cross-checks, with explicit timezone/instrument mapping, request budgets, token expiry, gaps and raw response hashes. Start with one liquid cash-equity intraday stream because the current replay engine handles one instrument per session. Build the company corpus separately, initially around 50 predeclared companies, with permitted dated filings and historical universe/actions including failures and delistings. Confirm actual coverage before running historical claims.
2. **Freeze baselines before tests.** Register rules, costs, universe, model/prompt, source vintage and metrics. Use chronological walk-forward and an untouched final holdout, identical candidate snapshots and conservative spread/slippage. Compare rules-only, no-trade and relevant passive total-return baselines. The current walk-forward tool partitions fixed rules; it does not fit ML parameters. Adaptive fitting, label-overlap purging/embargo where needed and a failed-trial registry remain to implement.
3. **Treat LLM historical results cautiously.** Blinding supplied later outcomes does not remove future events memorized during pretraining. [Glasserman/Lin](https://arxiv.org/abs/2309.17322) and [Lopez-Lira/Tang/Zhu](https://arxiv.org/abs/2504.14765) examine this contamination. Masking names/dates or asking a model to act as if it were in the past cannot establish an unbiased test. Prefer prospective paper/shadow comparisons after freezing the model version. The existing recorded harness validates contracts and arithmetic, not model forecasting skill.
4. **Separate knowledge updates from strategy changes.** Proposed daily source jobs append dated evidence and quarantine failures. Proposed weekly reviews diagnose drift, propose hypotheses and run challengers beside a frozen baseline. New evidence may update research packets; changing a prompt, threshold, risk rule or portfolio policy creates a new experiment and requires validation. Thirty paper sessions is an engineering floor, not proof of statistical edge.
5. **Make software evolution reviewable.** AI can suggest a patch and prepare an isolated build/test/replay comparison with resource, latency and data-integrity measurements. Keep a rollback version. Promote changes only after their evidence is reviewed, outside the active session. No model may rewrite production architecture, credentials or risk limits on its own. Required AI failure blocks new candidate approval; exits and reconciliation remain independent.

Daily jobs, actual model calls, adaptive learning, drift promotion, architecture patch automation and live-data paper sessions are **planned**, not implemented. The next concrete milestone is the read-only data adapter and a licensed historical validation report. Live trading remains a later gate. There is currently no defensible percentage chance of profit or guaranteed self-improvement.
