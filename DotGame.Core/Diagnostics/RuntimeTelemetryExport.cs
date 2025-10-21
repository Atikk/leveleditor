using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using DotGame.Core.Platform;

namespace DotGame.Core.Diagnostics;

public sealed class RuntimeTelemetryExport
{
    public RuntimeTelemetryExport(
        IReadOnlyList<RuntimeTelemetryRecorder.FrameTimingSample> frames,
        IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.AllocatorTelemetrySample>> allocators,
        IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.JobSystemTelemetrySample>> jobSystems,
        PlatformDiagnosticSnapshot? platform = null,
        MemoryStatistics? memory = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        DateTimeOffset? exportedAt = null)
    {
        Frames = frames ?? throw new ArgumentNullException(nameof(frames));
        Allocators = allocators ?? throw new ArgumentNullException(nameof(allocators));
        JobSystems = jobSystems ?? throw new ArgumentNullException(nameof(jobSystems));
        Platform = platform;
        Memory = memory;
        Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        ExportedAt = exportedAt ?? DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<RuntimeTelemetryRecorder.FrameTimingSample> Frames { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.AllocatorTelemetrySample>> Allocators { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.JobSystemTelemetrySample>> JobSystems { get; }

    public PlatformDiagnosticSnapshot? Platform { get; }

    public MemoryStatistics? Memory { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public DateTimeOffset ExportedAt { get; }

    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented
        };

        var payload = new TelemetryPayload
        {
            ExportedAt = ExportedAt,
            Frames = Frames,
            Allocators = Allocators,
            JobSystems = JobSystems,
            Platform = Platform,
            Memory = Memory,
            Metadata = Metadata
        };

        return JsonSerializer.Serialize(payload, options);
    }

    public static RuntimeTelemetryExport FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content must be provided.", nameof(json));

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var payload = JsonSerializer.Deserialize<TelemetryPayload>(json, options)
                      ?? throw new InvalidOperationException("Failed to deserialize telemetry payload.");

        var frames = payload.Frames ?? Array.Empty<RuntimeTelemetryRecorder.FrameTimingSample>();
        var allocators = payload.Allocators ?? new Dictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.AllocatorTelemetrySample>>(StringComparer.Ordinal);
        var jobSystems = payload.JobSystems ?? new Dictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.JobSystemTelemetrySample>>(StringComparer.Ordinal);
        var metadata = payload.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);

        return new RuntimeTelemetryExport(frames, allocators, jobSystems, payload.Platform, payload.Memory, metadata, payload.ExportedAt);
    }

    public static RuntimeTelemetryExport FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path must be provided.", nameof(path));

