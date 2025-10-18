using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DotGame.Core.Timing;

public sealed class FrameLoopController
{
    private const double DefaultMaxCatchUpSeconds = 0.25;

    private readonly ITimeSource timeSource;
    private readonly List<IFrameBudgetListener> listeners = new();
    private readonly object listenerGate = new();
    private readonly long targetFrameTicks;
    private readonly long maxDeltaTicks;

    public FrameLoopController(ITimeSource timeSource, double targetFrameRate, TimeSpan? maximumCatchUp = null)
    {
        if (timeSource == null)
            throw new ArgumentNullException(nameof(timeSource));
        if (targetFrameRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetFrameRate));

        this.timeSource = timeSource;
        TargetFrameRate = targetFrameRate;
        TargetFrameTime = TimeSpan.FromSeconds(1.0 / targetFrameRate);
        targetFrameTicks = (long)Math.Round(timeSource.TickFrequency / targetFrameRate);

        var requestedMaxDelta = maximumCatchUp?.TotalSeconds ?? DefaultMaxCatchUpSeconds;
        if (requestedMaxDelta <= 0)
            requestedMaxDelta = DefaultMaxCatchUpSeconds;

        var calculatedMaxDeltaTicks = (long)Math.Round(timeSource.TickFrequency * requestedMaxDelta);
        if (calculatedMaxDeltaTicks < targetFrameTicks)
            calculatedMaxDeltaTicks = targetFrameTicks;

        maxDeltaTicks = calculatedMaxDeltaTicks;
        MaximumDeltaTime = timeSource.ToTimeSpan(maxDeltaTicks);
    }

    public double TargetFrameRate { get; }

    public TimeSpan TargetFrameTime { get; }

    public TimeSpan MaximumDeltaTime { get; }

    public void RegisterListener(IFrameBudgetListener listener)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        lock (listenerGate)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }
    }

    public void UnregisterListener(IFrameBudgetListener listener)
    {
        if (listener == null)
            return;

        lock (listenerGate)
        {
            listeners.Remove(listener);
        }
    }

    public void Run(
        Func<TimeSpan, bool> fixedStepCallback,
        Action<FrameTimingInfo>? presentCallback,
        CancellationToken cancellationToken)
    {
        if (fixedStepCallback == null)
            throw new ArgumentNullException(nameof(fixedStepCallback));

        var frameIndex = 0;
        var accumulatorTicks = 0L;
        var previousTimestamp = timeSource.GetTimestamp();
        var lastFrameStartTimestamp = previousTimestamp;

        while (!cancellationToken.IsCancellationRequested)
        {
            var frameStartTimestamp = timeSource.GetTimestamp();
            var elapsedSinceLastFrameTicks = frameStartTimestamp - lastFrameStartTimestamp;
            if (elapsedSinceLastFrameTicks < 0)
                elapsedSinceLastFrameTicks = 0;

            var deltaTicks = frameStartTimestamp - previousTimestamp;
            if (deltaTicks < 0)
                deltaTicks = 0;

            if (deltaTicks > maxDeltaTicks)
                deltaTicks = maxDeltaTicks;

            accumulatorTicks += deltaTicks;
            previousTimestamp = frameStartTimestamp;

            var elapsedSinceLastFrame = timeSource.ToTimeSpan(elapsedSinceLastFrameTicks);

            var timingStart = new FrameTimingInfo(
                frameIndex,
                TargetFrameTime,
                TimeSpan.Zero,
                elapsedSinceLastFrame,
                timeSource.ToTimeSpan(accumulatorTicks),
                fixedStepCount: 0);

            NotifyListeners(static (listener, info) => listener.OnFrameStart(info), timingStart);

            var fixedStepCount = 0;
            var continueLoop = true;

            while (accumulatorTicks >= targetFrameTicks && continueLoop && !cancellationToken.IsCancellationRequested)
            {
                continueLoop = fixedStepCallback(TargetFrameTime);
                fixedStepCount++;
                accumulatorTicks -= targetFrameTicks;
            }

            var afterUpdateTimestamp = timeSource.GetTimestamp();
            var actualFrameTicks = afterUpdateTimestamp - frameStartTimestamp;
            if (actualFrameTicks < 0)
                actualFrameTicks = 0;

            var actualFrameTime = timeSource.ToTimeSpan(actualFrameTicks);
            var accumulatedDrift = timeSource.ToTimeSpan(accumulatorTicks);

            var timing = new FrameTimingInfo(
                frameIndex,
                TargetFrameTime,
                actualFrameTime,
                elapsedSinceLastFrame,
                accumulatedDrift,
                fixedStepCount);

            if (timing.BudgetExceeded)
                NotifyListeners(static (listener, info) => listener.OnBudgetExceeded(info), timing);

            presentCallback?.Invoke(timing);

            NotifyListeners(static (listener, info) => listener.OnFrameEnd(info), timing);

            if (!continueLoop || cancellationToken.IsCancellationRequested)
                break;

            var nextFrameTimestamp = frameStartTimestamp + targetFrameTicks;
            var remainingTicks = nextFrameTimestamp - timeSource.GetTimestamp() - accumulatorTicks;
            if (remainingTicks > 0)
                SleepPrecise(remainingTicks);

            lastFrameStartTimestamp = frameStartTimestamp;
            frameIndex++;
        }
    }

    private void SleepPrecise(long ticks)
    {
        if (ticks <= 0)
            return;

        var remaining = timeSource.ToTimeSpan(ticks);
        if (remaining >= TimeSpan.FromMilliseconds(2))
        {
            var sleep = remaining - TimeSpan.FromMilliseconds(1);
            if (sleep > TimeSpan.Zero)
                Thread.Sleep(sleep);
        }

        var targetTimestamp = timeSource.GetTimestamp() + ticks;
        while (timeSource.GetTimestamp() < targetTimestamp)
            Thread.SpinWait(10);
    }

    private void NotifyListeners(Action<IFrameBudgetListener, FrameTimingInfo> invoker, in FrameTimingInfo info)
    {
        IFrameBudgetListener[] snapshot;
        lock (listenerGate)
        {
            if (listeners.Count == 0)
                return;
            snapshot = listeners.ToArray();
        }

        foreach (var listener in snapshot)
        {
            invoker(listener, info);
        }
    }
}
