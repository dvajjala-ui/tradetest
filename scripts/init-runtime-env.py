#!/usr/bin/env python3
"""Create the ignored local API token file once, without printing its secret."""

import os
from pathlib import Path
import secrets
import sys


def main():
    target = Path(__file__).resolve().parents[1] / "deploy/.env"
    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w", encoding="utf-8") as environment:
        environment.write(f"TRADETEST_API_TOKEN={secrets.token_hex(32)}\n")
    print(f"Created {target}. The token was not printed; keep this ignored file private.")
    print("This protects the local read-only API. It is not a broker or AI credential.")
    if os.name == "nt":
        print("On Windows, restrict the file's ACL to your account; POSIX mode bits are insufficient.")


if __name__ == "__main__":
    try:
        main()
    except FileExistsError:
        print("deploy/.env already exists; it was left untouched.", file=sys.stderr)
        sys.exit(1)
    except OSError as error:
        print(f"Could not create runtime environment: {error}", file=sys.stderr)
        sys.exit(1)
