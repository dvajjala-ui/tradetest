# Implementation status and next gates

Checked 2026-10-08. The user authorized pushing the plan and beginning G0–G4. The repository currently contains an **offline prototype** and synthetic examples. G5 API purchases/broker write integration and G6 real-money activation have separate review gates in [the master plan](12-master-implementation-plan.md).

| Gate | Implemented and reviewable | Required before exit |
| --- | --- | --- |
| G0 source audit | [Source/licence register](15-source-register-and-data-contract.md); point-in-time record contract; initial Groww/NSE access and cost references. | Confirm actual account entitlements, permitted historical use, corporate-action coverage, data prices, and first company universe. No paid dataset is available in this workspace. |
| G1 replay core | Incremental opening-range/VWAP state, completed-bar quality checks, separate spread/slippage, partial fills, Groww charge assumptions, fee-aware cash/loss sizing, hash-chain journal, versioned security master, explicit calendar, forward split/bonus/dividend return chain and terminal settlement/write-off outcomes. | Licensed security/calendar/action history, independent adjusted-return reconciliation, unsupported rights/merger/demerger cases, multiple instruments, feed-state faults and broker reconciliation. All numerical assumptions need real contract-note checks. |
| G2 research system | Atomic, idempotent research batches; rollback and quarantine audit; source hashes/timestamps, indexed company retrieval, correction-aware facts/metrics, as-of packets, deterministic screen, CLI health report, static research dashboard and authenticated read-only API. | Licensed ingestion adapters, full parser correction workflow, nightly packet job, and cross-source quality checks using real data. |
| G3 hypothesis tests | Frozen baselines, chronological study and walk-forward with disjoint evaluation windows/final holdout, cost sensitivity, optional dated security universe, funded portfolio fees, outcome-data cutoff and optional net-expense passive fund comparison. | Licensed survivorship-aware history, registered hypotheses/adverse regimes, real final-holdout evidence, reconciled investable fund data, failed-variant log, adequate independent samples and a fitting protocol if adaptive strategies are introduced. No positive edge is claimed. |
| G4 paper/shadow | Offline paper replay with a no-order CLI and audit journal. | Authorized live feed and read-only account access, at least 30 operational paper sessions, measured p95/p99 latency, crash/restart drills, and broker position/order reconciliation. No live sessions have been recorded. |

## Reproduce the current evidence

Use the .NET 10 SDK and run `dotnet test TradeTest.slnx`. The current suite has **44 tests**, including the earlier replay/import/calendar tests plus API access, read-only storage, corporate-action wealth preservation, corrections, terminal losses and fund benchmark checks. `cd web && npm test` runs four report/security checks. GitHub Actions builds both projects, runs their tests and checks that the bundled dashboard report matches current engine output. See [measured optimization results](17-optimization-and-features.md) and [return/benchmark details](19-total-return-and-benchmarks.md).

`dotnet run --project src/TradeTest.Cli -- evaluate-study fixtures/synthetic-study.json` reports one **synthetic** session in each chronological partition and then widens spread/slippage for the held-out session. Spread/slippage are included in simulated execution prices; the `SpreadAndSlippage` fee field remains zero to avoid double counting. `evaluate-long-term fixtures/synthetic-long-term.json` calculates a return against a **synthetic** TRI. Those percentages only verify arithmetic; they are not forecasts or measured performance. The long-term drawdown field observes rebalance boundaries only. The intraday bootstrap interval is exploratory and omitted below 30 trades.

## Immediate build sequence

1. Obtain a written data-use/coverage decision for Groww history and, if needed, NSE datasets. Record total price and missing fields before buying access.
2. Populate the security master/calendar and return builder with licensed dated records. Reconcile adjustments and terminal outcomes against an independent source; model unsupported actions explicitly.
3. Connect licensed ingestion to the atomic import/quarantine path, add parser correction jobs, cross-source checks, and a quality dashboard.
4. Register hypotheses and evaluation dates before applying real history. Run the walk-forward and holdout harness, retain every failed variant, and reconcile an actual investable passive benchmark.
5. Only after authorized read access exists, add a no-order live feed adapter and measure paper-session reliability, latency, and reconciliation.

There is no defensible percentage chance of profit yet. The measured percentages in these fixtures are deliberately artificial. The [broker and cost report](13-broker-and-cost-report.md) remains the planning estimate; actual spend starts only when a subscription or data purchase is selected after current terms are checked.
