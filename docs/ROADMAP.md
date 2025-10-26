
# DotGame — Focused Roadmap (revised)

Last update: 2025-10-26

This document records the project's goals (Parts I–IX), the current implementation status for each area, and a concise list of what's completed vs outstanding. The aim is to make the long-term engine goals explicit while keeping the repository-level migration and CI tasks auditable and actionable.

Summary:
- Project: `leveleditor` / DotGame — C#/.NET 2D tile-based editor + small runtime.
- Short-term focus: stabilize tooling, tests, and safe data migrations (map passability + small metadata experiments). 
- Long-term goal: evolve the repo toward engine-grade modules across Parts I–IX (see below).

---

## Part I — Foundations (engine vs. game, architecture, math)

Goal: Establish engineering fundamentals — clear separation of engine vs game, recommended C++ patterns (when porting native subsystems), real-time loop design, and key math primitives.

Status: Partial — foundational work exists but needs formalization and docs.

Completed
- Core domain consolidation into `DotGame.Core` (maps, tile history, services).
- In-memory map loader: `Map.LoadFromJsonString`.
- Basic unit/integration tests and CI that validate core paths.

Outstanding
- Formal engine vs game guidance docs (roles/responsibilities, recommended C++ patterns for engine subsystems).
- A canonical, documented deterministic loop (60/120 FPS) reference and tests.
- Math fundamentals doc (vectors/matrices/coordinate systems) and examples for engine consumers.

---

## Part II — Systems (memory allocators, logging, job system)

Goal: Implement custom memory allocators, global logging/assertion framework, OS/build abstraction layer, and a robust job system with synchronization primitives.

Status: Not started / scaffolding present

Completed or scaffolding
- Repository structure and DI patterns exist; some tests exercise concurrency in small scopes.

Outstanding
- Custom allocators and fragmentation control.
- Structured logging and assertion framework (Microsoft.Extensions.Logging or Serilog integration planned).
- OS abstraction and build-system guidance (platform-specific layers, CI matrix expansion).
- Comprehensive job system and sync primitives (design & implementation).

---

## Part III — Graphics & Rendering

Goal: Provide configurable render architectures, scene graph / culling, lighting/post-processing, PBR/HDR, compute shader support and performance knobs.

Status: Partial (UI adapters & runtime present; advanced pipeline not implemented)

Completed or scaffolding
- Avalonia demo + MonoGame runtime integration for preview host.
- EditorGame runtime host and adapters to drive previews.

Outstanding
- Scene graph, culling stack, render passes, deferred/forward pipelines.
- Advanced lighting, shadows, post-processing, PBR/HDR, and compute support.
- Render architecture docs and examples for pluggable backends.

---

## Part IV — Animation

Goal: Skeletal runtime, blending, IK, compression, retargeting and gameplay animation hooks.

Status: Not started / basic sprite animation present

Completed or scaffolding
- Frame-based sprite animation in the runtime for characters.

Outstanding
- Skeletal animation runtime, blend trees, IK systems, compression/retargeting toolchain.
- Integration hooks for gameplay/physics-driven animation.

---

## Part V — Physics

Goal: Rigid-body dynamics, collision detection pipeline, and optional middleware integrations.

Status: Not started / tile-based passability exists

Completed or scaffolding
- Tile passability tooling and validator (dry-run + `--apply` injector) to model walkable areas.

Outstanding
- Full rigid-body physics engine or integration with a physics middleware (e.g., BEPU/PhysX/Box2D bridge).
- Broad collision pipeline and performance tests.

---

## Part VI — Scripting and AI

Goal: Embed scripting (Lua/Python), provide event/state-machine frameworks, and extend AI tooling beyond triggers.

Status: Partial — triggers exist; ECS tooling present

Completed or scaffolding
- Trigger systems and basic behavior hooks exist; ECS tooling is present structurally.

Outstanding
- Embedded scripting host(s) and safe runtime bindings (Lua/Python).
- Comprehensive event/state-machine toolkit and higher-level AI primitives.

---

## Part VII — Content Pipeline & Editor

Goal: Automated content pipeline, DCC exporter/import hooks, asset DB/versioning, build automation; editor UI to expose pipeline features.

Status: Partial — editor UI present; asset pipeline missing

Completed or scaffolding
- Editor UI and map editor; scripts/helpers for map validation.

Outstanding
- Asset exporter to avoid embedded base64 tiles (prototype planned).
- DCC export/import hooks, asset database, versioning, and build automation.

