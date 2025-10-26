Title: chore(maps): add camera quaternion metadata to sample.json and simple.json

Summary:
This small PR adds a `camera` metadata block to two test maps (`maps/sample.json` and `maps/simple.json`) so the editor can store simple 3D preview camera state. The change is metadata-only and includes `.bak` backups for both files.

Why:
- Small, reviewable experiment to validate the edit/PR/CI workflow before larger data migrations.
- Exercises map editing, backups, and CI dry-run validation without changing runtime behavior.

Files changed:
- maps/sample.json (camera block added)
- maps/simple.json (camera block added)
- maps/sample.json.bak (backup)
- maps/simple.json.bak (backup)

Acceptance checklist:
- [ ] CI builds and tests pass
- [ ] Map validator (dry-run) reports no errors for changed files
- [ ] One reviewer verifies loading `maps/sample.json` and `maps/simple.json` in the editor preview

Notes:
- The passability validator in CI remains in dry-run for now. This PR is intentionally minimal and safe.
