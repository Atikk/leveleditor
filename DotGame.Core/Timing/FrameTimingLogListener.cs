using System;
using DotGame.Core.Logging;

namespace DotGame.Core.Timing;

public sealed class FrameTimingLogListener : IFrameBudgetListener
{
    private readonly ILogger logger;
    private readonly int aggregationWindow;
    private readonly LogLevel budgetExceededLevel;

    private int accumulatedFrames;
    private TimeSpan totalActualTime = TimeSpan.Zero;
    private TimeSpan worstFrame = TimeSpan.Zero;

    public FrameTimingLogListener(ILogger? logger = null, int aggregationWindow = 120, LogLevel budgetExceededLevel = LogLevel.Warning)
    {
        if (aggregationWindow <= 0)
            throw new ArgumentOutOfRangeException(nameof(aggregationWindow));

        this.logger = logger ?? LogManager.GetLogger("FrameTiming");
        this.aggregationWindow = aggregationWindow;
        this.budgetExceededLevel = budgetExceededLevel;
    }

    public void OnFrameStart(in FrameTimingInfo timing)
    {
    }

    public void OnBudgetExceeded(in FrameTimingInfo timing)
    {
        logger.Log(budgetExceededLevel, $"Frame {timing.FrameIndex} exceeded budget. Target {timing.TargetFrameTime.TotalMilliseconds:F2} ms, actual {timing.ActualFrameTime.TotalMilliseconds:F2} ms, drift {timing.AccumulatedDrift.TotalMilliseconds:F2} ms.");
    }

    public void OnFrameEnd(in FrameTimingInfo timing)
    {
        accumulatedFrames++;
        totalActualTime += timing.ActualFrameTime;
        if (timing.ActualFrameTime > worstFrame)
            worstFrame = timing.ActualFrameTime;

        if (accumulatedFrames >= aggregationWindow)
        {
            var average = TimeSpan.FromTicks(totalActualTime.Ticks / accumulatedFrames);
            logger.Debug($"Frame timing window ({aggregationWindow} frames) avg {average.TotalMilliseconds:F2} ms, peak {worstFrame.TotalMilliseconds:F2} ms, drift {timing.AccumulatedDrift.TotalMilliseconds:F2} ms.");
            accumulatedFrames = 0;
            totalActualTime = TimeSpan.Zero;
            worstFrame = TimeSpan.Zero;
        }
    }
}