---

## Part VIII — Audio, Networking & Profiling

Goal: 3D audio mixing, networking/replication layer, and a full profiling/optimization toolkit.

Status: Not started

Outstanding
- Audio engine integration, networking/replication APIs, and profiling tools for CPU/GPU and memory.

---

## Part IX — Data-Oriented Design & Multicore

Goal: Explore data-oriented layouts, multicore scaling strategies, and cloud/distributed runtime examples.

Status: Not started / concepts acknowledged

Outstanding
- Modular experiments converting core subsystems to data-oriented layouts, multicore job scheduling, and sample cloud-hosted runtime examples.

---

## Core Concepts & Cross-cutting items

- Deterministic loop tooling (60/120 FPS) — Partial: plan exists; reference implementation and tests needed.
- Advanced rendering abstraction layer — Outstanding: design and examples required.
- Data-oriented layout utilities & modular layering docs — Outstanding: structural modularity exists but needs formal documentation and examples.

---

## Short-term roadmap (practical next actions)

1. Stabilize CI and expand test matrix (Windows + Ubuntu) — owner: infra — ETA: 1 week.
2. Merge tools/tests/CI PRs (current) so reviewers can validate the passability tooling in dry-run.
3. Small experiments: open the camera-quaternion branch PR and validate CI/dry-run on that branch.
4. Expand tests (Save/Load roundtrips, malformed JSON) and run full solution tests before any `--apply` migration.
5. Prototype asset exporter (dry-run): extract embedded tiles to `assets/` and produce rewritten maps. Add tests.

---

## Acceptance criteria for major migrations

- All CI checks pass (build + test matrix).
- Dry-run validator reports no missing passability entries on the migration branch.
- Backups (`*.bak`) present in migration PRs and scheduled for removal after a stabilization period.

---

If you want, I will commit a follow-up that converts this roadmap into tracked `issues/` and a 90-day milestones list (owners/ETAs). For now this file documents Parts I–IX, the current status per part, and short-term actionable steps.


# DotGame Roadmap
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

Recent verification (2025-10-25)

- Added Save/Load roundtrip and passability roundtrip tests in `DotGame.Core.Tests/MapSaveLoadTests.cs` to reduce migration risk. These tests exercise `Map.InitializeFromArray`, `Map.Clone`, `SetPassability`, and `GetPassabilityAsJagged`.
- Local solution build and tests executed; `DotGame.Core.Tests` passed locally (24 tests passed). This increases confidence before any mass-edit migration of map JSON files.

Recommended immediate next steps (practical)

1. Re-run the passability validator in dry-run and capture the report. This confirms which maps will be modified if the migration is applied.
2. Decide migration strategy (pick one):
   - A — Keep the draft PR (tools/tests/CI) only. Leave CI validator in dry-run mode and postpone map edits.
   - B — Apply the migration to all maps now (safe mode: backups `.bak` are created). Creates `chore/add-passability` branch and PR for review.
   - C — Continue expanding tests (more Save/Load/LoadFromJsonString roundtrips and malformed JSON cases) before performing any `--apply` migration.

If you'd like me to run the validator dry-run and paste the full report here, reply "run validator". To proceed with migration, reply "B". To continue expanding tests, reply "C". To keep the draft PR only, reply "A".

## 7) Continuation — concise decision matrix and next actions

This repository is ready for one of three safe, auditable next steps. Pick one and I will execute the corresponding commands and follow-up PR flow.

- Option A — Keep draft PR (tools/tests/CI only)
   - When to pick: you want more review before touching map JSON, or prefer to migrate maps incrementally later.
   - Outcome: I will leave CI map validation in dry-run. No `maps/*.json` files are changed.
   - What I will do: mark the migration-decision todo as "deferred" in the repo metadata and keep the `.github` workflow in dry-run.

- Option B — Apply the migration to all maps now (all-at-once)
   - When to pick: you accept the `.bak` backup strategy and want repository-wide consistency fast.
   - Outcome: all maps missing `passability` will be updated with a conservative default jagged bool[][], `.bak` backups will be created beside each edited map, and a branch `chore/add-passability` with a PR will be opened.
   - What I will do (on approval):
      1. Run the passability tool with `--apply` and create `.bak` files.
      2. Commit edits and backups to a new branch `chore/add-passability` and push the branch.
      3. Open a PR with the acceptance checklist: CI green, dry-run validates no missing passability, and at least one reviewer validates map loads for representative maps.
   - Exact commands I will run (PowerShell):

