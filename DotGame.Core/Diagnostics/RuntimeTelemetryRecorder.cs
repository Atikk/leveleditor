using System;
using System.Collections.Generic;
using DotGame.Core.Memory;
using DotGame.Core.Platform;
using DotGame.Core.Timing;
using DotGame.Core.Async.Jobs;

namespace DotGame.Core.Diagnostics;

public sealed class RuntimeTelemetryRecorder : IFrameBudgetListener, IDisposable
{
    private readonly object gate = new();
    private readonly List<FrameTimingSample> frameSamples = new();
    private readonly Dictionary<string, List<AllocatorTelemetrySample>> allocatorSamples = new(StringComparer.Ordinal);
    private readonly Dictionary<IMemoryAllocatorDiagnosticsSource, AllocatorSubscription> allocatorSubscriptions = new();
    private readonly Dictionary<string, List<JobSystemTelemetrySample>> jobSystemSamples = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IJobSystem> jobSystems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> jobSystemWorkerCapacities = new(StringComparer.Ordinal);
    private readonly HashSet<FrameLoopController> attachedLoops = new();
    private readonly Dictionary<string, string> metadata = new(StringComparer.Ordinal);
    private bool disposed;

    public void Attach(FrameLoopController loop)
    {
        if (loop == null)
            throw new ArgumentNullException(nameof(loop));

        lock (gate)
        {
            ThrowIfDisposed();
            if (attachedLoops.Add(loop))
                loop.RegisterListener(this);
        }
    }

    public void Detach(FrameLoopController loop)
    {
        if (loop == null)
            return;

        var shouldDetach = false;
        lock (gate)
        {
            if (attachedLoops.Remove(loop))
                shouldDetach = true;
        }

        if (shouldDetach)
            loop.UnregisterListener(this);
    }

    public IDisposable TrackAllocator(IMemoryAllocatorDiagnosticsSource allocator)
    {
        if (allocator == null)
            throw new ArgumentNullException(nameof(allocator));

        lock (gate)
        {
            ThrowIfDisposed();

            if (allocatorSubscriptions.TryGetValue(allocator, out var existing))
                return existing;

            var subscription = new AllocatorSubscription(this, allocator);
            allocatorSubscriptions.Add(allocator, subscription);
            allocator.MetricsUpdated += subscription.OnMetricsUpdated;
            return subscription;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            frameSamples.Clear();
            foreach (var list in allocatorSamples.Values)
                list.Clear();
        }
    }

    public RuntimeTelemetryExport CreateExportSnapshot()
    {
        lock (gate)
        {
            ThrowIfDisposed();

            var frameCopy = frameSamples.ToArray();
            var allocatorCopy = new Dictionary<string, IReadOnlyList<AllocatorTelemetrySample>>(StringComparer.Ordinal);
            foreach (var (key, value) in allocatorSamples)
                allocatorCopy[key] = value.ToArray();

            var jobCopy = new Dictionary<string, IReadOnlyList<JobSystemTelemetrySample>>(StringComparer.Ordinal);
            foreach (var (key, value) in jobSystemSamples)
                jobCopy[key] = value.ToArray();

            var platformSnapshot = CapturePlatformSnapshotUnsafe();
            var metadataCopy = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
            return new RuntimeTelemetryExport(frameCopy, allocatorCopy, jobCopy, platformSnapshot.Diagnostics, platformSnapshot.Memory, metadataCopy, DateTimeOffset.UtcNow);
        }
    }

    public void Dispose()
    {
        AllocatorSubscription[] subscriptions;
        FrameLoopController[] loops;

        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            subscriptions = allocatorSubscriptions.Values.ToArray();
            loops = attachedLoops.ToArray();
        }

        foreach (var subscription in subscriptions)
            subscription.Dispose();

        foreach (var loop in loops)
            loop.UnregisterListener(this);

