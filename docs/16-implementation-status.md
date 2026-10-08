# Implementation status and next gates

Checked 2026-10-08. The user authorized pushing the plan and beginning G0–G4. The repository currently contains an **offline prototype** and synthetic examples. G5 API purchases/broker write integration and G6 real-money activation have separate review gates in [the master plan](12-master-implementation-plan.md).

| Gate | Implemented and reviewable | Required before exit |
| --- | --- | --- |
| G0 source audit | [Source/licence register](15-source-register-and-data-contract.md); point-in-time record contract; initial Groww/NSE access and cost references. | Confirm actual account entitlements, permitted historical use, corporate-action coverage, data prices, and first company universe. No paid dataset is available in this workspace. |
| G1 replay core | .NET domain, completed-bar quality checks, deterministic opening-range/VWAP candidate, separate spread/slippage, simulated partial fills, Groww fee assumptions, fixed risk caps, versioned hash-chain event journal. | Security master and dated corporate-action mapping, exchange calendar, multiple instruments, feed-state faults, and independent reconciliation. All numerical assumptions need real contract-note checks. |
| G2 research system | SQLite archive with source hash, publication/first-known timestamps, FTS retrieval, facts, verification state, corrections, metric provenance, as-of packets and deterministic company screen. | Licensed ingestion adapters, transactional batch quarantine, parser/version correction workflow, nightly packet job, and source-quality dashboard. |
| G3 hypothesis tests | Frozen rules-only intraday and long-term baselines; chronological training/validation/holdout report; spread/slippage sensitivity; point-in-time long-term rank versus a supplied TRI series. | Licensed survivorship-aware historical data, pre-registered parameters, rolling walk-forward and adverse regimes, untouched final holdout, investable fund comparison, failed-variant log, uncertainty with adequate independent samples. No positive edge is claimed. |
| G4 paper/shadow | Offline paper replay with a no-order CLI and audit journal. | Authorized live feed and read-only account access, at least 30 operational paper sessions, measured p95/p99 latency, crash/restart drills, and broker position/order reconciliation. No live sessions have been recorded. |

## Reproduce the current evidence

Use the .NET 10 SDK and run `dotnet test TradeTest.slnx`. The current suite has **15 passing tests** covering dated information, deterministic replay, session cutoff, risk sizing, costs, partial fills, journal integrity, research provenance, and chronological study boundaries. GitHub Actions runs the same suite on pushes and pull requests.

`dotnet run --project src/TradeTest.Cli -- evaluate-study fixtures/synthetic-study.json` reports one **synthetic** session in each chronological partition and then widens spread/slippage for the held-out session. Spread/slippage are included in simulated execution prices; the `SpreadAndSlippage` fee field remains zero to avoid double counting. `evaluate-long-term fixtures/synthetic-long-term.json` calculates a return against a **synthetic** TRI. Those percentages only verify arithmetic; they are not forecasts or measured performance. The long-term drawdown field observes rebalance boundaries only. The intraday bootstrap interval is exploratory and omitted below 30 trades.

## Immediate build sequence

1. Obtain a written data-use/coverage decision for Groww history and, if needed, NSE datasets. Record total price and missing fields before buying access.
2. Add a stable security master, dated corporate actions, delisting outcomes, and exchange calendar; reject ambiguous or incomplete sessions in both lanes.
3. Build licensed ingestion and quarantine with atomic imports, source checks, parser versions, and a quality dashboard.
4. Register hypotheses and evaluation dates before applying real history. Run rolling walk-forward and holdout reports, include every failed variant and an investable passive benchmark.
5. Only after authorized read access exists, add a no-order live feed adapter and measure paper-session reliability, latency, and reconciliation.

There is no defensible percentage chance of profit yet. The measured percentages in these fixtures are deliberately artificial. The [broker and cost report](13-broker-and-cost-report.md) remains the planning estimate; actual spend starts only when a subscription or data purchase is selected after current terms are checked.
