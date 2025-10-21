using System;
using System.Threading;
using DotGame.Core.Logging;
using DotGame.Core.Memory;
using DotGame.Core.Platform;
using DotGame.Core.Timing;
using DotGame.Runtime.Diagnostics;
using DotGame.Runtime.Platform;
using DotGame.Runtime.Services;

LogManager.Initialize(LogLevel.Information, new[] { (ILogSink)new ConsoleLogSink() });

var logger = LogManager.GetLogger("Runtime.Program");

var telemetryDirectory = Environment.GetEnvironmentVariable("DOTGAME_QA_TELEMETRY_DIR");
var telemetrySessionName = Environment.GetEnvironmentVariable("DOTGAME_QA_SESSION");
DeterministicTelemetrySession? telemetrySession = null;
DeterministicTelemetrySession.TelemetryExportResult? telemetryExportResult = null;

if (!string.IsNullOrWhiteSpace(telemetryDirectory))
{
    try
    {
        telemetrySession = new DeterministicTelemetrySession(
            telemetryDirectory,
            string.IsNullOrWhiteSpace(telemetrySessionName) ? "runtime" : telemetrySessionName);
        telemetrySession.Start();
        logger.Info($"Runtime telemetry session active; exporting to '{telemetryDirectory}'.");
        TrySetTelemetryMetadata("telemetry.session", telemetrySessionName ?? "runtime");
    }
    catch (Exception ex)
    {
        logger.Warn($"Telemetry session initialization failed: {ex.Message}");
        telemetrySession = null;
    }
}

var platformActivation = RuntimePlatformFactory.CreateFromEnvironment();
var platformServices = platformActivation.Services;
PlatformServices.Initialize(platformServices);
TimeSource.Initialize(platformServices.TimeSource);

var requestedPlatform = string.IsNullOrWhiteSpace(platformActivation.Requested) ? "default" : platformActivation.Requested;
logger.Info($"Platform services selection -> requested='{requestedPlatform}', resolved='{platformActivation.Resolved}'.");
if (platformActivation.UsedFallback)
    logger.Warn($"Unknown platform identifier '{requestedPlatform}'. Falling back to '{platformActivation.Resolved}'.");
TrySetTelemetryMetadata("platform.requested", requestedPlatform);
TrySetTelemetryMetadata("platform.resolved", platformActivation.Resolved);
TrySetTelemetryMetadata("platform.fallback", platformActivation.UsedFallback ? "true" : "false");

var allocatorConfiguration = MemoryAllocatorConfiguration.FromEnvironment();
using var allocatorSet = allocatorConfiguration.CreateAllocators();
logger.Info(
    $"Allocators configured -> arena={allocatorConfiguration.Arena.Name}:{allocatorConfiguration.Arena.CapacityBytes / 1048576.0:F1} MiB, " +
    $"stack={allocatorConfiguration.Stack.Name}:{allocatorConfiguration.Stack.CapacityBytes / 1024.0:F1} KiB, " +
    $"pool={allocatorConfiguration.Pool.Name}:{allocatorConfiguration.Pool.BlockCount}x{allocatorConfiguration.Pool.BlockSizeBytes} ({allocatorConfiguration.Pool.BlockCount * allocatorConfiguration.Pool.BlockSizeBytes / 1048576.0:F2} MiB).");

var jobSystemActivation = RuntimeJobSystemFactory.CreateFromEnvironment();
var requestedJobSystem = string.IsNullOrWhiteSpace(jobSystemActivation.Requested) ? "default" : jobSystemActivation.Requested;
logger.Info($"Job system selection -> requested='{requestedJobSystem}', resolved='{jobSystemActivation.Resolved}', workers={jobSystemActivation.WorkerCount}.");
if (jobSystemActivation.UsedFallback)
    logger.Warn($"Unknown job system identifier '{requestedJobSystem}'. Falling back to '{jobSystemActivation.Resolved}'.");
TrySetTelemetryMetadata("jobSystem.requested", requestedJobSystem);
TrySetTelemetryMetadata("jobSystem.resolved", jobSystemActivation.Resolved);
TrySetTelemetryMetadata("jobSystem.workers", jobSystemActivation.WorkerCount.ToString());
TrySetTelemetryMetadata("jobSystem.fallback", jobSystemActivation.UsedFallback ? "true" : "false");

var headlessFlag = Environment.GetEnvironmentVariable("DOTGAME_RUNTIME_HEADLESS");
var runHeadless = IsHeadlessEnabled(headlessFlag);
HeadlessRuntimeOptions? headlessOptions = null;
if (runHeadless)
{
    headlessOptions = HeadlessRuntimeOptions.FromEnvironment();
    logger.Info(
        $"Headless mode enabled -> frames={headlessOptions.FrameCount}, jobs/frame={headlessOptions.JobsPerFrame}, " +
        $"iterations={headlessOptions.JobIterations}x{headlessOptions.InnerLoopIterations}, batch={headlessOptions.BatchSize}, " +
        $"fps={headlessOptions.TargetFrameRate:F2}.");
    TrySetTelemetryMetadata("headless.frames", headlessOptions.FrameCount.ToString());
    TrySetTelemetryMetadata("headless.jobsPerFrame", headlessOptions.JobsPerFrame.ToString());
    TrySetTelemetryMetadata("headless.jobIterations", headlessOptions.JobIterations.ToString());
    TrySetTelemetryMetadata("headless.innerLoopIterations", headlessOptions.InnerLoopIterations.ToString());
    TrySetTelemetryMetadata("headless.batch", headlessOptions.BatchSize.ToString());
    TrySetTelemetryMetadata("headless.targetFps", headlessOptions.TargetFrameRate.ToString("F2"));
}
TrySetTelemetryMetadata("headless.enabled", runHeadless ? "true" : "false");

