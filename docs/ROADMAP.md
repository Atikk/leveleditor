# DotGame — Focused Roadmap

Last update: 2025-10-24

This file is a concise, practical snapshot of repository health and priorities. It intentionally avoids a long prescriptive schedule and instead records current state, what we've completed, and what remains to be done.

## 1) High-level summary

- Project: `leveleditor` / DotGame — a C#/.NET 2D tile-based level editor and small runtime player.
- Key capabilities: visual map editor (tileset loading, painting, adjustable brush, JSON save/load) and a simple game player (named characters, tile-based movement, frame-based animations).
- Platform: originally Windows Forms (src/DotGameCSharp/). An Avalonia-based demo exists for cross-platform testing.
- Map format: JSON with embedded base64 images per tile (simple but not optimal for larger projects).

## 2) Current state — completed work (high level)

The following items are completed and present in the repository or in this branch:

- Domain consolidation: core game/data logic moved or centralized around `DotGame.Core` (maps, tile history, services).
- Preview contract & adapters: `DotGame.Core.Services.IPreviewService` plus adapters and EditorWindow wiring to start/stop previews from the editor.
- In-memory map loader: `Map.LoadFromJsonString` to start previews directly from JSON strings.
- Nullable-safety and small bug fixes: reduced CS8602 and related warnings in key files (Map, TileServiceAdapter).
- Tests: focused unit/integration tests added for DI wiring, preview lifecycle, and editor preview hooks (`DotGame.Tests`, `DotGame.Core.Tests`).
- CI basics: a CI workflow exists that builds the solution and runs telemetry/analysis scripts; a lint rule prevents UI projects from declaring `namespace DotGame.Core` to avoid accidental domain duplication.
- Tooling & migration helpers (recent additions):
  - `tools/MapPassabilityTool` — .NET console tool to validate and optionally inject `passability` jagged arrays into `maps/*.json` (dry-run default; `--apply` writes files and creates `.bak`).
  - `scripts/validate_maps.py` and `scripts/add_passability.py` — Python helpers for validation and batch injection (Python required).
  - GitHub helpers: issue templates, draft issues (`issues/`), a PR template, and a milestone summary (`docs/MILESTONE-90-DAYS.md`) to convert roadmap items into tracked work.

These completed items make it straightforward to validate maps for passability metadata, run a dry-run migration, and begin an incremental, reviewable migration of data and CI checks.

## 3) Outstanding work (what still needs to be done)

The repository is a solid prototype but several meaningful items remain before the project can be considered a polished, cross-platform editor/runtime or a reusable engine. Key outstanding tasks:

