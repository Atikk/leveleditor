# Contributing Guide

This short guide explains how to contribute small changes to the DotGame repo, how DI and adapters are wired, and how to run tests locally.

If you're adding a new core-facing service contract, follow the Adapter pattern: define the contract in `DotGame.Core`, then implement UI-side adapters in the Avalonia project and register them in `Program.cs`.

1) Adding a core contract

- Create an interface under `DotGame.Core.Services`, e.g. `IMyService`.
- Keep the contract surface small and avoid exposing UI-specific types (no Avalonia, WinForms, or platform types in the signature).
- Add unit tests in `DotGame.Core.Tests` to validate behavior and edge-cases.

2) Implementing a UI adapter

- Implement the core contract interface in the Avalonia project under `DotGame/src/Services/Adapters`.
- Keep the adapter thin: it should translate core-friendly calls into UI operations and handle UI thread marshalling where necessary.
- Prefer small, testable adapters: the `MonoGamePreviewAdapter` is an example of a headless-friendly adapter used by unit tests.

Example adapter registration (in `DotGame/src/App/Program.cs`):

```csharp
// Register default tile service and adapters at application startup
var defaultTileService = new Dotgame.Avalonia.Services.TileService();
DotGame.Core.Platform.ServiceContainer.RegisterSingleton<Dotgame.Avalonia.Services.ITileService>(defaultTileService);
DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.ITileService>(new Dotgame.Avalonia.Services.Adapters.TileServiceAdapter(defaultTileService));
DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.IPreviewService>(new Dotgame.Avalonia.Services.Adapters.MonoGamePreviewAdapter());
```

3) EditorWindow-backed adapters

- If the adapter needs to interact with `EditorWindow` (live UI), create an adapter that captures a reference to the window and marshals calls to `Dispatcher.UIThread` as in `EditorWindowPreviewService`.
- Tests should prefer the lightweight headless adapter whenever possible.

4) Testing locally

- Build solution:

```powershell
dotnet build leveleditor.sln --configuration Release
```

- Run all tests:

```powershell
dotnet test leveleditor.sln --configuration Release --no-build
```

- The `DotGame.Tests` project contains headless-friendly tests and an Avalonia Skia initializer (`TestAvaloniaSetup`) used to allow constructing `Bitmap` from streams during tests.

5) CI and telemetry

- The repository includes a CI workflow that runs solution builds and a deterministic telemetry sweep. The telemetry analyzer script (`scripts/analyze-job-system-telemetry.py`) aggregates job-system CSV exports and compares them to a baseline when present.

6) Style and PR guidance

- Keep changes small and focused. Add tests for behavior changes.
- Update `docs/ROADMAP.md` when completing roadmap-level items and keep the repository todo list in sync.

If you need help with writing adapters or tests, open an issue or draft a PR and request review — happy to assist with design and implementation details.
