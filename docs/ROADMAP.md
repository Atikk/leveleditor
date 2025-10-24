# DotGame Roadmap

This document mirrors the project's roadmap and TODOs. Keep it updated when you make changes in the codebase or the todo list.

## Overview
Primary goals:
- Port full game logic (editor + player) from original Windows Forms to Avalonia.
- Improve engine features (audio, collision, AI) after port stabilization.
- Improve editor UX (undo/redo, layers, tile metadata) and asset pipeline.

---

## Current status (live-tracked via internal TODO list)
- Roadmap maintenance: In-progress — this document should be kept synchronized with the repository todo list.
- Tile management refactor: Completed — tile logic moved into `DotGame/src/Services/TileService.cs` and `DotGame/src/Views/TileTypes.cs`.
- Windows Forms inventory: Completed — WinForms files exist under `src/DotGameCSharp/` and have been cataloged.

---

## Work plan (priority-ordered tasks)
1. Inventory Windows Forms files (completed)
   - Files: `src/DotGameCSharp/Program.cs`, `MainMenuForm.cs`, `EditorForm.cs`, `GameForm.cs`, `Map.cs`, `Character.cs`.

2. Extract & port domain logic (Map, Character) — not started
   - Move logic into `DotGame` or `DotGame.Core`.
   - Replace System.Drawing with Skia/Avalonia friendly types.
   - Add unit tests for serialization and behavior.

3. Wire runtime preview (EditorGame) — not started
   - Host MonoGame or equivalent renderer inside the Avalonia editor preview.
   - Forward input and lifecycle events to the runtime preview.

4. Port GameForm rendering loop — not started
   - Convert WinForms OnPaint/timer loop into Avalonia rendering (Skia or MonoGame host).

5. Port UI windows to Avalonia — not started
   - Recreate MainMenu, CharacterCreation, MapSelector as Avalonia windows if needed.

6. Replace platform services & dialogs — not started
   - Abstract file dialogs and other platform services; provide Avalonia implementations.

7. DI for TileService and other services — not started
   - Replace `new` usage with DI and register services in App startup.

8. Add unit tests (Map, TileService) — not started
   - Ensure core behaviors are covered and passing.

9. Build & CI checks — not started
   - Ensure `dotnet build` passes for Avalonia projects; consider adding CI.

10. Improve asset pipeline — not started
   - Add versioning, layout, and migration tools.

11. Add engine features (audio, collision, AI) — not started
   - Implement as separate small tasks after port is stable.

12. Improve UX (undo/redo, layers) — not started
   - Implement undo/redo and layered editing features.

13. Documentation & tutorials — not started
   - Update README and write contributor guides and tutorials.

---

## How to use this roadmap
- The authoritative source of truth is the repository's internal todo list; update that list and then refresh this `docs/ROADMAP.md` file to reflect changes.
- Prefer small, testable PRs. Each major item above can be broken into smaller subtasks.

---

## Next recommended immediate steps
- Run a build to get a baseline and catch compile errors.
- Start porting `Map.cs` and `Character.cs` to `DotGame.Core` and add unit tests.
- Replace direct `TileService` instantiation with DI to improve testability.

---

*Generated and synchronized with repository todo list.*
