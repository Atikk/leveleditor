using System;

namespace DotGame.Core.Timing;

public readonly struct FrameTimingInfo
{
    public FrameTimingInfo(
        int frameIndex,
        TimeSpan targetFrameTime,
        TimeSpan actualFrameTime,
        TimeSpan elapsedSinceLastFrame,
        TimeSpan accumulatedDrift,
        int fixedStepCount)
    {
        FrameIndex = frameIndex;
        TargetFrameTime = targetFrameTime;
        ActualFrameTime = actualFrameTime;
        ElapsedSinceLastFrame = elapsedSinceLastFrame;
        AccumulatedDrift = accumulatedDrift;
        FixedStepCount = fixedStepCount;
    }

    public int FrameIndex { get; }

    public TimeSpan TargetFrameTime { get; }

    public TimeSpan ActualFrameTime { get; }

    public TimeSpan ElapsedSinceLastFrame { get; }

    public TimeSpan AccumulatedDrift { get; }

    public int FixedStepCount { get; }

    public bool BudgetExceeded => ActualFrameTime > TargetFrameTime;
}
