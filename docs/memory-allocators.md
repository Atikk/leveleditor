# Memory Allocators Overview

The runtime now ships with a bundled trio of custom allocators that are tracked through the telemetry pipeline:

- **Arena allocator** – single-shot bump allocator for transient frame data with optional warning thresholding.
- **Stack allocator** – LIFO allocator with tail collapse for stack-like workloads that need deterministic reuse.
- **Pool allocator** – fixed-size block allocator intended for frequently reused objects or buffers.

All three allocators emit telemetry via `IMemoryAllocatorDiagnosticsSource`, surface fragmentation metrics through the recorder/export pipeline, and appear automatically in deterministic telemetry exports once instantiated.

## Default Configuration

Allocators are instantiated through `MemoryAllocatorConfiguration` and supplied to the runtime via `MemoryAllocatorSet`. Default capacities are tuned for development sweeps:

| Allocator | Name  | Default Capacity | Notes |
|-----------|-------|------------------|-------|
| Arena     | `arena` | 32 MiB           | Emits usage warnings when utilization exceeds 90%. |
| Stack     | `stack` | 2 MiB            | Supports alignment guarantees (default 16 bytes). |
| Pool      | `pool`  | 512 blocks × 4096 bytes (≈2 MiB) | Automatically replenishes and tracks outstanding blocks. |

The runtime (`Program.cs`) loads configuration from the environment, creates the allocator set, logs a summary, and passes the set into `Game1` so gameplay systems can consume the allocators through `RuntimeContext.Allocators`.

```csharp
var allocatorConfiguration = MemoryAllocatorConfiguration.FromEnvironment();
using var allocatorSet = allocatorConfiguration.CreateAllocators();

var game = new Game1(jobSystem, jobWorkers, allocatorSet);
```

When `Game1` is created without an explicit set (e.g., tests), it falls back to the default configuration.

## Current Usage

- The deterministic headless harness allocates per-frame job handle buffers from the shared stack allocator, eliminating the previous array churn during sweeps.
- Configuration parsing is covered by `DotGame.Core.Tests` so environment-driven overrides stay within supported bounds.
- Gameplay state collision geometry now lives in an arena-backed buffer, trimming per-load allocations and surfacing map collider consumption through telemetry.

## Environment Variables

You can tailor allocator behaviour without code changes via environment variables. The parser clamps values to safe ranges and falls back to defaults if parsing fails.

| Variable | Description | Default |
|----------|-------------|---------|
| `DOTGAME_ALLOCATOR_AUTO_DIAGNOSTICS` | Toggle automatic registration with `MemoryAllocatorDiagnosticsManager`. Any truthy value enables the auto-registration; falsy clears it. | `true` |
| `DOTGAME_ALLOCATOR_ARENA_NAME` | Name used in telemetry/logging for the arena allocator. | `arena` |
| `DOTGAME_ALLOCATOR_ARENA_CAPACITY_MB` | Arena capacity in mebibytes (MiB). Clamped between 1 and 4096. | `32` |
| `DOTGAME_ALLOCATOR_ARENA_WARN_THRESHOLD` | Usage ratio (0-1) that resets the arena warning level. | `0.90` |
| `DOTGAME_ALLOCATOR_STACK_NAME` | Stack allocator name. | `stack` |
| `DOTGAME_ALLOCATOR_STACK_CAPACITY_KB` | Stack capacity in kibibytes (KiB). Clamped between 64 and 524,288. | `2048` |
| `DOTGAME_ALLOCATOR_POOL_NAME` | Pool allocator name. | `pool` |
| `DOTGAME_ALLOCATOR_POOL_BLOCK_SIZE` | Size (bytes) per pool block. Clamped between 16 and 1,048,576. | `4096` |
| `DOTGAME_ALLOCATOR_POOL_BLOCK_COUNT` | Number of blocks in the pool. Clamped between 1 and 65,536. | `512` |

### Fragmentation + Telemetry Columns

Allocator metrics export the following columns (CSV) through `RuntimeTelemetryExport`:

- `largestFreeBlockBytes` – helps estimate compaction pressure.
- `fragmentedBytes` / `fragmentationRatio` – difference between available space and largest free block.
- `freeBytes` / `freeRatio` – total remaining capacity for the allocator.

This data now appears alongside existing usage counters for every allocator captured during a deterministic telemetry session.

## Consuming Allocators In-Game

`RuntimeContext` exposes the active `MemoryAllocatorSet` so subsystems can adopt the custom allocators in a structured way:

```csharp
void ExampleUsage(RuntimeContext context)
{
    using var block = context.Allocators.Arena.Allocate(1024);
    Span<byte> scratch = block.Memory.Span;
    // ... populate scratch buffer ...
}
```

Each allocator automatically emits updated metrics (and logs anomalies) when allocations succeed, fail, or when buffers are reset/disposed. When telemetry sessions run, the recorder tracks allocators as soon as they register with `MemoryAllocatorDiagnosticsManager` and includes snapshots in JSON/CSV exports.

## Follow-Up Checklist

- Integrate allocators with runtime subsystems (rendering, resource loading) so telemetry reflects real workloads.
- Document allocator usage patterns (frame scratch vs. persistent pools) alongside the data-oriented blueprint.
