# DotGame Roadmap

This document mirrors the project's roadmap and tracks progress against the implementation plan described in `docs/core-concepts-blueprint.md` and the repo todo list.

Last update: 2025-10-24 — progress consolidated after the preview/DI migration work.

## Overview
Primary goals:
- Centralize game domain logic in `DotGame.Core` so UI projects (Avalonia) are thin adapters.
- Provide a runtime preview host (EditorGame) that the editor can start/stop via a core contract.
- Make services (TileService, preview, resource manager) available via DI so they are testable and replaceable.
- Improve CI and test coverage so changes are validated automatically.

---

## Progress summary (completed)
The following work has been completed and validated locally and in CI where applicable:

- Canonical domain relocation: core domain types and logic centralized in `DotGame.Core` (maps, tile history, services).
- Removed legacy UI-side domain duplicates from the Avalonia project.
- Preview contract: added `DotGame.Core.Services.IPreviewService` allowing core code to request/stop previews without UI types.
- UI adapters:
  - `Dotgame.Avalonia.Services.Adapters.MonoGamePreviewAdapter` (headless-friendly adapter used by tests).
  - `Dotgame.Avalonia.Services.EditorWindowPreviewService` (EditorWindow-backed adapter that marshals to the UI thread).
- EditorWindow preview wrappers: `EditorWindow.StartPreview(string? mapSerialized = null)`, `StopPreview()`, and `IsPreviewRunning` implemented and wired.
- In-memory loader: `Map.LoadFromJsonString(string json, string? baseDirectory = null)` to start previews from JSON without temp files.
- Nullable-analysis and small bug fixes in `Map.cs` and `TileServiceAdapter.cs` to reduce warnings and avoid CS8602 in CI.
- Tests added/updated:
  - `DotGame.Tests`: DI wiring and preview behavior tests (`PreviewDIWiringTests`, `PreviewServiceBehaviorTests`, `PreviewLifecycleTests*`).
  - Headless EditorWindow integration test added (`EditorWindowIntegrationTests`) to exercise the EditorWindow preview wiring on the UI dispatcher in tests.
- CI and automation:
  - Added a CI workflow that runs solution build, deterministic telemetry sweeps, and runs the telemetry analyzer.
  - Added a telemetry analyzer script and fixed a header issue that previously broke CI.
  - Added a CI lint rule to prevent UI projects from declaring `namespace DotGame.Core` (prevents domain duplication).
- Documentation: updated `README.md` with a concise 'Roadmap progress' summary.

Validation:
- Local solution builds and unit tests pass after these changes.
- The telemetry sweep CI job was fixed (analyze script header) and retriggered; builds complete and telemetry exports are produced (the analyzer aggregates CSV exports).

---

## Work plan (priority, with status)
1. Inventory Windows Forms files — completed.
   - Files catalogued under `src/DotGameCSharp/`.

2. Extract & port remaining domain logic (Map, Character) — mostly completed.
   - `Map` logic exists in `DotGame` and `DotGame.Core`; `Map.LoadFromJsonString` added to make previews easier.

3. Wire runtime preview (EditorGame) — completed (editor wiring and adapters in place).
   - EditorWindow hosts `RuntimePreviewHostControl` and creates `EditorGame` instances when starting previews.

4. Port and adapt rendering loop (MonoGame/Skia) — partially done.
   - EditorGame / MonoGame integration exists; full rendering and input forwarding are in the runtime host control.

5. Port UI windows to Avalonia — in-progress/ongoing.
   - Main editor UI is available in `DotGame` (Avalonia); some auxiliary dialogs may still need conversion.

6. Replace platform services & dialogs (DI) — in-progress.
   - Many services are already DI-registered at startup; continue to remove direct `new` usage and prefer service resolution.

7. DI for TileService and other services — completed for core adapters.
   - `TileServiceAdapter` exposes UI tile logic to `DotGame.Core.Services.ITileService`.

8. Unit tests coverage (Map, TileService, preview wiring) — completed for the critical paths.

9. Build & CI checks — basic CI is in place and green after fixes; consider expanding to matrix builds.

10. Improve asset pipeline, UX, and engine features — backlog (plenty of follow-ups).

---

## Files and locations of key changes
- Core preview contract: `DotGame.Core/Services/IPreviewService.cs`
- UI adapters: `DotGame/src/Services/Adapters/MonoGamePreviewAdapter.cs`, `DotGame/src/Services/EditorWindowPreviewService.cs`
- EditorWindow preview wrappers & test hooks: `DotGame/src/Views/EditorWindow.axaml.cs`
- In-memory loader: `DotGame/src/Models/Map.cs` (added `LoadFromJsonString`)
- Tests: `DotGame.Tests/*` (DI wiring, preview behavior, EditorWindow integration) and `DotGame.Core.Tests` where applicable.
- CI: `.github/workflows/ci.yml` and `scripts/analyze-job-system-telemetry.py`

