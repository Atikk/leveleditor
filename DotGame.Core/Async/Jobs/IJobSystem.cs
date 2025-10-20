using System;

namespace DotGame.Core.Async.Jobs;

public interface IJobSystem : IDisposable
{
    JobHandle Schedule(JobExecuteDelegate execute, in JobScheduleOptions options, ReadOnlySpan<JobHandle> dependencies = default, JobFence? fence = null);

    JobHandle ScheduleBatch(in JobBatchDescriptor batch, ReadOnlySpan<JobHandle> dependencies = default, JobFence? fence = null);

    JobHandle CombineDependencies(ReadOnlySpan<JobHandle> handles, JobFence? fence = null);

    void Complete(JobHandle handle);

    void WaitAll(ReadOnlySpan<JobHandle> handles);

    JobStatistics GetStatistics();
}
