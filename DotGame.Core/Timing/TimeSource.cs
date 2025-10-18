using System;

namespace DotGame.Core.Timing;

public static class TimeSource
{
    private static readonly object Gate = new();
    private static ITimeSource current = CreateDefault();

    public static ITimeSource Current
    {
        get
        {
            lock (Gate)
            {
                return current;
            }
        }
    }

    public static void Initialize(ITimeSource timeSource)
    {
        if (timeSource == null)
            throw new ArgumentNullException(nameof(timeSource));

        lock (Gate)
        {
            current = timeSource;
        }
    }

    private static ITimeSource CreateDefault()
    {
        try
        {
            return new HighResolutionTimeSource();
        }
        catch (Exception)
        {
            return new FallbackTimeSource();
        }
    }

    private sealed class FallbackTimeSource : ITimeSource
    {
        private readonly long frequency = TimeSpan.TicksPerSecond;

        public long TickFrequency => frequency;

        public long GetTimestamp() => DateTime.UtcNow.Ticks;

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
}
