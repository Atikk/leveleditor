using System;
using System.Threading;
using DotGame.Core.Logging;
using DotGame.Core.Platform;
using DotGame.Core.Timing;
using DotGame.Runtime.Platform;

var logger = LogManager.GetLogger("Runtime.Program");

var platformServices = new WindowsPlatformServices();
PlatformServices.Initialize(platformServices);
TimeSource.Initialize(platformServices.TimeSource);

using var game = new DotGame.Runtime.Game1();
using var cancellation = new CancellationTokenSource();

var exitRequested = false;
var latestTiming = default(FrameTimingInfo);
var hasTiming = false;

Console.CancelKeyPress += OnCancelKeyPress;
game.Exiting += OnGameExiting;

try
{
    var controller = new FrameLoopController(TimeSource.Current, 60.0);
    controller.RegisterListener(new FrameTimingLogListener(LogManager.GetLogger("RuntimeLoop")));

    logger.Info("Runtime starting with deterministic frame controller (60 Hz).");

    controller.Run(
        _ =>
        {
            if (exitRequested || cancellation.IsCancellationRequested)
                return false;

            game.RunOneFrame();
            return !exitRequested && !cancellation.IsCancellationRequested;
        },
        timing =>
        {
            latestTiming = timing;
            hasTiming = true;
        },
        cancellation.Token);
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
    game.Exiting -= OnGameExiting;

    if (hasTiming)
    {
        logger.Info(
            $"Final frame stats -> budget: {latestTiming.TargetFrameTime.TotalMilliseconds:F2} ms, " +
            $"actual: {latestTiming.ActualFrameTime.TotalMilliseconds:F2} ms, " +
            $"drift: {latestTiming.AccumulatedDrift.TotalMilliseconds:F2} ms, " +
            $"fixed steps: {latestTiming.FixedStepCount}.");
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
