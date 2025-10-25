#!/usr/bin/env python3
"""Validate map JSON files under the `maps/` directory.

Checks performed:
- JSON parses correctly.
- Required fields: cols, rows, map (array-of-arrays) present.
- If a passability grid is present it must match rows x cols.

Exit code: 0 if all files OK or only warnings; 1 if any file has an error.

This is a small, low-risk helper to encourage adding passability metadata for tile-based movement.
"""
from __future__ import annotations
import json
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
MAP_DIR = ROOT / "maps"

ERRORS = 0


def check_map_file(path: Path) -> None:
    global ERRORS
    try:
        text = path.read_text(encoding="utf-8")
        data = json.loads(text)
    except Exception as e:
        print(f"ERROR: Failed to parse JSON: {path}: {e}")
        ERRORS += 1
        return

    cols = data.get("cols")
    rows = data.get("rows")
    tilemap = data.get("map") or data.get("tiles") or data.get("mapData")

    if cols is None or rows is None or tilemap is None:
        print(f"ERROR: Missing required fields (cols, rows, map) in {path}")
        ERRORS += 1
        return

    if not isinstance(tilemap, list):
        print(f"ERROR: 'map' must be an array-of-arrays in {path}")
        ERRORS += 1
        return

    if len(tilemap) != rows:
        print(f"ERROR: map row count ({len(tilemap)}) != rows ({rows}) in {path}")
        ERRORS += 1
        return

    for y, row in enumerate(tilemap):
        if not isinstance(row, list):
            print(f"ERROR: map[{y}] is not an array in {path}")
            ERRORS += 1
            return
        if len(row) != cols:
            print(f"ERROR: map[{y}] length ({len(row)}) != cols ({cols}) in {path}")
            ERRORS += 1
            return

    # Check passability keys
    pass_grid = None
    if "passability" in data:
        pass_grid = data["passability"]
    elif "pass" in data:
        pass_grid = data["pass"]
    elif "passabilityGrid" in data:
        pass_grid = data["passabilityGrid"]

    if pass_grid is None:
        print(f"WARN: No passability grid found in {path} (map will be walkable by default). Consider adding a 'passability' boolean grid.)")
        return

    # Validate passability dimension
    if not isinstance(pass_grid, list) or len(pass_grid) != rows:
        print(f"ERROR: passability must be an array of length rows ({rows}) in {path}")
        ERRORS += 1
        return

    for y, prow in enumerate(pass_grid):
        if not isinstance(prow, list) or len(prow) != cols:
            print(f"ERROR: passability[{y}] must be an array of length cols ({cols}) in {path}")
            ERRORS += 1
            return
        for x, v in enumerate(prow):
            if not isinstance(v, bool):
                print(f"ERROR: passability[{y}][{x}] must be boolean in {path}")
                ERRORS += 1
                return

    print(f"OK: {path} has valid passability grid ({rows}x{cols}).")


if __name__ == "__main__":
    files = sorted(MAP_DIR.glob("*.json"))
    if not files:
        print(f"No maps found in {MAP_DIR}")
        sys.exit(0)

    for f in files:
        check_map_file(f)

    if ERRORS:
        print(f"\nValidation finished: {ERRORS} error(s) found.")
        sys.exit(1)
    else:
        print("\nValidation finished: no errors.")
        sys.exit(0)