        var json = System.IO.File.ReadAllText(path);
        return FromJson(json);
    }

    public TelemetryCsvExport ToCsv()
    {
        var invariant = CultureInfo.InvariantCulture;
        var framesBuilder = new StringBuilder();
        framesBuilder.AppendLine("frameIndex,timestamp,targetMs,actualMs,driftMs,fixedStepCount,budgetExceeded,isBudgetEvent");

        foreach (var frame in Frames)
        {
            framesBuilder.Append(frame.FrameIndex.ToString(invariant)).Append(',');
            AppendCsvField(framesBuilder, frame.Timestamp.ToString("o", invariant));
            framesBuilder.Append(',');
            framesBuilder.Append(frame.TargetFrameTimeMilliseconds.ToString("F4", invariant)).Append(',');
            framesBuilder.Append(frame.ActualFrameTimeMilliseconds.ToString("F4", invariant)).Append(',');
            framesBuilder.Append(frame.DriftMilliseconds.ToString("F4", invariant)).Append(',');
            framesBuilder.Append(frame.FixedStepCount.ToString(invariant)).Append(',');
            framesBuilder.Append(frame.BudgetExceeded ? "true" : "false").Append(',');
            framesBuilder.Append(frame.IsBudgetEvent ? "true" : "false");
            framesBuilder.AppendLine();
        }

        var allocatorExports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in Allocators)
        {
            var builder = new StringBuilder();
            builder.AppendLine("allocator,kind,timestamp,capacityBytes,totalAllocatedBytes,currentUsageBytes,peakUsageBytes,freeBytes,largestFreeBlockBytes,fragmentedBytes,allocationCount,resetCount,outstandingBlocks,releasedBlocks,failedAllocations,lastAllocationBytes,lastAllocationTimestamp,lastResetTimestamp,usageRatio,fragmentationRatio");

            foreach (var sample in pair.Value)
            {
                AppendCsvField(builder, sample.Allocator);
                builder.Append(',');
                AppendCsvField(builder, sample.Kind.ToString());
                builder.Append(',');
                AppendCsvField(builder, sample.Timestamp.ToString("o", invariant));
                builder.Append(',');
                builder.Append(sample.CapacityBytes.ToString(invariant)).Append(',');
                builder.Append(sample.TotalAllocatedBytes.ToString(invariant)).Append(',');
                builder.Append(sample.CurrentUsageBytes.ToString(invariant)).Append(',');
                builder.Append(sample.PeakUsageBytes.ToString(invariant)).Append(',');
                builder.Append(sample.FreeBytes.ToString(invariant)).Append(',');
                builder.Append(sample.LargestFreeBlockBytes.ToString(invariant)).Append(',');
                builder.Append(sample.FragmentedBytes.ToString(invariant)).Append(',');
                builder.Append(sample.AllocationCount.ToString(invariant)).Append(',');
                builder.Append(sample.ResetCount.ToString(invariant)).Append(',');
                builder.Append(sample.OutstandingBlocks.ToString(invariant)).Append(',');
                builder.Append(sample.ReleasedBlocks.ToString(invariant)).Append(',');
                builder.Append(sample.FailedAllocations.ToString(invariant)).Append(',');
                builder.Append(sample.LastAllocationBytes.ToString(invariant)).Append(',');
                AppendCsvField(builder, sample.LastAllocationTimestamp.ToString("o", invariant));
                builder.Append(',');
                AppendCsvField(builder, sample.LastResetTimestamp.ToString("o", invariant));
                builder.Append(',');
                builder.Append(sample.UsageRatio.ToString("F6", invariant));
                builder.Append(',');
                builder.Append(sample.FragmentationRatio.ToString("F6", invariant));
                builder.AppendLine();
            }

            allocatorExports[pair.Key] = builder.ToString();
        }

        var jobSystemExports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in JobSystems)
        {
            var builder = new StringBuilder();
            builder.AppendLine("jobSystem,timestamp,pendingJobs,activeWorkers,completedJobs,configuredWorkers");

            foreach (var sample in pair.Value)
            {
                AppendCsvField(builder, sample.Name);
                builder.Append(',');
                AppendCsvField(builder, sample.Timestamp.ToString("o", invariant));
                builder.Append(',');
                builder.Append(sample.PendingJobs.ToString(invariant)).Append(',');
                builder.Append(sample.ActiveWorkers.ToString(invariant)).Append(',');
                builder.Append(sample.CompletedJobs.ToString(invariant)).Append(',');
                builder.Append(sample.ConfiguredWorkers.ToString(invariant));
                builder.AppendLine();
            }

            jobSystemExports[pair.Key] = builder.ToString();
        }

        return new TelemetryCsvExport(framesBuilder.ToString(), allocatorExports, jobSystemExports);
    }

    private static void AppendCsvField(StringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            builder.Append(value);
            return;
        }

        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            builder.Append('"');
            builder.Append(value.Replace("\"", "\"\""));
            builder.Append('"');
        }
        else
        {
            builder.Append(value);
        }
    }

    private sealed class TelemetryPayload
    {
        public DateTimeOffset ExportedAt { get; set; }

        public IReadOnlyList<RuntimeTelemetryRecorder.FrameTimingSample>? Frames { get; set; }
            = Array.Empty<RuntimeTelemetryRecorder.FrameTimingSample>();

        public IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.AllocatorTelemetrySample>>? Allocators { get; set; }
            = new Dictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.AllocatorTelemetrySample>>();

        public IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.JobSystemTelemetrySample>>? JobSystems { get; set; }
            = new Dictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.JobSystemTelemetrySample>>();

        public PlatformDiagnosticSnapshot? Platform { get; set; }

        public MemoryStatistics? Memory { get; set; }

        public IReadOnlyDictionary<string, string>? Metadata { get; set; }
            = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}

public sealed class TelemetryCsvExport
{
    public TelemetryCsvExport(string frames, IReadOnlyDictionary<string, string> allocators, IReadOnlyDictionary<string, string> jobSystems)
    {
        Frames = frames ?? throw new ArgumentNullException(nameof(frames));
        Allocators = allocators ?? throw new ArgumentNullException(nameof(allocators));
        JobSystems = jobSystems ?? throw new ArgumentNullException(nameof(jobSystems));
    }

    public string Frames { get; }

    public IReadOnlyDictionary<string, string> Allocators { get; }

    public IReadOnlyDictionary<string, string> JobSystems { get; }
}
