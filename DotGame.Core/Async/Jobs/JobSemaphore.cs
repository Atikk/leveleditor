using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotGame.Core.Async.Jobs;

/// <summary>
/// Counting semaphore tailored for job system producers to cap parallel inflight work.
/// </summary>
public sealed class JobSemaphore
{
    private readonly object gate = new();
    private readonly Queue<WaitRequest> waiters = new();
    private readonly int maxCount;
    private int currentCount;

    public JobSemaphore(int initialCount, int maxCount)
    {
        if (initialCount < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCount));
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        if (initialCount > maxCount)
            throw new ArgumentException("Initial count cannot exceed maximum count.", nameof(initialCount));

        currentCount = initialCount;
        this.maxCount = maxCount;
    }

    public JobSemaphore(int initialCount)
        : this(initialCount, initialCount)
    {
    }

    /// <summary>
    /// Gets the maximum number of permits for the semaphore.
    /// </summary>
    public int MaxCount => maxCount;

    /// <summary>
    /// Gets the currently available permit count.
    /// </summary>
    public int CurrentCount => Volatile.Read(ref currentCount);

    /// <summary>
    /// Gets the number of waiters currently blocked on the semaphore.
    /// </summary>
    public int WaitingCount
    {
        get
        {
            lock (gate)
            {
                return waiters.Count;
            }
        }
    }

    /// <summary>
    /// Attempts to acquire a permit without blocking.
    /// </summary>
    public bool TryWait()
    {
        lock (gate)
        {
            if (currentCount <= 0)
                return false;

            currentCount--;
            return true;
        }
    }

    /// <summary>
    /// Blocks until a permit becomes available.
    /// </summary>
    public void Wait(CancellationToken cancellationToken = default)
    {
        WaitAsync(cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously waits for a permit to become available.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        WaitRequest request;

        lock (gate)
        {
            if (currentCount > 0)
            {
                currentCount--;
                return Task.CompletedTask;
            }

            request = new WaitRequest();
            waiters.Enqueue(request);
        }

        request.AttachCancellation(this, cancellationToken);
        return request.Task;
    }

    /// <summary>
    /// Releases one or more permits back to the semaphore.
    /// </summary>
    /// <returns>The number of waiters that were released.</returns>
    public int Release(int releaseCount = 1)
    {
        if (releaseCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(releaseCount));

        List<WaitRequest>? toWake = null;
        var released = 0;

        lock (gate)
        {
            while (releaseCount > 0)
            {
                if (waiters.Count > 0)
                {
                    WaitRequest? waiter = null;

                    while (waiters.Count > 0 && waiter is null)
                    {
                        var next = waiters.Dequeue();
                        if (next.TrySetCompleted())
                            waiter = next;
                    }

                    if (waiter != null)
                    {
                        toWake ??= new List<WaitRequest>();
                        toWake.Add(waiter);
                        released++;
                        releaseCount--;
                        continue;
                    }
                }

                if (currentCount >= maxCount)
                    throw new SemaphoreFullException();

                currentCount++;
                released++;
                releaseCount--;
            }
        }

        if (toWake != null)
        {
            foreach (var request in toWake)
                request.Complete();
        }

        return released;
    }

    private void CancelWaiter(WaitRequest request)
    {
        lock (gate)
        {
            request.TrySetCanceled();
        }
    }

    private sealed class WaitRequest
    {
        private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration cancellationRegistration;
        private int state; // 0 = pending, 1 = completed, 2 = canceled

        public Task Task => completion.Task;

        public void AttachCancellation(JobSemaphore owner, CancellationToken token)
        {
            if (!token.CanBeCanceled)
                return;

            cancellationRegistration = token.Register(static s =>
            {
                var tuple = (Tuple<JobSemaphore, WaitRequest>)s!;
                tuple.Item1.CancelWaiter(tuple.Item2);
            }, Tuple.Create(owner, this));
        }

        public bool TrySetCompleted()
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0)
                return false;

            cancellationRegistration.Dispose();
            return true;
        }

        public void Complete()
        {
            completion.TrySetResult(true);
        }

        public bool TrySetCanceled()
        {
            if (Interlocked.CompareExchange(ref state, 2, 0) != 0)
                return false;

            cancellationRegistration.Dispose();
            completion.TrySetCanceled();
            return true;
        }
    }
}