- Full Avalonia port for editor UI: the demo runs, but several auxiliary dialogs and workflows still rely on Windows Forms; these need porting for a complete cross-platform UX.
- Asset pipeline and tileset export: avoid embedding full PNG data in map JSON. Add exporters that extract tiles into `assets/` folders and rewrite maps to reference files instead of base64 blobs.
- Passability migration: many `maps/*.json` files still lack a `passability` field. Use the `tools/MapPassabilityTool` (or Python scripts) in a controlled migration to add passability grids and commit the results with backups.
- CI integration for validation: add a CI step that runs the validator in dry-run (or strict mode after migration) to prevent malformed maps from landing.
- Broader unit test coverage: expand tests to cover map loading edge cases, Save/Load roundtrips, tile metadata, and history/undo if added.
- Structured logging and error handling: introduce `Microsoft.Extensions.Logging` or Serilog for core and adapters; add more robust exception handling for IO/JSON errors.
- Editor UX improvements: undo/redo, multiple tile layers, visible tile metadata editing (collision/trigger flags), and map export options.
- Small engine features (optional): audio playback, simple NPC AI/patrol behavior, and a scripting surface (Lua/C#) if the project aims to support modding or complex behaviors.
- Performance improvements: consider reducing the memory/IO cost of embedded base64 tiles and, if needed, adopt more efficient data structures for large maps.

## 4) Risk & stability notes

- The codebase appears stable for its scope (editor + small runtime). Unit tests and CI are present for core paths, but gaps remain for edge cases.
- Map JSON format with embedded images is functional but brittle at scale — migrating to file-backed tilesets reduces repository bloat and improves editor performance.
- Changes that modify `maps/*.json` should be done with backups and code review; the repo now includes tools to help do that safely.

## 5) How to validate locally (quick commands)

Build and run tests:

```powershell
dotnet build "leveleditor.sln" --configuration Release
dotnet test "leveleditor.sln" --configuration Release --no-build
```

Map validation (dry-run):

```powershell
dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps
```

Map migration (creates `.bak` backups) — run only after review/approval:

```powershell
dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps --apply
```

Or use the Python helpers (if you have Python installed):

```powershell
python .\scripts\validate_maps.py .\maps
python .\scripts\add_passability.py .\maps
```

## 6) Suggested immediate next steps (non-actionable guidance)

- Run the passability tool in dry-run and review which maps lack `passability` metadata.
- Decide whether to migrate all maps at once or migrate incrementally as parts of the repo are touched.
- Add a CI dry-run validator step to catch malformed maps early.
- Begin porting the highest-priority Windows Forms dialogs to Avalonia so cross-platform testing covers common editor flows.

---

If you want, I can open a branch/PR with the migration applied (with `.bak` backups), or I can run the validator here and paste the dry-run output so you can review before any write operation. Which would you prefer?
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

## Status update — recent repository changes (2025-10-24)

Since the last consolidation the repository received several practical additions to help migrate maps and track roadmap work. These are small, low-risk artifacts intended to make the next steps (map migration, CI checks, and incremental features) easier to implement and review.

Completed and added locally in this branch:
- A small C# dotnet console tool: `tools/MapPassabilityTool` — validates `maps/*.json` for `passability` and (optionally) injects a default `passability` jagged bool[][]. It supports `--maps-dir` and `--apply` and creates `.bak` backups before writing. See `tools/MapPassabilityTool/README.md` for usage.
- Two lightweight Python helpers (kept in `scripts/`): `scripts/validate_maps.py` and `scripts/add_passability.py` for validating maps and batch-injecting passability metadata (Python required to run them).
- Issue templates and draft issues under `.github/ISSUE_TEMPLATE` and `issues/` (for passability, asset pipeline, Avalonia port, CI matrix) to convert the 90-day plan into actionable GitHub issues.
- A PR template (`.github/PULL_REQUEST_TEMPLATE.md`) and a milestone summary at `docs/MILESTONE-90-DAYS.md` to help triage and track work.

Recent follow-ups (2025-10-25):

- Added a small set of unit tests in `DotGame.Core.Tests/MapPassabilityTests.cs` that exercise passability behaviour (initialize with passability, dimension-mismatch cases, and null-passability handling). These tests are intended as a starting point and can be expanded with roundtrip/save-load tests.
- Added a GitHub Actions workflow `.github/workflows/validate-maps.yml` that runs the `tools/MapPassabilityTool` in dry-run for pushes and pull requests touching `maps/` or the tool itself. The workflow fails if the validator reports missing passability entries or errors, providing immediate protection for PRs that touch maps.
- Per a local, reviewed change, `maps/mymap.json` now contains a `camera` metadata block with position and a quaternion rotation (metadata only; renderer changes not included). This is a small experiment to store 3D camera metadata for previews.

What is *not* yet applied to production maps or CI:
- `maps/*.json` files have not been modified automatically in the repository — the MapPassabilityTool and Python scripts currently run in dry-run mode by default; `--apply` must be used to modify files and commit those changes. A considered migration plan with review and backups is recommended.
- The CI validator workflow has been added in dry-run form (`.github/workflows/validate-maps.yml`) and will run on PRs/pushes; after a migration is agreed it can be tightened to be stricter or extended to run in other CI contexts.

Quick commands to reproduce locally (PowerShell):

```powershell
dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps
dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps --apply
# or (if you prefer Python helpers):
python .\scripts\validate_maps.py .\maps
python .\scripts\add_passability.py .\maps
```

If you want, I can run the MapPassabilityTool here in dry-run and share the output; run-and-apply changes require confirmation because they modify `maps/*.json` (backups are created).


## Roadmap coverage & assessment

Summary of how the repository maps to the roadmap parts and a short assessment of non-roadmap findings.

- Part I – Foundations: Partial — The project implements a 2D tile engine and editor in C#. It includes both a visual map editor and a simple game player. Essential math is minimal and it follows common C# practices (OOP, JSON serialization) but does not implement advanced engine-level features.
- Part II – Systems: None — No custom allocators, logging/assertion framework, or a job system/multithreading primitives are present.
- Part III – Graphics: None — Rendering relies on Windows Forms/Avalonia and there is no custom rendering pipeline, shaders, or scene graph.
- Part IV – Animation: Partial — Basic frame-based character animations exist (sprite frame swaps during movement). No blending, IK, or compression systems.
- Part V – Physics: None — Movement is strictly tile-based; no physics or collision engine is implemented.
- Part VI – Scripting/AI: None — No embedded scripting or significant AI systems; characters are player-controlled and there is no ECS apparent.
- Part VII – Assets/Editor: Partial — The editor UI is present and maps are saved as JSON (embedded base64 images). There is no full asset-pipeline or DCC integration.
- Part VIII – Misc Systems: None — No audio, networking, or profiling/diagnostics beyond what .NET provides.
- Part IX – Performance: None — No data-oriented design or multi-core scaling techniques are used.

Additional (non-roadmap) observations:
- Cross-Platform UI: An Avalonia demo exists alongside Windows Forms, and the README documents Linux/VNC workflows.
- Replit/Cloud Setup: Scripts and cloud/CI assets exist for running in web/VNC environments.
- JSON Map Format: Maps use JSON with embedded images (base64), a simple but suboptimal asset representation for larger projects.
- Web components: A non-trivial amount of JS/CSS suggests an accompanying web/demo or tooling layer.
- Testing scaffold: `DotGame.Core.Tests` and `DotGame.Tests` provide unit tests for core logic and adapter wiring.

Stability note: The project appears stable for its current scope (editor + small runtime), but lacks broader engineering tooling (robust logging, comprehensive tests for edge cases, structured asset pipeline).

Key short-term recommendations (workplan highlights)
- Complete the Avalonia port for remaining editor dialogs so the app is fully cross-platform (high priority if cross-platform support is a project goal).
- Add quality-of-life engine features: simple audio playback, tile passability/collision flags, a small AI/patrol behavior for NPCs, and an optional scripting surface (Lua/C#) for modding.
- Improve asset pipeline: avoid embedding full PNGs in map JSON where possible; provide project-level tileset folders and a lightweight asset validator script.
- Increase code quality: add structured logging (Serilog or Microsoft.Extensions.Logging), consistent exception handling, and unit tests for map loading/character behavior.
- UX and editor features: undo/redo, multiple tile layers, tile metadata (collision/trigger), and a way to export a playable bundle.
- Document contributor workflows: how to register UI adapters, run the Avalonia demo, and add CI matrix entries.

These recommendations are intended to be practical first steps to move the project from a solid prototype toward a more production-ready editor/runtime.


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


## Quick project snapshot

- What this repo is: a C#/.NET 2D tile-based level editor and simple runtime (editor + player). It exposes a Windows Forms origin and an Avalonia demo for cross-platform UI.
- Key implemented features: visual map editor (tileset loading, painting, adjustable brush), JSON save/load (maps include embedded base64 images), a player runtime with named characters, tile-based movement, and basic frame-based animations.

Summary: the codebase is a solid prototype that cleanly separates core game/data logic (`DotGame.Core`) from UI adapters. It is not a full engine (no physics, advanced rendering, or scripting), but it is well organized and has a modest test scaffold and CI assets.

## Roadmap coverage (short)

- Part I — Foundations: Partial — core domain (maps, tiles, characters) implemented with JSON serialization and OOP patterns. No advanced engine math or memory systems.
- Part II — Systems: None — lacks custom allocators, logging framework, or job-system primitives.
- Part III — Graphics: None — rendering delegated to Windows Forms/Avalonia; no custom pipeline or shaders.
- Part IV — Animation: Partial — frame-based sprite animations exist; no blending, IK, or compression.
- Part V — Physics: None — tile-based movement only; no physics or collision system.
- Part VI — Scripting/AI: None — no embedded scripting or rich AI systems; no ECS.
- Part VII — Assets/Editor: Partial — editor UI exists and maps are saved as JSON (embedded images). No structured asset DB or DCC integration.
- Part VIII — Misc: None — no audio, networking, or built-in profiling beyond .NET.
- Part IX — Performance: None — no DOD/multi-core scaling.

Non-roadmap highlights:
- An Avalonia demo exists; README documents Linux/VNC support and there are scripts for cloud/VNC deployment.
- There are web/JS assets (~38% of repository), likely demo/telemetry or tooling code.
- Tests exist in `DotGame.Core.Tests` and `DotGame.Tests`, including adapter and preview lifecycle tests.

Stability note: the project seems stable for its current scope but lacks robust logging, broad unit coverage for edge cases, and an asset pipeline suited to larger projects.

## Recommended priorities (practical, ordered)

1) Finish the Avalonia port so editor workflows run cross-platform (high priority if cross-platform use is a goal).
2) Add small engine quality-of-life features: tile passability (collision flags), basic audio playback, and simple NPC patrol AI.
3) Improve the asset pipeline: avoid embedding full PNGs inside map JSON; provide tileset folders and a validation script.
4) Improve reliability and maintainability: add structured logging (Serilog or Microsoft.Extensions.Logging), better exception handling, and expand unit tests for map loading and editor operations (undo/redo, save/load edge cases).
5) UX: undo/redo, multiple layers, tile metadata (collision/trigger), and a way to export playable bundles.

