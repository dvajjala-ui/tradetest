#!/usr/bin/env python3
"""Build and exercise the bundled synthetic workflows without broker/model APIs."""

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-build", action="store_true", help="Use an existing Release CLI build.")
    args = parser.parse_args()
    dotnet = shutil.which("dotnet")
    if dotnet is None and os.environ.get("DOTNET_ROOT"):
        candidate = Path(os.environ["DOTNET_ROOT"]) / ("dotnet.exe" if os.name == "nt" else "dotnet")
        if candidate.is_file():
            dotnet = str(candidate)
    if dotnet is None:
        raise RuntimeError("Install the .NET 10 SDK and put dotnet on PATH (or set DOTNET_ROOT).")

    environment = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    sdk = subprocess.run([dotnet, "--list-sdks"], check=True, capture_output=True,
                         text=True, env=environment, timeout=30).stdout
    if not args.no_build and not any(line.startswith("10.") for line in sdk.splitlines()):
        raise RuntimeError("The .NET 10 SDK is required to build this prototype.")

    artifact_root = ROOT / "artifacts"
    artifact_root.mkdir(exist_ok=True)
    output = Path(tempfile.mkdtemp(prefix="offline-check-", dir=artifact_root))
    if not args.no_build:
        print("Building the Release CLI...", flush=True)
        with (output / "build.log").open("x", encoding="utf-8") as log:
            build = subprocess.run([dotnet, "build", "src/TradeTest.Cli/TradeTest.Cli.csproj",
                                    "-c", "Release", "--nologo", "-v:q"], cwd=ROOT,
                                   env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=300)
        if build.returncode:
            raise RuntimeError(f"Build failed; inspect {output / 'build.log'}")

    assembly = ROOT / "src/TradeTest.Cli/bin/Release/net10.0/TradeTest.Cli.dll"
    if not assembly.is_file():
        raise RuntimeError("Release CLI build is missing. Run again without --no-build.")

    reports = []

    def run(name, *command):
        path = output / f"{name}.json"
        with path.open("x", encoding="utf-8") as report, (output / f"{name}.log").open("x", encoding="utf-8") as log:
            result = subprocess.run([dotnet, str(assembly), *map(str, command)], cwd=ROOT,
                                    env=environment, stdout=report, stderr=log, timeout=120)
        if result.returncode:
            raise RuntimeError(f"{name} failed; inspect {output / (name + '.log')}")
        value = json.loads(path.read_text(encoding="utf-8"))
        reports.append(path)
        print(f"Completed {name}", flush=True)
        return value

    fixtures = ROOT / "fixtures"
    run("walk-forward", "evaluate-walk-forward", fixtures / "synthetic-walk-forward.json")
    run("long-term", "evaluate-long-term", fixtures / "synthetic-long-term.json")
    run("total-return", "build-total-return", fixtures / "synthetic-corporate-actions.json")
    requests = run("ai-requests", "prepare-ai", fixtures / "synthetic-ai-cases.json")
    ai = run("ai-evaluation", "evaluate-ai", fixtures / "synthetic-ai-cases.json",
             fixtures / "synthetic-ai-recordings.json")
    if (ai.get("mode") != "RECORDED_OFFLINE" or ai.get("dataKind") != "Synthetic"
            or ai.get("datasetHash") != requests.get("datasetHash") or not ai.get("cases")):
        raise RuntimeError("Recorded AI report does not describe the expected synthetic offline dataset.")

    database = output / "research.sqlite"
    imported = run("research-import", "import-research", fixtures / "synthetic-research.json", database)
    repeated = run("research-repeat", "import-research", fixtures / "synthetic-research.json", database)
    if imported.get("Status") != "Applied" or repeated.get("Status") != "AlreadyApplied":
        raise RuntimeError("Research import did not apply once and remain idempotent.")
    run("research-health", "health", database)
    company_path = output / "company.json"
    run("company-export", "export-research", database, "2026-04-01T00:00:00Z", "SYNTH-ONE", "revenue", company_path)
    company = json.loads(company_path.read_text(encoding="utf-8"))
    if company.get("schemaVersion") != "tradetest-company-v1" or company.get("securityId") != "SYNTH-ONE":
        raise RuntimeError("Company export identity is incorrect.")
    reports.append(company_path)

    run("replay", "replay", fixtures / "synthetic-bars.json", output / "journal.sqlite",
        "offline-check", fixtures / "synthetic-market-reference.json")
    journal = run("journal", "journal", output / "journal.sqlite", "offline-check")
    if not journal.get("EventCount") or not journal.get("LastHash"):
        raise RuntimeError("Replay did not produce a persisted journal.")
    snapshot_path = output / "snapshot.json"
    run("dashboard-export", "export-dashboard", ROOT, snapshot_path)
    snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
    if snapshot.get("dataKind") != "Synthetic":
        raise RuntimeError("Dashboard export is not the expected synthetic report.")
    reports.append(snapshot_path)

    manifest = {
        "schemaVersion": "tradetest-offline-check-v1",
        "generatedAt": dt.datetime.now(dt.timezone.utc).isoformat(),
        "mode": "OFFLINE",
        "dataKind": "Synthetic",
        "brokerApiCalls": 0,
        "modelApiCalls": 0,
        "limitations": ["Synthetic arithmetic and integration checks only.",
                        "No real-market results, model skill, or probability of profit are measured.",
                        "Recorded token charges are fixture metadata, not actual API spending."],
        "reports": [{"file": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                    for path in reports],
    }
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"\nOffline check passed. Reports: {output}")
    print(f"Open {company_path} in the dashboard's Company research view.")
    print("No model or broker API calls were made. This does not validate profitability.")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"Offline check failed: {error}", file=sys.stderr)
        sys.exit(1)
