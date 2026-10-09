# Consistent company reports and private dashboard reads

Checked 2026-10-09. Company reports now read facts, metrics, source documents and rankings from one pinned read-only SQLite snapshot. A concurrent import cannot combine evidence from different database states.

## Local workflow

Import the explicitly synthetic example, then export a dated company report:

~~~bash
dotnet run --project src/TradeTest.Cli -- import-research fixtures/synthetic-research.json research.sqlite
dotnet run --project src/TradeTest.Cli -- export-research research.sqlite 2026-04-01T00:00:00Z SYNTH-ONE revenue company.json
~~~

Open Company research and choose **Open company report**. The file stays in browser memory. **Export company report** downloads it; **Restore example evidence** returns to the bundled demonstration. Loading a company report changes the research view. Replay, portfolio studies and performance retain their own dashboard report.

`research <db> <as-of> <security> <query>` prints the same complete `tradetest-company-v1` JSON contract. This replaces the earlier CLI summary of query-matched claims. `export-research` writes camel-case fields and refuses to overwrite the specified database or its WAL/SHM files. The `research`, `health` and `journal` commands open existing databases read-only and do not initialize a missing path.

## Authenticated service workflow

Configure the [read-only service](18-deployment-and-runtime.md) with an existing database, a token and exact allowed origins. Connect it on Deployment. Company research then shows a form for the stable security ID, explicit as-of timestamp and source-document search terms.

The response contains active verified claims, the latest active verified metric per kind, a deterministic screen, and source metadata for every returned fact. Search adds up to eight matching documents; an empty query still returns documents cited by facts. Empty historical reports retain their requested company identity. Dates are normalized to UTC for reproducible hashes.

`/api/research/health` reads row counts and recent import history from one snapshot as well. The API opens the database read-only and has no order, import, strategy-edit or account-write endpoint.

## Evidence consistency and corrections

The snapshot starts on a real SQLite read before any company query. Its deferred transaction retains the same WAL state across reads. Writers can commit while it is open; a new read sees their changes. Dispose snapshots promptly because long readers retain older WAL pages.

Fact and document corrections withdraw predecessors when first known. A correction continues to withdraw its predecessor if the document containing it is later replaced. The previous retrieval path could lose that withdrawal marker after filtering documents; a regression test covers this chain. Earlier as-of reports remain reproducible.

Each metric must match an active verified fact, and each fact must match its dated source document. The server recomputes document content hashes during a company read. Altered content causes an integrity failure. These checks validate relationships and bytes; they do not prove issuer claims or grant data-use rights.

`PacketHash` identifies the dated fact packet. `Hash` covers the complete report serialized with the server's output contract and its hash field empty. Query text, matching document IDs, metrics and scores participate in report identity. Hashes are content identifiers, not signatures. The browser validates fields, dates and citation links; it does not independently reproduce the server's JSON hash. Imported-file hashes and verification labels remain assertions from the producer.

## Browser performance and reliability

The browser renders 50 claims per page. Search covers the whole loaded packet and resets to the first matching page. A metric citation opens the page containing its source fact. This limits table DOM work without dropping evidence.

JSON downloads have a ten-second deadline and a two-megabyte byte limit enforced while reading chunks. Split Unicode is decoded correctly; HTML sign-in pages are rejected. Tokens go only in headers. Requests omit cookies and reject redirects. Tokens and reports stay in memory. Late responses cannot replace newer file imports or restored examples. Failed authentication preserves the service URL for a retry.

This increment passes **52 .NET tests and seven web tests**. Fresh Chromium checks also passed local import/export, authenticated reads, historical cutoffs, invalid-file retention, late-response handling, 202-claim pagination/citation jumps and a 390-pixel viewport. HTTP checks returned 401 for an anonymous private read and 200 for an authenticated read. No token appeared in request URLs or persisted browser storage. Checks used synthetic data and an ephemeral token.

## Deployment status

The Oct 8 temporary Vercel preview expired. On Oct 9, `vercel whoami` still reported the workspace logged out, and no authenticated Vercel deployment tools were exposed. Browser login is separate from workspace authorization. The checked-in `web/vercel.json`, successful web build and `web` root-directory settings remain ready. Permanent ownership and Git auto-deploy have not been verified.
