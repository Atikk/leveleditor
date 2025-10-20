# Job System Synchronization Primitives

The Part II runtime foundation introduces a growing set of synchronization tools that layer on top of `DotGame.Core.Async.Jobs`. This document summarizes the available primitives and how to consume them from gameplay/runtime code.

## JobFence

`JobFence` is a lightweight completion primitive that tracks one or more scheduled jobs and resolves when every producer finishes. It complements `IJobSystem.WaitAll` by providing a reusable handle that callers can share across scheduling sites.

### Creating and Waiting on a Fence

```csharp
var fence = new JobFence();
jobSystem.ScheduleBatch(descriptor, fence: fence);
await fence.WaitAsync(cancellationToken);
```

- `JobFence.RegisterProducer()` is invoked automatically by the job system when a fence is supplied during `Schedule`, `ScheduleBatch`, or `CombineDependencies`.
- Completion propagates success, cancellation, or the first failure back to waiters.
- Fences are single-use; create a new instance for each synchronization window.

### Error Propagation

If any job associated with the fence faults, the fence captures the first exception and rethrows it when `Wait` / `WaitAsync` completes. Cancellations surface as `OperationCanceledException`.

## JobBarrier

`JobBarrier` coordinates phased execution across a fixed set of participants. Each caller signals its arrival and waits for the remaining participants before the barrier advances to the next phase.

```csharp
var barrier = new JobBarrier(participantCount: 4);

await Parallel.ForEachAsync(jobs, async (job, token) =>
{
    jobSystem.Schedule(job.Work, job.Options, fence: job.Fence);
    await job.Barrier.SignalAndWaitAsync(token);
    // All participants reach this line together before starting the next stage.
});
```

- Participant counts can grow or shrink between phases via `AddParticipants` / `RemoveParticipants`.
- `Phase` increments every time the final participant arrives, allowing callers to detect progress.
- `Reset` cancels the current waiting phase and reinitializes the barrier with a new participant count.

## JobSemaphore

`JobSemaphore` caps the amount of work that may execute concurrently by issuing permits to job producers. The job system acquires a permit just before queueing work and returns it when execution finishes (or if the job faults/cancels), which makes it ideal for throttling background streaming or synthetic stress workloads.

```csharp
var throttle = new JobSemaphore(initialCount: 8);

var options = new JobScheduleOptions(
    name: "BuildNavigation",
    priority: JobPriority.Normal,
    affinity: JobAffinity.Background,
    allowInlineExecution: false,
    batchSize: 4,
    concurrencyLimiter: throttle);

var descriptor = new JobBatchDescriptor(
    execute: context => BakeTile(context.IterationIndex),
    iterationCount: 64,
    options: options);

jobSystem.ScheduleBatch(descriptor, fence: fence);
```

- Permits are only consumed once dependencies resolve, so upstream fences/barriers do not hold slots.
- Cancellation tokens propagate through the semaphore, ensuring shutdown does not deadlock on an exhausted permit pool.
- Headless sweeps (`DotGame.Runtime.Diagnostics.HeadlessRuntimeHarness`) use the shared semaphore when `DOTGAME_RUNTIME_HEADLESS_CONCURRENCY` is set, keeping frame workloads predictable without bespoke callbacks.

## Scheduling Domains & Priorities (Experimental)

The experimental job systems (`WorkStealingJobSystem`, `BifurcatedJobSystem`) expose additional scheduling knobs:

- **Affinity** — `JobAffinity` allows callers to target background, render, IO, or main-thread queues.
- **Priority** — `JobPriority` influences queue ordering within a domain.
- **Batch Size** — Each job can specify `batchSize` to chunk iterative workloads.

Use the batch descriptor helpers to define these options succinctly:

```csharp
var descriptor = new JobBatchDescriptor(
    execute: context => { /* work */ },
    iterationCount: 64,
    options: new JobScheduleOptions(
        name: "BakeLightmaps",
        priority: JobPriority.High,
        affinity: JobAffinity.Background,
        allowInlineExecution: false,
        batchSize: 4));
```

Pair descriptors with fences when synchronizing large staging steps (asset streaming, render graph compilation, etc.).

## Runtime Usage

- The deterministic headless harness (`DotGame.Runtime.Diagnostics.HeadlessRuntimeHarness`) now uses a `JobFence` to synchronize per-frame batches, reducing GC pressure compared to array-based waits.
- `JobBarrier` is available for upcoming multi-stage workloads (render graph compilation, asset staging) that need deterministic hand-offs between job phases.
- `JobSemaphore` backs concurrency budgets so producers can throttle in-flight batches without wiring per-job continuations.

For additional examples, inspect `DotGame.Runtime.Diagnostics.HeadlessRuntimeHarness` and the job system implementations in `DotGame.Core.Async.Jobs`. Future milestones will extend semaphore usage across asset streaming and render graph compilation paths.
