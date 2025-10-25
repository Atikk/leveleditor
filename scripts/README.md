validate_maps.py

Small helper to validate JSON map files in the repository `maps/` directory.

Usage (PowerShell):

    python .\scripts\validate_maps.py

What it checks:
- JSON parse correctness
- Required keys: cols, rows, map
- map is rows x cols
- optional passability grid presence and correctness (boolean grid)

Exit codes:
- 0: no errors (warnings possible)
- 1: one or more errors found

Suggested next steps:
- If your maps are missing a passability grid, consider adding a `passability` field with a boolean grid matching rows x cols where `true` means passable and `false` means blocked.
