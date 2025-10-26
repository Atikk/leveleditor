Title: Editor preview robustness & test coverage
Labels: tests, preview
Assignees: TBD
Milestone: 90-day plan: Oct 24 2025 — Jan 22 2026

Goal
----
Increase test coverage around the preview lifecycle. Add tests that exercise starting from JSON, concurrent start/stop, and error handling in headless and EditorWindow contexts.

Deliverables
----
- Add 4–6 tests in `DotGame.Tests` and/or `DotGame.Core.Tests` covering start/stop/error cases.
- Create a small test helper for generating serialized maps used in tests.

Success criteria
----
- New tests pass locally and in CI.
- The preview-related code path has at least 80% coverage on critical methods (goal, not hard requirement).

Notes
----
Prefer headless adapters and mocks to avoid unreliable UI dependencies in CI.