```powershell
dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps --apply
git checkout -b chore/add-passability
git add maps/*.json maps/*.json.bak
git commit -m "chore(maps): add default passability grids (backups included)"
git push --set-upstream origin chore/add-passability
```

- Option C — Expand tests first (recommended if you want extra safety)
   - When to pick: you want additional Save/Load/LoadFromJsonString roundtrips and malformed-JSON tests before altering data.
   - Outcome: I will add targeted tests in `DotGame.Core.Tests` (roundtrip and negative cases), run the suite, re-run the dry-run validator, and resurface the updated report so you can pick A/B/C again with better confidence.
   - What I will do: add 2–4 small tests (roundtrip, LoadFromJsonString roundtrip, malformed JSON handling) and run `dotnet test`. If all pass, I will re-run the dry-run validator and attach the results.
   - Commands I will run (PowerShell):

```powershell
dotnet test "leveleditor.sln" --configuration Release --no-build
dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps
```

Custom proposal: If you prefer an incremental migration, say so — I can implement a branch strategy where `--apply` runs only for maps changed by a given PR (or only for a given list of map files). This is a fourth option and is compatible with the CI dry-run protection already in place.

Please reply with the letter of your chosen path (A/B/C) or a short description of a custom plan and I'll proceed. If you want the dry-run now, reply "run validator" and I'll paste the updated report.


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

## 90-day tracked workplan (concrete tasks)

This section converts the high-level 90-day plan into reviewable, trackable work—one item per PR/issue where possible. Use these as templates for GitHub issues or draft markdown under `issues/`.

Sprint 1 (weeks 0–4) — Stabilize CI & safety
- Issue: `issue/0002-stabilize-ci.md` — Ensure CI runs on Windows and Ubuntu; add map validation dry-run job (already added). Acceptance: matrix CI green on `main` and feature branches.
- Issue: `issue/0003-passability-tooling.md` — Finalize `tools/MapPassabilityTool` docs and usage; attach dry-run report to `issues/0001-add-passability.md`. Acceptance: tool reproduces dry-run results in CI.

Sprint 2 (weeks 4–8) — Domain cleanup & tests
- Issue: `issue/0004-expand-tests.md` — Add Save/Load roundtrip tests, map IO edge cases, and additional passability unit tests. Acceptance: tests added to `DotGame.Core.Tests` and pass locally/CI.
- Issue: `issue/0005-avalonia-dialogs.md` — Port top 3 Windows Forms dialogs needed for common editor flows. Acceptance: dialogs function on Avalonia demo and manual checklist completed.

Sprint 3 (weeks 8–12) — Asset pipeline & migration
- Issue: `issue/0006-asset-exporter.md` — Prototype exporter to extract embedded tiles to `assets/tilesets/<name>/` and rewrite maps (dry-run by default). Acceptance: exporter runs in dry-run and produces expected file layout.
- Issue: `issue/0007-add-passability.md` — Migration PR `chore/add-passability` (all-at-once) or incremental strategy. Acceptance: PR includes `.bak` files, CI green, dry-run shows no missing passability, at least one reviewer validates map load for representative maps.

Cross-cutting acceptance criteria
- All PRs must include a short checklist (build/tests/dry-run) in the PR description.
- Backups (`*.bak`) must be included in migration PRs and scheduled for removal in a follow-up PR after 14 days if no issues are reported.

Suggested PR naming and branches
- Tools/tests/CI: `chore/roadmap-updates-YYYY-MM-DD` (already created in this branch).
- All-at-once passability migration: `chore/add-passability` (created when `--apply` is run and changes committed).
- Asset exporter prototype: `tools/asset-exporter-prototype`.

Notes
- I created markdown issue stubs for the sprint tasks under the `issues/` directory so they are reviewable and can be converted into GitHub Issues quickly. Files added:
   - `issues/0002-stabilize-ci.md`
   - `issues/0003-passability-tooling.md`
   - `issues/0004-expand-tests.md`
   - `issues/0005-avalonia-dialogs.md`
   - `issues/0006-asset-exporter.md`
   - `issues/0007-add-passability.md`

   These stubs include goals, acceptance criteria, and steps. If you'd like, I can open a small PR that adds these files explicitly (they are already present in the branch) or convert them into GitHub Issues for you (conversion requires repository issue permissions).

