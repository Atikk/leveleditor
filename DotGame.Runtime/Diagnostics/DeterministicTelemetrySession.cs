using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotGame.Core.Diagnostics;
using DotGame.Core.Memory;
using DotGame.Core.Timing;
using DotGame.Core.Async.Jobs;

namespace DotGame.Runtime.Diagnostics;

public sealed class DeterministicTelemetrySession : IDisposable
{
    private readonly RuntimeTelemetryRecorder recorder = new();
    private readonly Dictionary<IMemoryAllocatorDiagnosticsSource, IDisposable> allocatorSubscriptions = new();
    private readonly Dictionary<string, IDisposable> jobSystemSubscriptions = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly string outputDirectory;
    private readonly string sessionName;
    private bool started;
    private bool disposed;

    public DeterministicTelemetrySession(string outputDirectory, string sessionName)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name must be provided.", nameof(sessionName));

        this.outputDirectory = outputDirectory;
        this.sessionName = sessionName;
    }

    public RuntimeTelemetryRecorder Recorder => recorder;

    public void Start()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (started)
                return;

            Directory.CreateDirectory(outputDirectory);

            foreach (var allocator in MemoryAllocatorDiagnosticsManager.GetRegisteredAllocatorsSnapshot())
                TrackAllocatorInternal(allocator);

            MemoryAllocatorDiagnosticsManager.AllocatorRegistered += OnAllocatorRegistered;
            MemoryAllocatorDiagnosticsManager.AllocatorUnregistered += OnAllocatorUnregistered;

            started = true;
        }
    }

    public void Attach(FrameLoopController controller)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));

        recorder.Attach(controller);
    }

    public TelemetryExportResult Export()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            Directory.CreateDirectory(outputDirectory);

            var export = recorder.CreateExportSnapshot();
            var timestamp = DateTimeOffset.UtcNow;
            var baseName = $"{sessionName}_{timestamp:yyyyMMdd_HHmmss}";

            var jsonPath = Path.Combine(outputDirectory, baseName + ".json");
            File.WriteAllText(jsonPath, export.ToJson());

            var csv = export.ToCsv();
            var framesCsvPath = Path.Combine(outputDirectory, baseName + "_frames.csv");
            File.WriteAllText(framesCsvPath, csv.Frames);

            var allocatorPaths = new List<string>();
            foreach (var pair in csv.Allocators)
            {
                var sanitized = SanitizeFileNameFragment(pair.Key, "allocator");
                var allocatorPath = Path.Combine(outputDirectory, $"{baseName}_alloc_{sanitized}.csv");
                File.WriteAllText(allocatorPath, pair.Value);
                allocatorPaths.Add(allocatorPath);
            }

            var jobSystemPaths = new List<string>();
            foreach (var pair in csv.JobSystems)
            {
                var sanitized = SanitizeFileNameFragment(pair.Key, "job");
                var jobPath = Path.Combine(outputDirectory, $"{baseName}_jobs_{sanitized}.csv");
                File.WriteAllText(jobPath, pair.Value);
                jobSystemPaths.Add(jobPath);
            }

            return new TelemetryExportResult(jsonPath, framesCsvPath, allocatorPaths, jobSystemPaths);
        }
    }

    public void Dispose()
    {
    MemoryAllocatorDiagnosticsManager.AllocatorRegistered -= OnAllocatorRegistered;
    MemoryAllocatorDiagnosticsManager.AllocatorUnregistered -= OnAllocatorUnregistered;

        List<IDisposable> subscriptions;
        List<IDisposable> jobSubscriptions;
        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            subscriptions = allocatorSubscriptions.Values.ToList();
            allocatorSubscriptions.Clear();
            jobSubscriptions = jobSystemSubscriptions.Values.ToList();
            jobSystemSubscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
            subscription.Dispose();

        foreach (var subscription in jobSubscriptions)
            subscription.Dispose();

        recorder.Dispose();
    }

    private void OnAllocatorRegistered(IMemoryAllocatorDiagnosticsSource allocator)
    {
        lock (gate)
        {
            if (disposed)
                return;

            TrackAllocatorInternal(allocator);
        }
    }

    private void OnAllocatorUnregistered(IMemoryAllocatorDiagnosticsSource allocator)
    {
        IDisposable? subscription = null;
        lock (gate)
        {
            if (allocatorSubscriptions.TryGetValue(allocator, out subscription))
            {
                allocatorSubscriptions.Remove(allocator);
            }
        }

        subscription?.Dispose();
    }

    private void TrackAllocatorInternal(IMemoryAllocatorDiagnosticsSource allocator)
    {
        if (allocatorSubscriptions.ContainsKey(allocator))
            return;

        var subscription = recorder.TrackAllocator(allocator);
        allocatorSubscriptions.Add(allocator, subscription);
    }

    public void TrackJobSystem(string name, IJobSystem jobSystem, int configuredWorkers)
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
            if (jobSystemSubscriptions.ContainsKey(name))
            {
                jobSystemSubscriptions[name].Dispose();
                jobSystemSubscriptions.Remove(name);
            }

            var subscription = recorder.TrackJobSystem(name, jobSystem, configuredWorkers);
            jobSystemSubscriptions[name] = subscription;
        }
    }

    private static string SanitizeFileNameFragment(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(DeterministicTelemetrySession));
    }

    public sealed record TelemetryExportResult(string JsonPath, string FramesCsvPath, IReadOnlyList<string> AllocatorCsvPaths, IReadOnlyList<string> JobSystemCsvPaths);
}
