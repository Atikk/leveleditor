---
title: Expand CI to run on Windows and Ubuntu (minimal matrix)
labels: ci, roadmap
---

## Summary

Ensure the solution builds and tests run on both Windows and Ubuntu in CI.

## Acceptance criteria
- [ ] CI workflow updated to include a small matrix (windows-latest, ubuntu-latest).
- [ ] All tests run and pass on both platforms.
- [ ] Lint rule preventing UI projects from declaring `namespace DotGame.Core` enforced.

## Estimate
1–2 days
