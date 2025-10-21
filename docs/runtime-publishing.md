# Runtime Publishing Guide

This guide captures the current publishing setup for the deterministic runtime harness. Use these steps to produce binaries for validation sweeps or to hand off builds across platforms while the editor tooling adopts the new platform abstraction.

## Available Publish Profiles

Publish profiles live under `DotGame.Runtime/Properties/PublishProfiles`:

- `Runtime-win-x64.pubxml`
- `Runtime-linux-x64.pubxml`
- `Runtime-osx-arm64.pubxml`

Each profile targets the shared `net8.0` framework, disables self-contained packaging (expect dotnet runtime on the host), and writes output to `DotGame.Runtime/publish/<rid>/`.

## Command Examples

```powershell
# Windows x64 release publish
cd c:\path\to\leveleditor
 dotnet publish DotGame.Runtime\DotGame.Runtime.csproj `
    /p:PublishProfile=Runtime-win-x64 `
    /p:PlatformServices=windows

# Linux build (cross publish from Windows hosts)
dotnet publish DotGame.Runtime\DotGame.Runtime.csproj `
    /p:PublishProfile=Runtime-linux-x64 `
    /p:PlatformServices=linux

# macOS arm64 build
dotnet publish DotGame.Runtime\DotGame.Runtime.csproj `
    /p:PublishProfile=Runtime-osx-arm64 `
    /p:PlatformServices=mac
```

> The `PlatformServices` property is forwarded to MSBuild as an environment variable so the runtime resolves the correct platform factory when launched.

## Integration Points

1. **Telemetry Sweeps:** update automation to invoke `dotnet publish` for the relevant profile before executing the headless runs. Pair the publish step with `DOTGAME_PLATFORM_IMPLEMENTATION` to guarantee the correct platform services implementation.
2. **CI Matrix:** extend GitHub Actions to publish all three profiles and archive artifacts per platform once Linux/macOS harness validation is available.
3. **Future Work:** toggle `SelfContained`/`PublishSingleFile` to `true` when distributing to machines without a pre-installed .NET runtime; revisit trimming settings after validating MonoGame DesktopGL compatibility.
