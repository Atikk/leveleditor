Title: Finalize Core/Adapter contracts & remove domain leakage
Labels: core, refactor
Assignees: TBD
Milestone: 90-day plan: Oct 24 2025 — Jan 22 2026

Goal
----
Ensure UI projects (Avalonia/Editor) do not directly reference domain types from `DotGame.Core`. All core services should be provided via interfaces defined in `DotGame.Core`.

Deliverables
----
- Audit UI projects for any remaining `using DotGame.Core` or direct domain type references.
- Migrate remaining references to adapter interfaces (add small adapter shims where necessary).
- Add or update CI lint rules and verify no regressions.

Success criteria
----
- CI lint rule passes: no files in UI projects declare `namespace DotGame.Core`.
- All preview- and tile-related code paths use `DotGame.Core` interfaces.

Notes
----
This is low-risk refactoring; prefer small PRs to keep reviews manageable.
