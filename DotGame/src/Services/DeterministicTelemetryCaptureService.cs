using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotGame.Core.Diagnostics;
using DotGame.Core.Logging;
using DotGame.Core.Memory;
using DotGame.Core.Platform;
using DotGame.Core.Timing;
using DotGame.Runtime.Diagnostics;
using DotGame.Runtime.Platform;
using DotGame.Runtime.Services;

namespace Dotgame.Avalonia.Services;

public sealed class DeterministicTelemetryCaptureService
{
    private readonly ILogger logger = LogManager.GetLogger<DeterministicTelemetryCaptureService>();
    private readonly string outputRoot;

    public DeterministicTelemetryCaptureService(string? outputRoot = null)
    {
        this.outputRoot = string.IsNullOrWhiteSpace(outputRoot)
            ? Path.Combine(AppContext.BaseDirectory ?? Environment.CurrentDirectory, "telemetry-editor")
            : outputRoot;
    }

    public string OutputRoot => outputRoot;

    public Task<TelemetryCaptureResult> CaptureAsync(TelemetryCaptureRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        return Task.Run(() => CaptureInternal(request, cancellationToken), cancellationToken);
    }

    private TelemetryCaptureResult CaptureInternal(TelemetryCaptureRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var headlessOptions = request.HeadlessOptions ?? HeadlessRuntimeOptions.CreateDefault();
        var platformActivation = EnsurePlatformServicesInitialized(request.PlatformIdentifier);
        TimeSource.Initialize(PlatformServices.Current.TimeSource);

        var allocatorConfiguration = request.AllocatorConfiguration ?? MemoryAllocatorConfiguration.FromEnvironment();
        using var allocatorSet = allocatorConfiguration.CreateAllocators();

        var workerCount = request.WorkerCountOverride ?? Math.Clamp(Environment.ProcessorCount, 1, 64);
        var jobActivation = RuntimeJobSystemFactory.Create(request.JobSystemIdentifier, workerCount);
        using var jobSystem = jobActivation.JobSystem;

        var sessionDirectory = ResolveSessionDirectory(request);
        Directory.CreateDirectory(sessionDirectory);
        var sessionName = request.BuildSessionName();

        DeterministicTelemetrySession.TelemetryExportResult exportPaths;
        RuntimeTelemetryExport snapshot;
        FrameTimingInfo? lastTiming = null;

        using var session = new DeterministicTelemetrySession(sessionDirectory, sessionName);
        session.Start();

        var controller = new FrameLoopController(TimeSource.Current, headlessOptions.TargetFrameRate);
        session.Attach(controller);
        session.TrackJobSystem(jobActivation.Resolved, jobSystem, jobActivation.WorkerCount);

        SetMetadata(session, request, jobActivation, platformActivation, headlessOptions, sessionDirectory, sessionName);

        using (var harness = new HeadlessRuntimeHarness(jobSystem, headlessOptions, allocatorSet))
        {
            controller.Run(
                _ =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return harness.StepFrame();
                },
                timing => lastTiming = timing,
                cancellationToken);
        }

        snapshot = session.Recorder.CreateExportSnapshot();
        exportPaths = session.Export();

        logger.Info($"Telemetry captured -> session '{sessionName}', frames CSV: {exportPaths.FramesCsvPath}.");

