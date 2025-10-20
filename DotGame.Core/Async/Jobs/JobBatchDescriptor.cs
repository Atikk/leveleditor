using System;

namespace DotGame.Core.Async.Jobs;

public readonly struct JobBatchDescriptor
{
    public JobBatchDescriptor(JobExecuteDelegate execute, int iterationCount, JobScheduleOptions options)
    {
        if (execute == null)
            throw new ArgumentNullException(nameof(execute));
        if (iterationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterationCount));

        Execute = execute;
        IterationCount = iterationCount;
        Options = options;
    }

    public JobExecuteDelegate Execute { get; }

    public int IterationCount { get; }

    public JobScheduleOptions Options { get; }
}
