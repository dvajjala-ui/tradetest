# Dashboard deployment and runtime

Checked 2026-10-09. The permanent static dashboard is live at [tradetest-dashboard.vercel.app](https://tradetest-dashboard.vercel.app), in project `tradetest-dashboard` under `dvajjala-2765s-projects`. The user authorized deployment and checkpoint pushes.

## Runtime choice

| Component | Recommended location | Reason |
| --- | --- | --- |
| Read-only dashboard | Vercel static hosting, or local Vite | Small static bundle; no server calculation is required to inspect a report. |
| Replay, risk, research SQLite | Local machine for development; persistent .NET server for unattended operation | Own the process, storage and resource budget. Dashboard traffic does not run trading calculations. |
| Future market feed / reconciliation | Persistent process near the measured broker/data route | Needs durable state and recovery across the market session. Choose a region from actual feed/ack measurements. |
| Optional AI assessments | Offline research or separately timed service | No provider call is required to render a report or manage simulator state. |

Running locally removes the dashboard-to-service network hop. It does not remove broker, market-data or AI network delays. A hosted server can be closer to the broker than a home connection; measure both before choosing it. The existing performance numbers measure local synthetic workloads, not real-market execution.

Vercel now documents WebSocket support, but connections are bounded by a function's duration and later connections can reach a different instance. The persistent .NET engine is the selected implementation for this project. See [Vercel connection behaviour](https://vercel.com/kb/guide/do-vercel-serverless-functions-support-websocket-connections).

## Local dashboard

~~~bash
cd web
npm ci
npm run dev
~~~

Open `http://127.0.0.1:5173`. The dashboard uses a precomputed synthetic report. Navigation, evidence search, trade inspection, export and local file import work without a broker or backend service. Imported files remain in browser memory; they are not uploaded.

To regenerate the bundled report from the engine and fixtures, run at the repository root:

~~~bash
dotnet build TradeTest.slnx -c Release
dotnet run --no-build -c Release --project src/TradeTest.Cli -- export-dashboard . web/public/snapshot.json
~~~

CI compares current output with the bundled snapshot, excluding the generation clock, to catch stale reports. The exporter uses the explicit bundled fixture paths. It does not scan or export private databases.

## Vercel project settings

Import GitHub repository `dvajjala-ui/tradetest`, branch `main`, with these settings:

| Setting | Value |
| --- | --- |
| Root directory | `web` |
| Framework | Vite |
| Install | `npm ci` |
| Build | `npm run build` |
| Output | `dist` |
| Environment secrets | None for the synthetic dashboard |

The checked-in `web/vercel.json` defines security headers, revalidation of the report, and long caching of fingerprinted assets. Navigation uses URL fragments and does not need a server rewrite. Once the GitHub project is connected, main-branch pushes can deploy through Vercel's Git integration. See [Vercel monorepo root settings](https://vercel.com/docs/monorepos).

Browser login and workspace CLI login are separate. Vercel's [CLI login](https://vercel.com/docs/cli/login) uses a device authorization flow. An installed integration must also expose authenticated deployment tools in the active session before it can be used.

The first temporary preview expired. Workspace device authorization completed on Oct 9. The permanent production deployment built pushed Git commit `2394b079d8a98c798dbd737fac0ec1c841080aaf` directly from the public repository. Its page and JSON report returned 200; the hosted report exactly matched the committed report, and security headers were present.

The project repository connection was rejected by Vercel and remains a separate setup step. A Git-source deployment can succeed while the project's automatic repository connection is absent. Do not infer push-triggered deployment from the Git metadata of a manual build. Connect `dvajjala-ui/tradetest` in [the project's Git settings](https://vercel.com/dvajjala-2765s-projects/tradetest-dashboard/settings/git), then verify a later main-branch push creates a deployment.

Until Git auto-deploy is verified, update production explicitly:

~~~bash
python3 scripts/deploy-vercel.py
~~~

The helper requires a clean checkout matching pushed main and an authenticated workspace CLI. It deploys that exact commit using Vercel's Git-source API, waits for readiness and verifies the source SHA. It uploads no workspace files, database, environment file or token. It does not purchase a plan or create API subscriptions.

## Read-only .NET service

At the repository root:

~~~bash
dotnet run --project src/TradeTest.Api
~~~

Default binding: `http://127.0.0.1:5080`. `/health` reports offline status; `/api/snapshot` returns the cached synthetic report, with ETag and HEAD support. No endpoint places an order, imports data or changes risk policy.

The dashboard's Deployment screen can load this service. Its access token stays in browser memory and is cleared on reload. There is no token in query strings or persisted browser storage. A hosted HTTPS dashboard may require browser permission to reach a local service; local dashboard plus local service is the reliable development setup.

For private research reads, initialize/import a database with the CLI first, then configure the service:

- `TRADETEST_DATABASE`: absolute path to the existing SQLite database. The API opens it in read-only mode.
- `TRADETEST_API_TOKEN`: separately generated secret of at least 32 characters. Required for any private database or non-loopback binding.
- `TRADETEST_ALLOWED_ORIGINS`: semicolon-separated exact dashboard origins. Default permits local Vite only; include the actual hosted origin for a remote dashboard.
- `TRADETEST_ROOT`: repository/fixture root for a published service; development discovers the repository by walking parent directories.
- `ASPNETCORE_URLS`: explicit service binding. Put remote access behind HTTPS; protect the internal HTTP listener from direct public access.

Read routes:

~~~text
GET /api/research/health
GET /api/research/SYNTH-ONE?asOf=2026-04-01T00:00:00Z&query=revenue
~~~

`/api/snapshot` contains the synthetic demo when a database is configured. The research routes provide separate authenticated reads from a consistent database snapshot. The Company research screen can now query those routes or open a local company export; see [the company report workflow](21-consistent-company-reports.md). The demo is not a live account dashboard.

## Container option

~~~bash
docker build -f deploy/Dockerfile -t tradetest-api .
~~~

Supply the token and exact allowed origins through the deployment platform's secret settings. The non-root container listens on port 8080 and fails startup without a token for that non-loopback binding. Mount an initialized research database and its WAL files on persistent storage if enabling research reads. No container or alternative hosting account was purchased in this increment.

## Evidence and remaining work

The first increment passes 32 .NET tests, 4 dashboard contract/security tests, and browser checks for all six views, search, import/restore, and a 390-pixel mobile viewport. The web build is roughly 10.6 KB of gzipped JavaScript, excluding the JSON report and CSS. These are engineering measurements.

Permanent project ownership and production HTTP access have been verified. Git auto-deploy, real-data access, licensed parsers and paper-feed operation still need verification. The work does not establish a profitable strategy or activate real-money trading.
