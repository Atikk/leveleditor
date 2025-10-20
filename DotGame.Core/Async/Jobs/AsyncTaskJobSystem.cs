using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotGame.Core.Async.Jobs;

public sealed class AsyncTaskJobSystem : IJobSystem
{
    private readonly AsyncTaskScheduler scheduler;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<ulong, JobState> jobs = new();
    private long nextToken;
    private long completedJobs;
    private int activeWorkers;
    private bool disposed;

    public AsyncTaskJobSystem(int workerCount = 4, string workerNamePrefix = "JobWorker-")
    {
        scheduler = new AsyncTaskScheduler(workerCount, workerNamePrefix);
    }

    public JobHandle Schedule(JobExecuteDelegate execute, in JobScheduleOptions options, ReadOnlySpan<JobHandle> dependencies = default, JobFence? fence = null)
    {
        if (execute == null)
            throw new ArgumentNullException(nameof(execute));

        return ScheduleInternal(execute, iterationCount: 1, options, dependencies, fence);
    }

    public JobHandle ScheduleBatch(in JobBatchDescriptor batch, ReadOnlySpan<JobHandle> dependencies = default, JobFence? fence = null)
    {
        return ScheduleInternal(batch.Execute, batch.IterationCount, batch.Options, dependencies, fence);
    }

