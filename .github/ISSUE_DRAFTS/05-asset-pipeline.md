Title: Asset & tile pipeline improvements
Labels: assets, tools
Assignees: TBD
Milestone: 90-day plan: Oct 24 2025 — Jan 22 2026

Goal
----
Make it easier for contributors to add tilesets and validate assets before running the editor. Provide small tooling and documentation for the asset layout.

Deliverables
----
- A small validation script in `scripts/` that checks a tileset for expected metadata and image sizes.
- Documentation in `docs/` describing the asset layout and contributor flow.

Success criteria
----
- Contributors can run the validation script and receive clear pass/fail output with actionable messages.

Notes
----
Start with a Python or PowerShell script depending on contributor preference; keep the dependencies minimal.