---

## How to validate locally
1. Build the solution (requires .NET 8 SDK):

```powershell
dotnet build leveleditor.sln --configuration Release
```

2. Run tests:

```powershell
dotnet test leveleditor.sln --configuration Release --no-build
```

3. To run the Avalonia app on Windows (dev helper):

```powershell
#.\run-windows.ps1
```

Notes: unit tests include headless-friendly tests that initialize Avalonia's Skia platform; CI runs the same tests in a headless environment.

---

## Remaining recommended next steps
- Expand CI to run a wider matrix (Windows/Linux, different job system backends) if you want the telemetry sweep to run across platforms.
- Continue migrating any remaining UI files that reference core domain types — the CI lint rule will help catch regressions.
- Improve test coverage for EditorWindow-backed preview lifecycle (e.g., assert EditorGame state) if you want deeper runtime assertions; this requires more runtime assets or mock instrumentation.
- Write a short contributor guide documenting how to register UI adapters in `Program.cs` and how to add new core contracts.

---

This file is intended to be a concise snapshot of progress. The authoritative, machine-readable source of task state is the repository todo list (maintained during development). Update that list first and then refresh this document when the plan changes.

---

## Next 90 days (actionable)

This section translates the current work plan into a focused, testable 90-day execution plan with owners, estimated milestones, and success criteria. Each item below is intentionally small and verifiable — when complete, convert it into issues/milestones in GitHub.

1) Stabilize CI & expand test matrix — Owner: infra / core maintainer (week 0–3)
   - Goal: Ensure solution builds and tests run on Windows and Linux in CI; add a minimal matrix for .NET 8 and .NET 7 compatibility if feasible.
   - Deliverables: updated `.github/workflows/ci.yml` with matrix entries, passing runs for both OSes.
   - Success criteria: CI green on both Windows and Ubuntu runners for the default test suite within 3 attempts.

2) Finalize Core/Adapter contracts & reduce domain leakage — Owner: core maintainer (week 1–6)
   - Goal: Remove remaining direct domain type references from UI projects; ensure all core services are referenced via interfaces in `DotGame.Core`.
   - Deliverables: linter rule verification, PR to update any remaining references, small migration notes in `docs/`.
   - Success criteria: No files in UI projects using `namespace DotGame.Core` per CI lint; tests covering preview startup/shutdown pass.

3) Editor preview robustness & test coverage — Owner: runtime/editor dev (week 2–8)
   - Goal: Increase test coverage around preview lifecycle, add edge tests for starting previews from JSON strings and concurrent start/stop calls.
   - Deliverables: 4–6 new unit/integration tests added to `DotGame.Tests` and `DotGame.Core.Tests` covering startup, stop, and error conditions.
   - Success criteria: Added tests pass locally and in CI; code paths for preview error handling exercised by tests.

4) UX & Avalonia migration sprint — Owner: UI/UX dev (week 4–12)
   - Goal: Convert the last 2–4 remaining Windows Forms dialogs to Avalonia and remove any platform-specific fallbacks.
   - Deliverables: PR(s) converting dialogs, updated screenshots in `docs/`, and a short contributor note on registering adapters.
   - Success criteria: All editor workflows exercised manually (start/stop preview, open/save map, tile editing) work on Avalonia without crashing.

5) Asset & tile pipeline improvements — Owner: asset lead (week 6–12)
   - Goal: Simplify creating/previewing maps and tiles in the editor and reduce friction for contributors adding assets.
   - Deliverables: small CLI or script (`scripts/`) to validate tilesets, and documentation in `docs/` describing the asset layout.
   - Success criteria: Contributors can add a tileset and run a validation script that returns pass/fail with actionable messages.

6) Convert roadmap items to issues & set milestones — Owner: project lead (week 0–2)
   - Goal: Create GitHub issues for the above items, assign owners, and set a 90-day milestone so progress is tracked in the issue tracker.
   - Deliverables: issues created with checklists, milestone created for the 90-day plan.
   - Success criteria: Each of the 1–5 items has an issue and the milestone is populated.

Quick notes and assumptions
--
- Assumes maintainers have permissions to change GitHub workflows and create milestones.
- If running matrix CI is too heavy, start with a single Ubuntu runner and expand after stability is confirmed.
- Tests that depend on graphical components should prefer headless adapters or be ignored for headful runners.

Next actions (recommended)
- Convert each numbered item above into an issue and attach this section as an implementation note.
- Triage the infra tasks first (CI + milestone creation) so contributors can rely on consistent validation.

---

Update history: 2025-10-24 — appended actionable 90-day plan.

