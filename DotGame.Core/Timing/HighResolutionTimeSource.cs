using System;
using System.Diagnostics;

namespace DotGame.Core.Timing;

public sealed class HighResolutionTimeSource : ITimeSource
{
    private readonly long frequency;

    public HighResolutionTimeSource()
    {
        frequency = Stopwatch.Frequency;
        if (frequency <= 0)
            throw new InvalidOperationException("High resolution timers are not supported on this platform.");
    }

    public long TickFrequency => frequency;

    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public DateTimeOffset GetCurrentTime() => DateTimeOffset.UtcNow;

    public double GetElapsedSeconds(long startTimestamp, long endTimestamp)
    {
        return ToSeconds(endTimestamp - startTimestamp);
    }

    public TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp)
    {
        return ToTimeSpan(endTimestamp - startTimestamp);
    }

    public double ToSeconds(long timestampDelta)
    {
        return timestampDelta / (double)frequency;
    }

    public TimeSpan ToTimeSpan(long timestampDelta)
    {
        return TimeSpan.FromSeconds(ToSeconds(timestampDelta));
    }
}