        return new TelemetryCaptureResult(
            snapshot,
            exportPaths,
            new TelemetryJobSystemSelection(jobActivation.Requested, jobActivation.Resolved, jobActivation.WorkerCount, jobActivation.UsedFallback),
            platformActivation,
            headlessOptions,
            allocatorConfiguration,
            sessionDirectory,
            sessionName,
            lastTiming);
    }

    private RuntimePlatformFactory.PlatformActivation EnsurePlatformServicesInitialized(string? requestedPlatform)
    {
        if (PlatformServices.IsInitialized)
        {
            var resolved = string.IsNullOrWhiteSpace(requestedPlatform) ? "windows" : requestedPlatform.Trim();
            return new RuntimePlatformFactory.PlatformActivation(requestedPlatform, resolved, PlatformServices.Current, false);
        }

        var activation = string.IsNullOrWhiteSpace(requestedPlatform)
            ? RuntimePlatformFactory.CreateFromEnvironment()
            : RuntimePlatformFactory.Create(requestedPlatform);

        PlatformServices.Initialize(activation.Services);
        TimeSource.Initialize(activation.Services.TimeSource);
        return activation;
    }

    private string ResolveSessionDirectory(TelemetryCaptureRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SessionDirectory))
            return Path.IsPathRooted(request.SessionDirectory)
                ? request.SessionDirectory
                : Path.Combine(outputRoot, request.SessionDirectory);

        var defaultFolder = DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(outputRoot, defaultFolder);
    }

    private void SetMetadata(
        DeterministicTelemetrySession session,
        TelemetryCaptureRequest request,
        RuntimeJobSystemFactory.JobSystemActivation jobActivation,
        RuntimePlatformFactory.PlatformActivation platformActivation,
        HeadlessRuntimeOptions headlessOptions,
        string sessionDirectory,
        string sessionName)
    {
        void Set(string key, string value)
        {
            try
            {
                session.Recorder.SetMetadata(key, value);
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to set telemetry metadata '{key}': {ex.Message}");
            }
        }

        Set("telemetry.source", "editor");
        Set("telemetry.session", sessionName);
        Set("telemetry.output", sessionDirectory);

        Set("platform.requested", platformActivation.Requested ?? platformActivation.Resolved);
        Set("platform.resolved", platformActivation.Resolved);
        Set("platform.fallback", platformActivation.UsedFallback ? "true" : "false");

        Set("jobSystem.requested", jobActivation.Requested ?? jobActivation.Resolved);
        Set("jobSystem.resolved", jobActivation.Resolved);
        Set("jobSystem.workers", jobActivation.WorkerCount.ToString(CultureInfo.InvariantCulture));
        Set("jobSystem.fallback", jobActivation.UsedFallback ? "true" : "false");

        Set("headless.frames", headlessOptions.FrameCount.ToString(CultureInfo.InvariantCulture));
        Set("headless.jobsPerFrame", headlessOptions.JobsPerFrame.ToString(CultureInfo.InvariantCulture));
        Set("headless.jobIterations", headlessOptions.JobIterations.ToString(CultureInfo.InvariantCulture));
        Set("headless.innerLoopIterations", headlessOptions.InnerLoopIterations.ToString(CultureInfo.InvariantCulture));
        Set("headless.batch", headlessOptions.BatchSize.ToString(CultureInfo.InvariantCulture));
        Set("headless.targetFps", headlessOptions.TargetFrameRate.ToString("F2", CultureInfo.InvariantCulture));
        Set("headless.seed", headlessOptions.Seed.ToString(CultureInfo.InvariantCulture));
        Set("headless.maxConcurrency", headlessOptions.MaxConcurrentJobs.ToString(CultureInfo.InvariantCulture));
        Set("headless.sampleStatistics", headlessOptions.SampleStatistics ? "true" : "false");

        foreach (var pair in request.Metadata)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;
            Set(pair.Key.Trim(), pair.Value ?? string.Empty);
        }
    }
}

public sealed class TelemetryCaptureRequest
{
    private IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal);

    public string SessionPrefix { get; init; } = "editor";

    public string? SessionSuffix { get; init; }
        = null;

    public bool AppendTimestamp { get; init; } = true;

    public string? SessionDirectory { get; init; }
        = null;

    public string? JobSystemIdentifier { get; init; }
        = null;

    public int? WorkerCountOverride { get; init; }
        = null;

    public string? PlatformIdentifier { get; init; }
        = null;

    public HeadlessRuntimeOptions? HeadlessOptions { get; init; }
        = null;

    public MemoryAllocatorConfiguration? AllocatorConfiguration { get; init; }
        = null;

    public IReadOnlyDictionary<string, string> Metadata
    {
        get => metadata;
        init => metadata = value ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public string BuildSessionName()
    {
        var baseName = string.IsNullOrWhiteSpace(SessionPrefix) ? "editor" : SessionPrefix.Trim();
        if (!string.IsNullOrWhiteSpace(SessionSuffix))
            baseName = baseName + "-" + SessionSuffix.Trim();
        if (AppendTimestamp)
            baseName += "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return baseName;
    }
}

public sealed record TelemetryJobSystemSelection(string? Requested, string Resolved, int WorkerCount, bool UsedFallback);

public sealed record TelemetryCaptureResult(
    RuntimeTelemetryExport Export,
    DeterministicTelemetrySession.TelemetryExportResult Files,
    TelemetryJobSystemSelection JobSystem,
    RuntimePlatformFactory.PlatformActivation Platform,
    HeadlessRuntimeOptions HeadlessOptions,
    MemoryAllocatorConfiguration Allocators,
    string SessionDirectory,
    string SessionName,
    FrameTimingInfo? FinalTiming);