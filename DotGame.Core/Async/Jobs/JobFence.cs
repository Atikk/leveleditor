using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotGame.Core.Async.Jobs;

/// <summary>
/// Synchronization primitive that completes when all scheduled jobs associated with the fence finish.
/// </summary>
public sealed class JobFence
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ExceptionDispatchInfo? failure;
    private int pendingSignals;
    private int cancellations;

    internal void RegisterProducer()
    {
        Interlocked.Increment(ref pendingSignals);
    }

    internal void SignalSuccess()
    {
        TryFinalize(null, canceled: false);
    }

    internal void SignalFailure(Exception exception)
    {
        if (exception != null)
        {
            var capture = ExceptionDispatchInfo.Capture(exception);
            Interlocked.CompareExchange(ref failure, capture, null);
        }

        TryFinalize(exception, canceled: false);
    }

    internal void SignalCanceled()
    {
        Interlocked.Increment(ref cancellations);
        TryFinalize(null, canceled: true);
    }

    private void TryFinalize(Exception? latestError, bool canceled)
    {
        var remaining = Interlocked.Decrement(ref pendingSignals);
        if (remaining < 0)
            throw new InvalidOperationException("Job fence signalled more times than it was registered.");

        if (remaining > 0)
            return;

        if (canceled || Volatile.Read(ref cancellations) > 0)
        {
            completion.TrySetCanceled();
            return;
        }

        var captured = Volatile.Read(ref failure);
        if (captured != null)
        {
            completion.TrySetException(captured.SourceException ?? latestError ?? new InvalidOperationException("Job fence failed."));
            return;
        }

        completion.TrySetResult(true);
    }

    /// <summary>
    /// Blocks the calling thread until the fence is completed.
    /// </summary>
    public void Wait(CancellationToken cancellationToken = default)
    {
        completion.Task.Wait(cancellationToken);
    }

    /// <summary>
    /// Asynchronously waits for the fence to complete.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Indicates whether the fence has finished (successfully, faulted, or canceled).
    /// </summary>
    public bool IsCompleted => completion.Task.IsCompleted;
}
