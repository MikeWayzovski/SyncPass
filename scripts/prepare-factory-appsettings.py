#!/usr/bin/env python3
"""Write a factory appsettings.json into the publish folder.

Keeps the product OAuth client so "Inloggen met Trimble ID" works, but never
copies refresh tokens, sync jobs, folder mappings, or local paths from the
developer machine.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: prepare-factory-appsettings.py <repo-root> <publish-dir>", file=sys.stderr)
        return 2

    root = Path(sys.argv[1]).resolve()
    publish = Path(sys.argv[2]).resolve()
    example_path = root / "appsettings.example.json"
    if not example_path.is_file():
        print(f"ERROR: missing {example_path}", file=sys.stderr)
        return 1
    if not publish.is_dir():
        print(f"ERROR: publish directory does not exist: {publish}", file=sys.stderr)
        return 1

    factory = json.loads(example_path.read_text(encoding="utf-8"))
    factory["SyncJobs"] = []
    factory["SharedSyncRules"] = []
    connect = factory.setdefault("TrimbleConnect", {})
    connect["RefreshToken"] = ""

    local_path = root / "appsettings.json"
    if local_path.is_file():
        local = json.loads(local_path.read_text(encoding="utf-8"))
        local_connect = local.get("TrimbleConnect") or {}
        client_id = (local_connect.get("ClientId") or "").strip()
        client_secret = (local_connect.get("ClientSecret") or "").strip()
        if client_id:
            connect["ClientId"] = client_id
        if client_secret:
            connect["ClientSecret"] = client_secret

    if not (connect.get("ClientId") or "").strip() or not (connect.get("ClientSecret") or "").strip():
        print(
            "ERROR: local appsettings.json must contain TrimbleConnect ClientId and ClientSecret "
            "so a clean installer can still sign users in.",
            file=sys.stderr,
        )
        return 1

    dest = publish / "appsettings.json"
    dest.write_text(json.dumps(factory, indent=2) + "\n", encoding="utf-8")
    print("Factory appsettings.json written (jobs/tokens stripped, OAuth client kept).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
