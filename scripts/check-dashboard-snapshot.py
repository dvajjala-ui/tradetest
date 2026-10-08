"""Compare the checked-in demo with current .NET output, ignoring its generation clock."""
import json
import sys
from pathlib import Path

if len(sys.argv) != 3:
    raise SystemExit("Usage: check-dashboard-snapshot.py <bundled.json> <fresh.json>")

bundled, current = (json.loads(Path(name).read_text()) for name in sys.argv[1:])
for report in (bundled, current):
    report.pop("generatedAt", None)
if bundled != current:
    raise SystemExit("The dashboard demo is stale. Rebuild and run export-dashboard before committing.")
print("Dashboard report matches the current engine and fixtures.")