## Owners, ETA & per-issue checklist

To turn the 90-day plan into assignable work, below is a compact owners/ETA suggestion and a short checklist for each issue stub. These are proposals you can adapt before creating real GitHub Issues or assigning owners.

- `issues/0002-stabilize-ci.md` — Owner: @devops (TBD) — ETA: 1 week
   - Checklist: confirm matrix entries; fix failing steps; ensure map dry-run runs in CI.

- `issues/0003-passability-tooling.md` — Owner: @tools (TBD) — ETA: 1 week
   - Checklist: finish README, ensure non-zero exit on error, attach full dry-run report to `issues/0001-add-passability.md`.

- `issues/0004-expand-tests.md` — Owner: @core-team (TBD) — ETA: 2 weeks
   - Checklist: add Save/Load roundtrip tests, malformed JSON tests, passability roundtrip tests; run CI.

- `issues/0005-avalonia-dialogs.md` — Owner: @ui-team (TBD) — ETA: 3–4 weeks
   - Checklist: pick top 3 dialogs, implement Avalonia equivalents, add verification steps to `docs/`.

- `issues/0006-asset-exporter.md` — Owner: @tools (TBD) — ETA: 3 weeks
   - Checklist: prototype dry-run, validate file layout, add `--apply` guarded by backups.

- `issues/0007-add-passability.md` — Owner: @core-team (TBD) — ETA: 1–2 weeks (depending on review)
   - Checklist (all-at-once): run `--apply`, commit `maps/*.json` and `*.json.bak` on branch `chore/add-passability`, open PR with acceptance criteria (CI green, validator dry-run clean, reviewer validation).

If you want, I can:
- commit these proposed owners/ETAs into the roadmap as-is (they're placeholders),
- or instead create individual issue drafts that include the checklist and recommended assignees.



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

## 8) Tracked tasks & next PRs (actionable)

This section converts the 90‑day plan into small, reviewable work items and gives explicit branch/PR commands that maintainers can copy. The repository already contains issue stubs under `issues/` and a draft branch for the tooling/tests; use the checklist below to proceed safely.

- Core guidance:
  - Validator and Python helpers run in dry-run by default. Use `--apply` only after code review approval.
  - Destructive edits create `.bak` backups next to the edited map (the tooling does this automatically).
  - Make changes on a branch and open a PR with CI enabled. Keep the map validator in dry-run on PRs until reviewers approve mass edits.

- Short, reviewable tasks (branch / PR commands)

1) Tools / tests / CI-only PR (no map edits)

   Purpose: let reviewers approve tooling and CI before any map data changes.

   Commands:

   ```powershell
   git checkout -b chore/roadmap-updates-YYYY-MM-DD
   # edit / review: tools/MapPassabilityTool, .github workflows, DotGame.Core.Tests additions
   git add tools/ .github/ DotGame.Core.Tests/ docs/ROADMAP.md issues/
   git commit -m "chore(tooling): add map validation tooling, CI dry-run and roadmap tasks"
   git push --set-upstream origin chore/roadmap-updates-YYYY-MM-DD
   ```

2) All-at-once passability migration (when approved)

   Purpose: add conservative default `passability` grids to all maps, include `.bak` backups, open `chore/add-passability` PR.

   Commands (run locally after approval):

   ```powershell
   dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps --apply
   git checkout -b chore/add-passability
   git add maps/*.json maps/*.json.bak
   git commit -m "chore(maps): add default passability grids (backups included)"
   git push --set-upstream origin chore/add-passability
   ```

   PR acceptance checklist (minimum):
   - CI builds and tests pass.
   - Validator dry-run shows no missing `passability` entries.
   - At least one reviewer validates map loading in the editor/preview for representative maps.

3) Camera quaternion change (small, low-risk experiment)

   Purpose: add or update a `camera` block with `rotationQuaternion` to one or more test maps so the editor can store simple 3D camera metadata for previews.

   Recommended branch and commands:

   ```powershell
   git checkout -b chore/add-camera-quaternions
   # edit maps/mymap.json (or maps/sample.json / maps/simple.json)
   git add maps/mymap.json maps/mymap.json.bak
   git commit -m "chore(maps): add camera quaternion metadata to mymap.json"
   git push --set-upstream origin chore/add-camera-quaternions
   ```

   Use-case: small PR for reviewers to confirm no runtime breakage — safe and easy to test in the preview host.

