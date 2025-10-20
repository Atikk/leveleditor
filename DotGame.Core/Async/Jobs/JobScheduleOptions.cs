using System;

namespace DotGame.Core.Async.Jobs;

public readonly struct JobScheduleOptions
{
    public JobScheduleOptions(
        string name,
        JobPriority priority,
        JobAffinity affinity,
        bool allowInlineExecution,
        int batchSize = 1,
        JobSemaphore? concurrencyLimiter = null)
    {
        Name = name ?? string.Empty;
        Priority = priority;
        Affinity = affinity;
        AllowInlineExecution = allowInlineExecution;
        BatchSize = Math.Max(1, batchSize);
        ConcurrencyLimiter = concurrencyLimiter;
    }

    public string Name { get; }

    public JobPriority Priority { get; }

    public JobAffinity Affinity { get; }

    public bool AllowInlineExecution { get; }

    public int BatchSize { get; }

    public JobSemaphore? ConcurrencyLimiter { get; }
}