        lock (gate)
        {
            allocatorSubscriptions.Clear();
            attachedLoops.Clear();
            frameSamples.Clear();
            allocatorSamples.Clear();
            jobSystemSamples.Clear();
            jobSystems.Clear();
            jobSystemWorkerCapacities.Clear();
        }
    }

    public void SetMetadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key must be provided.", nameof(key));

        lock (gate)
        {
            ThrowIfDisposed();
            metadata[key] = value ?? string.Empty;
        }
    }

    public void OnFrameStart(in FrameTimingInfo timing)
    {
    }

    public void OnBudgetExceeded(in FrameTimingInfo timing)
    {
        RecordFrame(timing, true);
    }

    public void OnFrameEnd(in FrameTimingInfo timing)
    {
        RecordFrame(timing, false);
    }

    private void RecordFrame(in FrameTimingInfo timing, bool isBudgetEvent)
    {
        lock (gate)
        {
            if (disposed)
                return;

            var timestamp = TimeSource.Current.GetCurrentTime();
            frameSamples.Add(new FrameTimingSample(
                timing.FrameIndex,
                timestamp,
                timing.TargetFrameTime.TotalMilliseconds,
                timing.ActualFrameTime.TotalMilliseconds,
                timing.AccumulatedDrift.TotalMilliseconds,
                timing.FixedStepCount,
                timing.BudgetExceeded,
                isBudgetEvent));

            foreach (var (name, jobSystem) in jobSystems)
            {
                try
                {
                    var stats = jobSystem.GetStatistics();
                    var configuredWorkers = jobSystemWorkerCapacities.TryGetValue(name, out var workers) ? workers : 0;
                    if (!jobSystemSamples.TryGetValue(name, out var samples))
                    {
                        samples = new List<JobSystemTelemetrySample>();
                        jobSystemSamples.Add(name, samples);
                    }

                    samples.Add(new JobSystemTelemetrySample(
                        name,
                        timestamp,
                        stats.PendingJobs,
                        stats.ActiveWorkers,
                        stats.CompletedJobs,
                        configuredWorkers));
                }
                catch (Exception)
                {
                    // Swallow to avoid destabilizing telemetry loop; diagnostics may log separately.
                }
            }
        }
    }

    private void RecordAllocatorSnapshot(in MemoryAllocatorMetricsSnapshot snapshot)
    {
        lock (gate)
        {
            if (disposed)
                return;

            if (!allocatorSamples.TryGetValue(snapshot.Name, out var samples))
            {
                samples = new List<AllocatorTelemetrySample>();
                allocatorSamples.Add(snapshot.Name, samples);
            }

            var timestamp = TimeSource.Current.GetCurrentTime();
            samples.Add(new AllocatorTelemetrySample(
                snapshot.Name,
                snapshot.Kind,
                timestamp,
                snapshot.CapacityBytes,
                snapshot.TotalAllocatedBytes,
                snapshot.CurrentUsageBytes,
                snapshot.PeakUsageBytes,
                snapshot.FreeBytes,
                snapshot.LargestFreeBlockBytes,
                snapshot.FragmentedBytes,
                snapshot.AllocationCount,
                snapshot.ResetCount,
                snapshot.OutstandingBlocks,
                snapshot.ReleasedBlocks,
                snapshot.FailedAllocations,
                snapshot.LastAllocationBytes,
                snapshot.LastAllocationTimestamp,
                snapshot.LastResetTimestamp,
                snapshot.UsageRatio,
                snapshot.FragmentationRatio));
        }
    }

    private void RemoveAllocator(IMemoryAllocatorDiagnosticsSource allocator, AllocatorSubscription subscription)
    {
        lock (gate)
        {
            if (allocatorSubscriptions.TryGetValue(allocator, out var existing) && ReferenceEquals(existing, subscription))
                allocatorSubscriptions.Remove(allocator);
        }
    }

    public IDisposable TrackJobSystem(string name, IJobSystem jobSystem, int configuredWorkers)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Job system name must be provided.", nameof(name));
        if (jobSystem == null)
            throw new ArgumentNullException(nameof(jobSystem));
        if (configuredWorkers < 0)
            throw new ArgumentOutOfRangeException(nameof(configuredWorkers));

        lock (gate)
        {
            ThrowIfDisposed();

            if (!jobSystemSamples.ContainsKey(name))
                jobSystemSamples[name] = new List<JobSystemTelemetrySample>();

            jobSystems[name] = jobSystem;
            jobSystemWorkerCapacities[name] = configuredWorkers;
            return new JobSystemSubscription(this, name);
        }
    }

    private void RemoveJobSystem(string name)
    {
        lock (gate)
        {
            jobSystems.Remove(name);
            jobSystemWorkerCapacities.Remove(name);
        }
    }

    private static PlatformSnapshot CapturePlatformSnapshotUnsafe()
    {
        PlatformDiagnosticSnapshot? diagnostics = null;
        MemoryStatistics? memory = null;

        if (!PlatformServices.IsInitialized)
            return new PlatformSnapshot(null, null);

        var services = PlatformServices.Current;

        try
        {
            diagnostics = services.Diagnostics.CaptureSnapshot();
        }
        catch
        {
            diagnostics = null;
        }

        try
        {
            memory = services.Memory.QueryProcessMemory();
        }
        catch
        {
            memory = null;
        }

        return new PlatformSnapshot(diagnostics, memory);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(RuntimeTelemetryRecorder));
    }

    public readonly record struct FrameTimingSample(
        int FrameIndex,
        DateTimeOffset Timestamp,
        double TargetFrameTimeMilliseconds,
        double ActualFrameTimeMilliseconds,
        double DriftMilliseconds,
        int FixedStepCount,
        bool BudgetExceeded,
        bool IsBudgetEvent);

    public readonly record struct AllocatorTelemetrySample(
        string Allocator,
    MemoryAllocatorKind Kind,
    DateTimeOffset Timestamp,
    long CapacityBytes,
    long TotalAllocatedBytes,
    long CurrentUsageBytes,
    long PeakUsageBytes,
    long FreeBytes,
    long LargestFreeBlockBytes,
    long FragmentedBytes,
    int AllocationCount,
    int ResetCount,
    int OutstandingBlocks,
    int ReleasedBlocks,
    int FailedAllocations,
    int LastAllocationBytes,
    DateTimeOffset LastAllocationTimestamp,
    DateTimeOffset LastResetTimestamp,
    double UsageRatio,
    double FragmentationRatio);

    public readonly record struct JobSystemTelemetrySample(
        string Name,
        DateTimeOffset Timestamp,
        int PendingJobs,
        int ActiveWorkers,
        int CompletedJobs,
        int ConfiguredWorkers);

    private sealed class AllocatorSubscription : IDisposable
    {
        private readonly RuntimeTelemetryRecorder owner;
        private readonly IMemoryAllocatorDiagnosticsSource allocator;
        private bool disposed;

        public AllocatorSubscription(RuntimeTelemetryRecorder owner, IMemoryAllocatorDiagnosticsSource allocator)
        {
            this.owner = owner;
            this.allocator = allocator;
        }

        public void OnMetricsUpdated(MemoryAllocatorMetricsSnapshot snapshot)
        {
            owner.RecordAllocatorSnapshot(snapshot);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            allocator.MetricsUpdated -= OnMetricsUpdated;
            owner.RemoveAllocator(allocator, this);
        }
    }

    private sealed class JobSystemSubscription : IDisposable
    {
        private readonly RuntimeTelemetryRecorder owner;
        private readonly string name;
        private bool disposed;

        public JobSystemSubscription(RuntimeTelemetryRecorder owner, string name)
        {
            this.owner = owner;
            this.name = name;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            owner.RemoveJobSystem(name);
        }
    }

    private readonly record struct PlatformSnapshot(PlatformDiagnosticSnapshot? Diagnostics, MemoryStatistics? Memory);
}
