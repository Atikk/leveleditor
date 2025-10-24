Title: Stabilize CI & expand test matrix
Labels: infra, ci
Assignees: TBD
Milestone: 90-day plan: Oct 24 2025 — Jan 22 2026

Goal
----
Ensure the repository CI reliably builds the solution and runs the tests on both Windows and Ubuntu runners. Add a minimal matrix entry for OS and consider adding a .NET SDK matrix entry if stable.

Deliverables
----
- Update `.github/workflows/ci.yml` to include an Ubuntu runner and a minimal matrix (OS: windows-latest, ubuntu-latest).
- Confirm `dotnet build` and `dotnet test` succeed on both runners.
- Add short docs in `docs/` for CI expectations.

Success criteria
----
- CI runs green on Windows and Ubuntu for the default test suite.
- Any flaky tests are documented or temporarily quarantined with a linked follow-up issue.

Notes / commands
----
Use the existing build steps documented in `docs/ROADMAP.md` as the starting point. If bandwidth is limited, start with Ubuntu + your current Windows runner and expand later.
