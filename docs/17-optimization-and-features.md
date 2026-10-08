# Optimization measurements and pending-feature update

Checked 2026-10-08. The offline replay and ingestion paths now process less repeated work. The update also adds atomic imports, correction-aware retrieval, dated security/calendar records, expanding walk-forward evaluation, and fee-aware risk sizing.

## Measured performance

Three separate Release processes were run for each workload on the same Debian 13 container with two logical processors and .NET 10.0.12. Values below are medians. The baseline is commit `eb4309cd40e9c59f6d065fa7e500f4079fa0a602`; [raw before/after trials](../benchmarks/results/2026-10-08.json) retain timings, allocations, counts, and runtime metadata.

| Workload | Before | After | Measured change |
| --- | --- | --- | --- |
| Replay 5,000 synthetic 75-bar sessions | 1,587.9 ms | 451.6 ms | 71.6% less elapsed time; 3.52× throughput |
| Allocation per replay session | 483,960 bytes | 8,120 bytes | 98.3% less allocation |
| Import 5,000 documents + 5,000 facts + 5,000 metrics | 903.4 ms | 240.0 ms | 73.4% less elapsed time; 3.76× throughput |

The replay workload is a flat, no-trade five-minute session with 100 warm-up sessions. It excludes broker/API latency, AI, persistence, and active-trading serialization. The import workload includes cold import-path startup and uses a new local SQLite database for each trial. The filesystem and shared-container scheduler affect results; the recorded tail timings include scheduling outliers. These are local measurements, not an end-to-end latency promise or a portfolio-return estimate.

## What changed

- **Incremental replay:** each completed candle updates opening range, volume, and VWAP once. Replay no longer copies/revalidates every prefix. Invalid stream data latches the session closed. Four frozen trade/no-trade/rejection/partial-fill outputs remain identical to the baseline.
- **Lean evaluation:** bulk evaluation skips event JSON that it does not persist. CLI replay still captures the complete audit journal. Walk-forward reuses each fixed-strategy session report across expanding folds; compiled reference indexes are reused across sessions.
- **Atomic research ingestion:** one transaction writes documents, FTS rows, facts, metrics, and import audit. A constraint or validation failure rolls all research rows back and records the parseable payload in quarantine. Stable batch IDs and payload hashes make exact retries idempotent and reject changed payloads under the same ID. Database write locks serialize concurrent retries.
- **Correction handling:** corrected documents and superseded facts stop supplying current facts/metrics from their actual first-known timestamp. Earlier as-of queries retain the old evidence. Unverified corrections can withdraw stale claims while awaiting verification. Branching correction chains are rejected.
- **Indexed company retrieval:** queries filter by company and as-of time in SQLite. Timestamp, source, and correction indexes avoid fetching the entire research corpus for a company packet. A health command reports stored records and recent applied/quarantined batches.
- **Dated market foundations:** stable security IDs retain versioned symbols, listing state, effective dates, first-known dates, sources, and licences. Explicit exchange-session records support supplied special sessions. Optional reference data validates replay windows and filters the long-term universe; an unknown or unverified listing/session cannot pass those checks. Source metadata and a manually assigned verification state do not establish licence entitlement by themselves.
- **Walk-forward evaluation:** expanding training history, bounded validation windows, disjoint evaluation dates, a separate final holdout, wider spread/slippage scenarios, and an input checksum. The current hypothesis is frozen and fits no parameters; a future learned strategy will need its own fitting and cache rules.
- **Fee-aware sizing:** reserve round-trip charges in available cash and planned per-trade/daily loss budgets. Small remaining budgets can yield `NO_TRADE` because minimum charges alone are too large. Stop gaps can exceed the planned loss; current sizing does not model a worst-case gap distribution. The sample policy is versioned `paper-example-v2-fees`.

## Run the added features

~~~bash
dotnet run --project src/TradeTest.Cli -- import-research fixtures/synthetic-research.json research.sqlite
dotnet run --project src/TradeTest.Cli -- health research.sqlite
dotnet run --project src/TradeTest.Cli -- universe fixtures/synthetic-market-reference.json 2026-01-07T00:00:00Z
dotnet run --project src/TradeTest.Cli -- replay fixtures/synthetic-bars.json replay.sqlite session-1 fixtures/synthetic-market-reference.json
dotnet run --project src/TradeTest.Cli -- evaluate-walk-forward fixtures/synthetic-walk-forward.json
~~~

Every bundled source, price, listing, session, and company is synthetic. The new market reference records validate metadata and timing; they do not populate an actual NSE/BSE universe. A later increment adds the [total-return builder and explicit delisting outcomes](19-total-return-and-benchmarks.md); licensed inputs and independent reconciliation remain. The gate status and next work are tracked in [the implementation status](16-implementation-status.md).

Malformed JSON and inputs that cannot be converted into the typed batch are rejected before import; their parse failures are not stored in the quarantine table. Existing prototype rows remain intact during the additive schema update, but only imports made through the new batch API have retry/audit metadata.