    public JobHandle CombineDependencies(ReadOnlySpan<JobHandle> handles, JobFence? fence = null)
    {
        var tasks = ResolveDependencyTasks(handles);
        if (tasks.Count == 0)
            return JobHandle.Invalid;

        var handle = CreateState(new JobScheduleOptions("Combine", JobPriority.Low, JobAffinity.Any, allowInlineExecution: true), iterationCount: 0, out var state, fence);

        Task.WhenAll(tasks).ContinueWith(static (t, s) =>
        {
            var jobState = (JobState)s!;
            if (t.IsFaulted)
            {
                var ex = t.Exception?.InnerException ?? (Exception?)t.Exception ?? new InvalidOperationException("Dependency failed.");
                jobState.TryFail(ex);
            }
            else if (t.IsCanceled)
            {
                jobState.TryCancel();
            }
            else
            {
                jobState.TryComplete();
            }
        }, state, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        return handle;
    }

    public void Complete(JobHandle handle)
    {
        if (!handle.IsValid)
            return;

        if (jobs.TryGetValue(handle.Token, out var state))
            state.Completion.Task.GetAwaiter().GetResult();
    }

    public void WaitAll(ReadOnlySpan<JobHandle> handles)
    {
        var tasks = new List<Task>(handles.Length);
        foreach (var handle in handles)
        {
            if (!handle.IsValid)
                continue;

            if (jobs.TryGetValue(handle.Token, out var state))
                tasks.Add(state.Completion.Task);
        }

        if (tasks.Count > 0)
            Task.WaitAll(tasks.ToArray());
    }

    public JobStatistics GetStatistics()
    {
        var pending = jobs.Count(static pair => !pair.Value.Completion.Task.IsCompleted);
        var completed = Math.Clamp((int)Interlocked.Read(ref completedJobs), 0, int.MaxValue);
        var active = Math.Clamp(Volatile.Read(ref activeWorkers), 0, scheduler.WorkerCount + 1);
        return new JobStatistics(pending, active, completed);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        cancellation.Cancel();

        foreach (var state in jobs.Values)
            state.TryCancel();

        jobs.Clear();
        Interlocked.Exchange(ref activeWorkers, 0);
        scheduler.Dispose();
        cancellation.Dispose();
    }

    private JobHandle ScheduleInternal(JobExecuteDelegate execute, int iterationCount, JobScheduleOptions options, ReadOnlySpan<JobHandle> dependencies, JobFence? fence)
    {
        if (iterationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterationCount));

        ThrowIfDisposed();

        var handle = CreateState(options, iterationCount, out var state, fence);
        var dependencyTasks = ResolveDependencyTasks(dependencies);

        void QueueWork()
        {
            if (state.Completion.Task.IsCompleted)
                return;

            try
            {
                state.TryAcquireConcurrency(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                state.TryCancel();
                return;
            }

            QueueSegments(state, execute, iterationCount, options);
        }

        if (dependencyTasks.Count == 0)
        {
            QueueWork();
        }
        else
        {
            Task.WhenAll(dependencyTasks).ContinueWith(static (t, s) =>
            {
                var payload = (Tuple<Action, JobState>)s!;
                var callback = payload.Item1;
                var jobState = payload.Item2;

                if (t.IsFaulted)
                {
                    var ex = t.Exception?.InnerException ?? (Exception?)t.Exception ?? new InvalidOperationException("Dependency failed.");
                    jobState.TryFail(ex);
                }
                else if (t.IsCanceled)
                {
                    jobState.TryCancel();
                }
                else
                {
                    callback();
                }
            }, Tuple.Create<Action, JobState>(QueueWork, state), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        return handle;
    }

    private List<Task> ResolveDependencyTasks(ReadOnlySpan<JobHandle> dependencies)
    {
        var tasks = new List<Task>(dependencies.Length);
        foreach (var dependency in dependencies)
        {
            if (!dependency.IsValid)
                continue;

            if (jobs.TryGetValue(dependency.Token, out var state))
            {
                tasks.Add(state.Completion.Task);
            }
        }

        return tasks;
    }

    private void QueueSegments(JobState state, JobExecuteDelegate execute, int iterationCount, JobScheduleOptions options)
    {
        var batchSize = Math.Max(1, options.BatchSize);
        var segments = Math.Max(1, (int)Math.Ceiling(iterationCount / (double)batchSize));
        state.SetPendingSegments(segments);

        for (var segmentIndex = 0; segmentIndex < segments; segmentIndex++)
        {
            var start = segmentIndex * batchSize;
            var count = Math.Min(batchSize, iterationCount - start);

            if (options.AllowInlineExecution && segmentIndex == 0 && segments == 1)
            {
                ExecuteSegment(state, execute, start, count, cancellation.Token, inline: true);
            }
            else
            {
                scheduler.Enqueue(token => ExecuteSegment(state, execute, start, count, token, inline: false), onError: ex => state.TryFail(ex));
            }
        }
    }

    private void ExecuteSegment(JobState state, JobExecuteDelegate execute, int start, int count, CancellationToken token, bool inline)
    {
        if (state.Completion.Task.IsCompleted)
            return;

        Interlocked.Increment(ref activeWorkers);
        try
        {
            for (var i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                var iterationIndex = start + i;
                var workerIndex = inline ? -1 : Thread.CurrentThread.ManagedThreadId;
                var context = new JobExecutionContext(token, workerIndex, iterationIndex, state.IterationCount);
                execute(context);
            }

            state.TrySegmentComplete();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            state.TryCancel();
        }
        catch (Exception ex)
        {
            state.TryFail(ex);
        }
        finally
        {
            Interlocked.Decrement(ref activeWorkers);
        }
    }

    private JobHandle CreateState(JobScheduleOptions options, int iterationCount, out JobState state, JobFence? fence)
    {
        var token = (ulong)Interlocked.Increment(ref nextToken);
        state = new JobState(token, options, iterationCount, OnJobFinished, fence);
        jobs[token] = state;
        fence?.RegisterProducer();
        return new JobHandle(token);
    }

    private void OnJobFinished(JobState state)
    {
        if (jobs.TryRemove(state.Token, out _))
            Interlocked.Increment(ref completedJobs);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(AsyncTaskJobSystem));
    }

        private sealed class JobState
    {
        private readonly Action<JobState> completionCallback;
        private readonly JobFence? fence;
            private readonly JobSemaphore? concurrencyLimiter;
        private int pendingSegments;
        private int completionSignalled;
            private int concurrencyAcquired;

        public JobState(ulong token, JobScheduleOptions options, int iterationCount, Action<JobState> completionCallback, JobFence? fence)
        {
            Token = token;
            Options = options;
            IterationCount = iterationCount;
            this.completionCallback = completionCallback;
            this.fence = fence;
            concurrencyLimiter = options.ConcurrencyLimiter;
            Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public ulong Token { get; }

        public JobScheduleOptions Options { get; }

        public int IterationCount { get; }

        public TaskCompletionSource<bool> Completion { get; }

        public void SetPendingSegments(int count)
        {
            pendingSegments = count;
        }

        public void TryAcquireConcurrency(CancellationToken token)
        {
            if (concurrencyLimiter == null)
                return;

            if (Volatile.Read(ref concurrencyAcquired) == 1)
                return;

            concurrencyLimiter.Wait(token);
            Volatile.Write(ref concurrencyAcquired, 1);
        }

        public void TrySegmentComplete()
        {
            if (Completion.Task.IsCompleted)
                return;

            if (Interlocked.Decrement(ref pendingSegments) == 0)
                TryComplete();
        }

        public void TryComplete()
        {
            if (Interlocked.Exchange(ref completionSignalled, 1) != 0)
                return;

            Completion.TrySetResult(true);
            ReleaseConcurrency();
            completionCallback(this);
            fence?.SignalSuccess();
        }

        public void TryFail(Exception exception)
        {
            if (Interlocked.Exchange(ref completionSignalled, 1) != 0)
                return;

            Completion.TrySetException(exception);
            ReleaseConcurrency();
            completionCallback(this);
            fence?.SignalFailure(exception);
        }

        public void TryCancel()
        {
            if (Interlocked.Exchange(ref completionSignalled, 1) != 0)
                return;

            Completion.TrySetCanceled();
            ReleaseConcurrency();
            completionCallback(this);
            fence?.SignalCanceled();
        }

        private void ReleaseConcurrency()
        {
            if (concurrencyLimiter == null)
                return;

            if (Interlocked.Exchange(ref concurrencyAcquired, 0) == 1)
            {
                concurrencyLimiter.Release();
            }
        }
    }
}
