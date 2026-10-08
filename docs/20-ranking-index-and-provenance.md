# Dated ranking index and source provenance

Checked 2026-10-08. This increment compiles company metric histories once for repeated dated queries. It also makes optional source-document and fact validation part of long-term evaluation.

## Measured local optimization

| Measurement | Scan | Compiled index |
| --- | --- | --- |
| Median elapsed time for 24 queries | 1,014.42 ms | 201.74 ms |
| Median allocation per query | 19,894,934 bytes | 2,277,293 bytes |
| One-time build, median | — | 232.79 ms |
| Build plus 24 queries, median of combined trial totals | — | 436.60 ms |

Queries took **80.11% less time** and allocated **88.55% fewer bytes**. The workload contains 3,000 synthetic companies, 16 revisions of seven metrics each, and 24 query dates: 336,000 metric rows. Every complete score output, including evidence IDs and flags, matched the original scan path.

Three separate Release processes ran on the shared two-CPU workspace with .NET 10.0.12. Each process warmed both paths once and alternated their order across query dates. Timed queries exclude data generation, index construction, output hashing/serialization, source-document validation, network access and AI. Build time is measured separately. The median combined cost uses each trial's build plus indexed-query total before taking its median; it is not the sum of two separately chosen medians.

These figures measure local computation for this workload. They do not measure broker acknowledgments, live-market latency, or profitability. Small workloads or a single query may not justify an index. Compiled histories require memory and should be rebuilt when their input snapshot changes.

Reproduce from the repository root:

~~~bash
dotnet build TradeTest.slnx -c Release
dotnet run --no-build -c Release --project benchmarks/TradeTest.Benchmarks -- ranking 3000
~~~

[Raw trials and method](../benchmarks/results/2026-10-08-ranking.json) are checked in and displayed in the dashboard's Performance view. The generator hashes this benchmark input alongside the other report inputs.

## Dated source checks

Long-term inputs can supply both `EvidenceFacts` and `EvidenceDocuments`. Supplying only one is rejected. The evaluator checks document content hashes, publication/knowledge/retrieval order, source URL and licence metadata, fact-to-document links, security identity, metric-to-fact links, and verification state. A metric cannot be dated earlier than its source fact or claim a higher verification state.

Fact and document corrections withdraw predecessor evidence when the correction first becomes known. Past queries remain unchanged. An unverified correction can withdraw an old claim; it cannot silently preserve the old verified metric. Correction chains must have one earlier predecessor and cannot fork. The immutable index uses binary search for dated metrics and precompiled withdrawal times for evidence checks.

`SourceProvenanceValidated` means these structural checks ran. It does not authenticate a publisher, prove a claim, establish licence rights, or replace cross-source review. Inputs without both histories remain supported for earlier arithmetic examples and are explicitly labelled as relying on caller assertions. All checked-in evidence is synthetic.

The 47-test .NET suite includes indexed-versus-scan equivalence across future, stale, tied and incomplete metric histories; dated fact/document withdrawal; altered hashes; verification promotion; and immutable input ownership. The four web tests validate the rendering contract and unsafe input boundaries.

## Container runtime fix

The published non-root API image now gives its copied assemblies and bundled fixtures explicit read permissions. A real container startup caught source-file permissions that prevented the runtime user from reading the synthetic fixtures. The fixed container ran as UID 1654 and returned 401 without its API token and 200 with the token. The token was provided only to the test process; it was not committed or copied into the image.

Permanent Vercel ownership and Git auto-deploy still require authenticated deployment access. These engine and report changes are usable locally regardless of hosting setup.