These items are intentionally low-risk and give measurable value quickly.

## Short implementation plan (90 days, focused)

Convert the plan below into GitHub issues and a 90-day milestone. Each item is small and testable.

1) Stabilize CI & expand minimal matrix (weeks 0–3)
   - Ensure solution builds and tests run on Windows and Ubuntu in CI.
   - Deliverable: CI workflow with a small matrix; passing runs.

2) Finish core/adapter separation (weeks 1–6)
   - Remove remaining direct domain references from UI projects; expose contracts in `DotGame.Core`.
   - Deliverable: no UI files under `DotGame` referencing `DotGame.Core` types directly (enforced by lint rule).

3) Improve preview lifecycle tests (weeks 2–8)
   - Add tests to exercise starting/stopping previews from JSON and concurrent start/stop behavior.
   - Deliverable: 3–6 new tests in `DotGame.Tests` and/or `DotGame.Core.Tests`.

4) Avalonia migration sprint (weeks 4–12)
   - Convert ancillary dialogs to Avalonia; verify editor workflows on Linux/Avalonia.
   - Deliverable: PRs converting remaining dialogs; manual verification checklist in `docs/`.

5) Asset & editor QoL (weeks 6–12)
   - Add tile passability metadata, an asset validator script under `scripts/`, and a small exporter that avoids storing full encoded images per tile.
   - Deliverable: `scripts/validate-tileset.*`, map exporter, and tests.

## Files to inspect next

- `DotGame.Core/Maps/Map.cs`
- `DotGame/src/Views/EditorWindow.axaml.cs`
- `DotGame/src/Services/TileServiceAdapter.cs`
- `DotGame.Tests/*`

## How to validate locally

1) Build:

```powershell
dotnet build "leveleditor.sln" --configuration Release
```

2) Tests:

```powershell
dotnet test "leveleditor.sln" --configuration Release --no-build
```

3) Run Avalonia demo (Windows helper):

```powershell
# .\run-windows.ps1
```



