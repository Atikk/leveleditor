#!/usr/bin/env python3
"""Add a default passability grid to maps that don't have one.

The script will:
- Iterate all JSON files in the repo `maps/` directory.
- If a file lacks a `passability` field (or it's null), create a jagged boolean grid [rows][cols] filled with `true` (passable).
- Write a backup of the original file with `.bak` appended.
- Overwrite the original with the updated JSON (pretty-printed).

Usage:
    python .\scripts\add_passability.py [--dry-run]

Options:
  --dry-run   Don't write any files; just report what would be changed.

This is intentionally conservative and only adds a default passability grid. For more nuanced behavior (e.g., infer from doodads or tile properties), extend the script.
"""
from __future__ import annotations
import json
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
MAP_DIR = ROOT / "maps"

if not MAP_DIR.exists():
    print(f"Map directory not found: {MAP_DIR}")
    sys.exit(1)

DRY_RUN = "--dry-run" in sys.argv

changed = 0

for path in sorted(MAP_DIR.glob("*.json")):
    try:
        text = path.read_text(encoding="utf-8")
        data = json.loads(text)
    except Exception as e:
        print(f"SKIP {path}: failed to parse JSON: {e}")
        continue

    cols = data.get("cols")
    rows = data.get("rows")
    if not isinstance(cols, int) or not isinstance(rows, int) or cols <= 0 or rows <= 0:
        print(f"SKIP {path}: missing or invalid cols/rows")
        continue

    pass_grid = data.get("passability")
    if pass_grid is not None:
        print(f"OK {path}: passability already present")
        continue

    print(f"ADD {path}: inserting default passability grid ({rows}x{cols})")
    changed += 1
    if DRY_RUN:
        continue

    # create jagged array [row][col]
    jagged = [[True for _ in range(cols)] for _ in range(rows)]
    data["passability"] = jagged

    # backup
    bak = path.with_suffix(path.suffix + ".bak")
    try:
        path.rename(bak)
    except Exception:
        # if rename fails, try writing to bak path
        bak.write_text(text, encoding="utf-8")

    # write updated file
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")

print(f"\nFinished. {changed} file(s) modified.")
if changed > 0 and DRY_RUN:
    print("Dry run: no files were changed. Rerun without --dry-run to apply changes.")

sys.exit(0)