using var jobSystem = jobSystemActivation.JobSystem;
using var cancellation = new CancellationTokenSource();

IDisposable? runtimeDisposable = null;
Func<bool> stepFrame;
double targetFrameRate;
DotGame.Runtime.Game1? game = null;
HeadlessRuntimeHarness? headlessHarness = null;

if (runHeadless)
{
    headlessHarness = new HeadlessRuntimeHarness(jobSystem, headlessOptions!, allocatorSet);
    runtimeDisposable = headlessHarness;
    targetFrameRate = headlessOptions!.TargetFrameRate;
    stepFrame = () => headlessHarness.StepFrame();
}
else
{
    game = new DotGame.Runtime.Game1(jobSystem, jobSystemActivation.WorkerCount, allocatorSet);
    runtimeDisposable = game;
    targetFrameRate = 60.0;
    stepFrame = () =>
    {
        game.RunOneFrame();
        return true;
    };
}

var exitRequested = false;
var latestTiming = default(FrameTimingInfo);
var hasTiming = false;

Console.CancelKeyPress += OnCancelKeyPress;
if (game != null)
    game.Exiting += OnGameExiting;

try
{
    using (runtimeDisposable)
    {
        var controller = new FrameLoopController(TimeSource.Current, targetFrameRate);
        controller.RegisterListener(new FrameTimingLogListener(LogManager.GetLogger("RuntimeLoop")));

        telemetrySession?.Attach(controller);
        if (telemetrySession != null)
        {
            try
            {
                telemetrySession.TrackJobSystem(jobSystemActivation.Resolved, jobSystem, jobSystemActivation.WorkerCount);
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to attach job system telemetry: {ex.Message}");
            }
        }

        logger.Info($"Runtime starting with deterministic frame controller ({targetFrameRate:F2} Hz).");

        controller.Run(
            _ =>
            {
                if (exitRequested || cancellation.IsCancellationRequested)
                    return false;

                var shouldContinue = stepFrame();
                if (!shouldContinue)
                {
                    exitRequested = true;
                    cancellation.Cancel();
                    return false;
                }

                return !exitRequested && !cancellation.IsCancellationRequested;
            },
            timing =>
            {
                latestTiming = timing;
                hasTiming = true;
            },
            cancellation.Token);
    }
}
catch (OperationCanceledException)
{
    logger.Info("Runtime loop cancelled.");
}
catch (Exception ex)
{
    logger.Error("Runtime loop terminated with an unexpected error.", ex);
    throw;
}
finally
{
    Console.CancelKeyPress -= OnCancelKeyPress;
    if (game != null)
        game.Exiting -= OnGameExiting;

    if (hasTiming)
    {
        logger.Info(
            $"Final frame stats -> budget: {latestTiming.TargetFrameTime.TotalMilliseconds:F2} ms, " +
            $"actual: {latestTiming.ActualFrameTime.TotalMilliseconds:F2} ms, " +
            $"drift: {latestTiming.AccumulatedDrift.TotalMilliseconds:F2} ms, " +
            $"fixed steps: {latestTiming.FixedStepCount}.");
    }

    if (telemetrySession != null)
    {
        try
        {
            var export = telemetrySession.Export();
            telemetryExportResult = export;
            logger.Info($"Telemetry exported -> JSON: {export.JsonPath}, frames CSV: {export.FramesCsvPath}.");
            if (export.AllocatorCsvPaths.Count > 0)
                logger.Info("Allocator telemetry exports: " + string.Join(", ", export.AllocatorCsvPaths));
            if (export.JobSystemCsvPaths.Count > 0)
                logger.Info("Job system telemetry exports: " + string.Join(", ", export.JobSystemCsvPaths));
        }
        catch (Exception ex)
        {
            logger.Warn($"Telemetry export failed: {ex.Message}");
        }
        finally
        {
            telemetrySession.Dispose();
        }
    }

    logger.Info("Runtime shutdown.");
}

void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
{
    if (!cancellation.IsCancellationRequested)
    {
        logger.Warn("Ctrl+C detected. Requesting runtime shutdown...");
        cancellation.Cancel();
    }

    args.Cancel = true;
}

void OnGameExiting(object? sender, EventArgs args)
{
    exitRequested = true;
    cancellation.Cancel();
}

static bool IsHeadlessEnabled(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return false;

    var trimmed = value.Trim();
    return trimmed.Equals("1", StringComparison.OrdinalIgnoreCase)
        || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
        || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || trimmed.Equals("on", StringComparison.OrdinalIgnoreCase)
        || trimmed.Equals("headless", StringComparison.OrdinalIgnoreCase);
}

void TrySetTelemetryMetadata(string key, string value)
{
    if (telemetrySession == null)
        return;

    try
    {
        telemetrySession.Recorder.SetMetadata(key, value);
    }
    catch (Exception ex)
    {
        logger.Warn($"Failed to record telemetry metadata '{key}': {ex.Message}");
    }
}
