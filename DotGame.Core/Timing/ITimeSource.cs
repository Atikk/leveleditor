using System;

namespace DotGame.Core.Timing;

public interface ITimeSource
{
    long TickFrequency { get; }

    long GetTimestamp();

    DateTimeOffset GetCurrentTime();

    double GetElapsedSeconds(long startTimestamp, long endTimestamp);

    TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp);

    double ToSeconds(long timestampDelta);

    TimeSpan ToTimeSpan(long timestampDelta);
}
