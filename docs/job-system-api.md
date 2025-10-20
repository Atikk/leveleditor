# Job System API Surface

Date: 2025-10-19

## Overview

The runtime needs a deterministic, data-oriented job system that can service multiple scheduling domains (gameplay, streaming, rendering prep) while staying aligned with the timing/telemetry stack. This document captures the initial API surface that engine and tooling code will program against prior to landing the underlying scheduler implementation.

Goals:
- Provide a minimal but expressive contract for scheduling single jobs and batched iterations.
- Allow callers to express priorities, domain affinities, and inline-execution hints without tying them to a specific queue topology.
- Support dependency composition so subsystems can assemble job graphs without bespoke synchronization.
- Expose lightweight statistics and completion handles for diagnostics/QA instrumentation.

Non-goals for this stage:
- Shipping the final scheduler implementation (work-stealing vs. bifurcated is still under evaluation).
- Providing per-job custom allocators or task-local storage (can layer in later once baseline is proven).

## API Highlights

Namespace: `DotGame.Core.Async.Jobs`

```csharp
public delegate void JobExecuteDelegate(in JobExecutionContext context);

public interface IJobSystem : IDisposable
{
    JobHandle Schedule(JobExecuteDelegate execute, in JobScheduleOptions options, ReadOnlySpan<JobHandle> dependencies = default);
    JobHandle ScheduleBatch(in JobBatchDescriptor batch, ReadOnlySpan<JobHandle> dependencies = default);
    JobHandle CombineDependencies(ReadOnlySpan<JobHandle> handles);
    void Complete(JobHandle handle);
    void WaitAll(ReadOnlySpan<JobHandle> handles);
    JobStatistics GetStatistics();
}
```

Supporting types capture the scheduling metadata:

- `JobHandle`: opaque completion token with `IsValid` semantics and room for future generation counters.
- `JobScheduleOptions`: name, priority, affinity hints, optional inline execution, batch size override.
- `JobBatchDescriptor`: wraps `JobExecuteDelegate` plus iteration count so callers can fan out work as `for` loops.
- `JobExecutionContext`: per-invocation context (cancellation token, worker index, batch iteration metadata).
- `JobPriority` and `[Flags] JobAffinity`: let higher-level systems map to queue sets or worker pools once we settle on topology.
- `JobStatistics`: minimal diagnostics payload for logging/telemetry until full profiler integration arrives.

These shapes intentionally avoid referencing a concrete scheduler class—they enable the runtime and editor preview host to depend only on the interface while experimentation proceeds behind the scenes.

## Integration Plan

1. **Prototype Adapter Layer** — Wrap the existing `AsyncTaskScheduler` with an `IJobSystem` shim so gameplay/runtime code can begin targeting the new contract without waiting for the new scheduler. *(Completed via `AsyncTaskJobSystem`, providing immediate parity while spikes run.)*
2. **Spike Scheduler Backends** — Build two experimental backends behind the interface:
   - *Work-Stealing Pool*: per-worker deques with victim stealing, tuned for heterogeneous workloads.
   - *Bifurcated Queues*: dedicated queues per domain (IO, simulation, rendering) with central arbitration to test predictability and cache locality.
   Instrument both via the telemetry pipeline (frame + allocator exporters) and compare contention/drift across representative scenes.
        - `Experimental.WorkStealingJobSystem` wraps a queue-per-worker model for the work-stealing trial.
        - `Experimental.BifurcatedJobSystem` provides domain-specific queues (main/background/render) for predictability experiments.
3. **Unify Editor + Runtime Usage** — Update Avalonia preview, standalone runtime, and upcoming CI harnesses to accept an `IJobSystem` factory so deterministic QA runs exercise the same scheduling path. *(Standalone runtime and the Avalonia preview now consume `RuntimeJobSystemFactory`, honoring `DOTGAME_RUNTIME_JOB_SYSTEM` (`async`, `workstealing`, `bifurcated`) and `DOTGAME_RUNTIME_JOB_WORKERS` to configure thread counts for telemetry runs.)*
4. **Finalize Diagnostics Hooks** — Extend `JobStatistics` with optional queue depth histograms once the winning backend is chosen, and wire results into the new telemetry export format for automated regression tracking.

### Runtime Selection & Telemetry

- CI/QA scripts can set `DOTGAME_RUNTIME_JOB_SYSTEM` to `async`, `workstealing`, or `bifurcated` before launching the deterministic runtime harness. Unknown identifiers fall back to `async` and emit a warning.
- `DOTGAME_RUNTIME_JOB_WORKERS` caps worker threads (default `2`, max `64`), allowing scale comparisons without code edits.
- Telemetry exports now include per-sample `configuredWorkers` alongside `pendingJobs`, `activeWorkers`, and `completedJobs`, enabling dashboards to normalize utilization against the configured pool size.
- The helper script `scripts\collect-job-system-telemetry.ps1` sweeps the supported schedulers, invoking the deterministic runtime with the appropriate environment variables and depositing exports under a timestamped directory for analysis.
- The helper script `scripts\collect-job-system-telemetry.ps1` sweeps the supported schedulers, invoking the deterministic runtime with the appropriate environment variables and depositing exports under a timestamped directory for analysis.
- After runs complete, `scripts\analyze-job-system-telemetry.py <telemetry-root>` prints pending/active/worker utilisation summaries (optionally `--json` to emit machine-readable output) so scheduler comparisons can feed dashboards.

## Next Steps

- Draft spike tasks referencing the two scheduler prototypes and capture success metrics (latency, throughput, determinism adherence).
- Author RFC for integrating the job system factory into platform services initialization (ensuring editor/runtime parity).
- Extend roadmap Part II entry to reflect completion of the API surface milestone and to track the upcoming scheduler spike deliverables.