4) Incremental migration alternative

   Purpose: apply `--apply` only to maps touched in a given PR (good for frequent, small PRs). Implement by passing a file list to the tools or using the Python helpers.

   Example (PowerShell):

   ```powershell
   # Run validator for specific maps (dry-run)
   dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps --files mymap.json,sample.json
   # After review, apply to those same files
   dotnet run --project tools/MapPassabilityTool/MapPassabilityTool.csproj -- --maps-dir .\maps --files mymap.json,sample.json --apply
   ```

Recommended next step for maintainers:
- If you want the minimal-risk path, pick Option A (tools/tests/CI PR only) and merge it first. Then pick small, targeted map changes like the camera-quaternion experiment (item 3) to validate the workflow.
- If you want consistent repo state quickly, pick Option B (all-at-once) and use the exact commands above.
- If you want maximal safety, pick Option C (expand tests further) — add 2–4 additional roundtrip and malformed-JSON tests, run CI, then re-evaluate.

If you'd like, say which option you prefer (A/B/C), or tell me which map(s) to edit for the camera quaternion experiment and whether to create `.bak` backups and a branch; I'll perform the edits and push the branch for PR review.

## 9) Tracked tasks — progress snapshot

Below is a concise, linkable snapshot of the work items derived from the 90‑day plan. Each line points to the local issue stub (in `issues/`) where available, the suggested branch name, owner placeholder, ETA, and current status (as tracked in the repo todo list).

- `issues/0001-add-passability.md` — branch: `chore/add-passability` — owner: `@core-team` — ETA: 1–2 weeks — status: in-progress (decision pending)
- `issues/0002-stabilize-ci.md` — branch: `chore/stabilize-ci` — owner: `@devops` — ETA: 1 week — status: not-started
- `issues/0003-passability-tooling.md` — branch: `tools/passability-tooling` — owner: `@tools` — ETA: 1 week — status: not-started
- `issues/0004-expand-tests.md` — branch: `chore/expand-tests` — owner: `@core-team` — ETA: 2 weeks — status: completed (initial tests added)
- `issues/0005-avalonia-dialogs.md` — branch: `ui/port-avalonia-dialogs` — owner: `@ui-team` — ETA: 3–4 weeks — status: not-started
- `issues/0006-asset-exporter.md` — branch: `tools/asset-exporter-prototype` — owner: `@tools` — ETA: 3 weeks — status: not-started
- `issues/0007-add-passability.md` — branch: `chore/add-passability` — owner: `@core-team` — ETA: 1–2 weeks — status: not-started

Notes:
- Status values are derived from the repository todo list. If you want me to flip a status (for example to mark `0004` fully done), tell me and I'll update the todo list and this document.
- If you prefer a different owner or ETA for any item, I can patch the issue stub files in `issues/` to include the updated owner/ETA and a short checklist.

Next actions I can take right away (pick one):
- Run the passability validator in dry-run and paste the full report here (`run validator`).
- Apply camera quaternion metadata to specified map(s) and commit on `chore/add-camera-quaternions` (create `.bak` files) — reply `camera` and name map(s).
- Execute the all-at-once migration (Option B) and open `chore/add-passability` PR (needs explicit approval; reply `B`).

If you'd like me to open PRs for any of the branches above (once changes are made), I can prepare the commits and push them for review.

---

Decision (selected): Option A — tools/tests/CI-only PR (2025-10-25)

- Status: Selected and recorded in repository todo list.
- Why: minimal-risk approach that allows reviewers to approve tooling, tests, and CI behavior before any map JSON files are modified. Keeps the validator in dry-run so PRs touching `maps/` continue to be checked without changing files.
- Actions performed now:
   - Kept the draft branch for tooling/tests: `chore/roadmap-updates-2025-10-25`.
   - Left the CI validator workflow in dry-run mode (`.github/workflows/validate-maps.yml`).
   - Recorded the selection in the repository todo list so maintainers have an auditable decision.

- Next steps recommended after this selection:
   1. Merge the tooling/tests PR so reviewers can inspect the validator, tests, and CI changes.
   2. Optionally open small, focused experiments (for example `chore/add-camera-quaternions`) to validate editing workflow and review process.
   3. Once reviewers are satisfied, pick between Option B (all-at-once migration) or an incremental strategy for applying `passability` edits.

If you want a different decision, reply with A/B/C (or `camera <maps>` / `run validator`) and I'll update the docs and todo list accordingly.



