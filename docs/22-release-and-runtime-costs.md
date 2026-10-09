# Deployment, optimization and cost checkpoint

Checked 2026-10-09. The dashboard is live at [tradetest-dashboard.vercel.app](https://tradetest-dashboard.vercel.app). Project `tradetest-dashboard` belongs to `dvajjala-2765s-projects`, uses Vite/Node 24, and builds the repository's `web` directory.

## Delivered changes

- A portable dashboard for company evidence, intraday replay, chronological studies and performance reports; company files can be opened and exported privately in browser memory.
- An authenticated read-only .NET API, consistent SQLite snapshots, atomic imports, source hashes, dated correction withdrawal and a regression fix preventing withdrawn claims from reappearing.
- Compiled ranking histories and optional source-provenance validation; forward split/bonus/dividend returns, terminal cash/write-off outcomes, funded portfolio costs and a passive fund comparison.
- Bounded JSON downloads, stale-response handling, 50-claim pagination, citation jumps and a working non-root container.
- A permanent production deployment and a [manual deployment helper](../scripts/deploy-vercel.py) that builds the exact pushed Git commit.

The current engine suite passes **52 tests**; the web suite passes **seven tests**. Chromium checks covered authenticated/private reads, historical cutoffs, file import/export, invalid input retention, late responses, larger evidence packets and mobile layout. Production HTTP returned 200 and the hosted JSON matched the pushed report exactly.

## Measured performance

| Workload | Median before | Median after | Less elapsed time |
| --- | --- | --- | --- |
| 5,000 synthetic replay sessions | 1,587.88 ms | 451.57 ms | 71.56% |
| 5,000 research documents, facts and metrics | 903.42 ms | 240.04 ms | 73.43% |
| 24 ranking queries over 336,000 metric rows | 1,014.42 ms | 201.74 ms | 80.11% |

Ranking index construction took a separate median 232.79 ms. Build plus queries took a median 436.60 ms. Scores, evidence IDs and flags matched the original ranking path. [Replay/import methods](17-optimization-and-features.md) and [ranking raw trials](20-ranking-index-and-provenance.md) disclose workload, exclusions and allocation measurements.

These are local synthetic engineering benchmarks. No live feed, broker acknowledgment or remote AI latency was measured. They do not establish an end-to-end microsecond response time or a profitable strategy.

## Cost and runtime choice

| Item | This checkpoint |
| --- | --- |
| Vercel hosting | Existing Hobby account; no paid plan upgrade or hosting subscription purchased by this work. |
| Dashboard runtime | Static assets and precomputed JSON; no hosted .NET worker or model invocation. Build and transfer quotas apply. |
| Broker / licensed data | No subscription bought and no broker access activated. Current entitlement and licence decisions remain pending. |
| AI inference | No paid provider call made. The additional AI tool's name is still pending. |
| Persistent backend | Runs locally. The container is ready for a separately selected host; none was purchased. |

Vercel serves the dashboard. Local execution removes the dashboard-to-engine network hop during development. A future persistent host should be selected from measured feed/broker routes and recovery requirements; a home connection is not automatically closest to the broker. Broker, licensed-data and model budgets remain the [planning estimates](13-broker-and-cost-report.md), to be checked before purchase.

## What remains

Vercel rejected the GitHub project connection. Manual deployment from the public Git source succeeds, but automatic deployment from main-branch pushes is not yet verified. The user has been given the exact project Git-settings link to connect the repository.

Licensed market/company history, independent reconciliation, parser adapters, preregistered market evaluations, live-data paper sessions and broker reconciliation remain open gates. There is no defensible percentage chance of profit from the current synthetic data. The toy returns verify arithmetic and are not forecasts. Broker orders and real-money activation remain disabled.
