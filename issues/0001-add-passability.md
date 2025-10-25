---
title: Add tile passability metadata and validator
labels: enhancement, roadmap, passability
---

## Summary

Add `passability` metadata to maps and a validator/CI check. Provide tool(s) to migrate existing `maps/*.json` files by inserting a default `passability` jagged bool[][] where missing.

## Motivation

The editor and core map model support passability, but many maps lack the metadata. This issue ensures consistency and enables collision-aware gameplay and editor UX.

## Acceptance criteria
- [ ] A repository tool exists (MapPassabilityTool or equivalent) that validates `maps/*.json` files.
- [ ] The tool can run in dry-run mode and an `--apply` mode that creates `.bak` backups before writing.
- [ ] CI includes a dry-run validation job (optional until migration completes).
- [ ] Existing maps are migrated (or documented) with passability defaulted to `true` where previously missing.

## Implementation
- Create/land `tools/MapPassabilityTool` (done).
- Run dry-run against `maps/` and confirm results.
- With approval, run `--apply` and commit changes.

## Estimate
1–2 days
