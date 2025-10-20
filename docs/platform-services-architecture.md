# Platform Services Architecture Plan

This note captures the cross-platform plan for runtime services, build integration, and OS abstractions referenced in Part II of the engine expansion roadmap. It anchors near-term implementation work (Windows, Linux, macOS) and establishes the expectations for future platform bring-up.

## Goals

- Provide one coherent `IPlatformServices` surface that covers file I/O, threading, timing, window/event pumps, and environment utilities without leaking platform-specific details into gameplay or tooling code.
- Enable platform-neutral builds for the runtime, editor host, and headless harness, with platform-specific glue isolated to well-defined adapters.
- Support deterministic timing and telemetry instrumentation consistently across all targets.
- Simplify future platform onboarding (Steam Deck, consoles) by documenting required services, dependencies, and testing hooks.

## Current Inventory (Oct 2025)

- **Interfaces:** `DotGame.Core.Platform` exposes `IPlatformServices`, `IFileSystem`, `IThreadServices`, and `ITimeSource` contracts. `PlatformServices.Initialize(...)` stores the active implementation.
- **Windows implementation:** `DotGame.Runtime.Platform.WindowsPlatformServices` supplies file system, thread services (ThreadPool-backed background work), and high-resolution timing. This implementation powers both the Avalonia preview host and the standalone runtime.
- **Coverage gaps:**
  - No Linux or macOS implementation exists yet.
  - Window/event pump, input, high-resolution timers, and native library management are not abstracted.
  - Build scripts assume Windows (PowerShell, `.csproj` single target). Headless harness runs, but packaging/publishing for non-Windows targets is undefined.
  - Fragmentation telemetry that depends on OS memory queries is still TBD.

## Target Service Matrix

| Domain | Interface additions | Windows status | Linux (planned) | macOS (planned) |
| --- | --- | --- | --- | --- |
| File system | Extend to cover temp directories, path normalization, async streams | ✅ basic | Reuse `System.IO` with case-sensitive variants | Same as Linux |
| Threading | Add lightweight synchronization helpers (sleep/yield already exist) | ✅ basic | Map to `Thread`, `ThreadPool`, `SemaphoreSlim` | Same |
| Timing | Interface already provides `ITimeSource`; add calibration + sleep precision queries | ✅ basic | Use `Stopwatch` + `clock_gettime` when needed | Same |
| Windowing | New `IWindowServices` for event pump, cursor, DPI, swap chain hooks | ❌ | SDL/Avalonia host integration | SDL/Avalonia host |
| Diagnostics | Logging sink integration, OS version info, performance counters | Partial | Implement via `/proc`, `perf_event_open` | `sysctl`, Instruments hooks |
| Memory | Optional `IMemoryServices` for page size, commit/decommit, NUMA awareness | ❌ | `mmap`/`madvise` wrappers | `mach_vm` wrappers |
| Environment | Process info, clocks, locale, env vars | Partial | Provide wrappers | Provide wrappers |

The matrix guides interface expansion before we start non-Windows bring-up.

## Build System Alignment

1. **Project targets:** Update runtime and editor projects to multi-target `net8.0-windows`, `net8.0-linux`, `net8.0-macos` once the platform service implementations land.
2. **Packaging:** Introduce platform-specific publish profiles (e.g., `dotnet publish -r win-x64`, `linux-x64`, `osx-arm64`) and document required native dependencies (SDL2, angle, etc. if we adopt them).
3. **Scripts:** Guard PowerShell scripts with documented Bash equivalents or embed `pwsh` instructions for Linux/macOS runners. Ensure CI matrix (GitHub Actions) includes at least Windows + Linux sweeps when the runtime supports them.
4. **Asset paths:** Enforce use of `IFileSystem.GetAbsolutePath` and avoid manual path separators to ease case-sensitivity differences.

## Implementation Phases

1. **Interface expansion (Short-term):**
   - Create new interfaces: `IWindowServices`, `IMemoryServices`, `IDiagnosticServices` (name TBD).
   - Extend `IPlatformServices` to expose the new domains behind optional capability probes.
   - Document expectations for each interface in XML comments and update `docs/deterministic-telemetry-workflow.md` where platform nuances matter.
2. **Linux bring-up (Mid-term):**
   - Implement `LinuxPlatformServices` using .NET abstractions plus P/Invoke wrappers where necessary.
   - Validate headless harness + telemetry sweep on a GitHub Actions Ubuntu runner.
   - Update build pipeline to publish Linux binaries and capture telemetry artifacts.
3. **macOS bring-up (Mid-term):**
   - Implement `MacPlatformServices`.
   - Verify Avalonia host windowing and runtime entry run on macOS (local testing + CI once available).
4. **Advanced capabilities (Long-term):**
   - Add memory fragmentation queries and NUMA awareness for allocator work.
   - Provide high-resolution sleep/yield tuning hooks for deterministic loops.
   - Integrate window/input services with rendering abstraction once Part III begins.

## Testing & Validation

- **Unit coverage:** Add platform service unit tests that exercise file IO, threading helpers, and timing accuracy using .NET’s cross-platform test runners.
- **Integration:** Extend telemetry sweeps to run on each supported OS, verifying deterministic frame timing variance stays within tolerance.
- **Tooling:** Provide developer scripts to spin up headless runs on Linux/macOS (mirroring `tmp-run-headless.ps1` but with shell equivalents).

## Risks & Mitigations

- **Native dependency drift:** Document required native libs per platform; add CI checks that verify presence.
- **Determinism regressions:** Leverage telemetry gating to highlight timing drifts introduced by platform-specific differences.
- **Maintenance load:** Keep interface surface minimal and align with .NET cross-platform offerings before introducing custom native glue.

## Next Steps

1. Fold these interface plans into `DotGame.Core.Platform` (create stubs with TODOs and XML documentation).
2. Prototype Linux implementations for file + threading + time services; validate the headless harness on Ubuntu.
3. Prepare build pipeline PR introducing multi-targeting and publish profiles once the implementations stabilize.
4. Update the roadmap once Linux support is validated to reflect progress and re-evaluate macOS sequencing.
