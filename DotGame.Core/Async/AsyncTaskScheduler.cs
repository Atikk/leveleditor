using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DotGame.Core.Async;

public sealed class AsyncTaskScheduler : IDisposable
{
    private readonly BlockingCollection<ScheduledWork> _workQueue;
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly List<Thread> _workers = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _disposeLock = new();
    private volatile bool _disposed;

    public AsyncTaskScheduler(int workerCount = 1, string workerNamePrefix = "AsyncWorker")
    {
        if (workerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be positive.");

        _workQueue = new BlockingCollection<ScheduledWork>(new ConcurrentQueue<ScheduledWork>());

        for (var i = 0; i < workerCount; i++)
        {
            var thread = new Thread(RunWorker)
            {
                IsBackground = true,
                Name = workerNamePrefix + i
            };
            _workers.Add(thread);
            thread.Start();
        }
    }

    public event Action<Exception>? UnhandledException;

    public int WorkerCount => _workers.Count;

    public void Enqueue(Action<CancellationToken> work, Action? onCompleted = null, Action<Exception>? onError = null)
    {
        if (work == null)
            throw new ArgumentNullException(nameof(work));

        EnqueueInternal(new ActionScheduledWork(work, onCompleted, onError, this));
    }

    public void Enqueue<T>(Func<CancellationToken, T> work, Action<T>? onCompleted = null, Action<Exception>? onError = null)
    {
        if (work == null)
            throw new ArgumentNullException(nameof(work));

        EnqueueInternal(new FuncScheduledWork<T>(work, onCompleted, onError, this));
    }

    public void PumpMainThread()
    {
        ThrowIfDisposed();

        while (_mainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                UnhandledException?.Invoke(ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _cts.Cancel();
        _workQueue.CompleteAdding();

        foreach (var worker in _workers)
        {
            try
            {
                worker.Join();
            }
            catch
            {
                // ignored
            }
        }

        _workQueue.Dispose();
        _cts.Dispose();
    }

    internal void QueueMainThreadAction(Action action)
    {
        if (action == null)
            return;

        _mainThreadActions.Enqueue(action);
    }

    private void EnqueueInternal(ScheduledWork work)
    {
        ThrowIfDisposed();
        _workQueue.Add(work);
    }

    private void RunWorker()
    {
        try
        {
            foreach (var work in _workQueue.GetConsumingEnumerable(_cts.Token))
            {
                work.Execute(_cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception ex)
        {
            UnhandledException?.Invoke(ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncTaskScheduler));
    }

    private abstract class ScheduledWork
    {
        protected ScheduledWork(AsyncTaskScheduler owner)
        {
            Owner = owner;
        }

        protected AsyncTaskScheduler Owner { get; }

        public abstract void Execute(CancellationToken cancellationToken);
    }

    private sealed class ActionScheduledWork : ScheduledWork
    {
        private readonly Action<CancellationToken> _work;
        private readonly Action? _onCompleted;
        private readonly Action<Exception>? _onError;

        public ActionScheduledWork(Action<CancellationToken> work, Action? onCompleted, Action<Exception>? onError, AsyncTaskScheduler owner)
            : base(owner)
        {
            _work = work;
            _onCompleted = onCompleted;
            _onError = onError;
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            try
            {
                _work(cancellationToken);

                if (_onCompleted != null)
                {
                    Owner.QueueMainThreadAction(_onCompleted);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // ignore cancellation
            }
            catch (Exception ex)
            {
                QueueError(ex);
            }
        }

        private void QueueError(Exception ex)
        {
            if (_onError != null)
            {
                Owner.QueueMainThreadAction(() => _onError(ex));
            }
            else
            {
                Owner.QueueMainThreadAction(() => Owner.UnhandledException?.Invoke(ex));
            }
        }
    }

    private sealed class FuncScheduledWork<T> : ScheduledWork
    {
        private readonly Func<CancellationToken, T> _work;
        private readonly Action<T>? _onCompleted;
        private readonly Action<Exception>? _onError;

        public FuncScheduledWork(Func<CancellationToken, T> work, Action<T>? onCompleted, Action<Exception>? onError, AsyncTaskScheduler owner)
            : base(owner)
        {
            _work = work;
            _onCompleted = onCompleted;
            _onError = onError;
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            try
            {
                var result = _work(cancellationToken);

                if (_onCompleted != null)
                {
                    Owner.QueueMainThreadAction(() => _onCompleted(result));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // ignore cancellation
            }
            catch (Exception ex)
            {
                if (_onError != null)
                {
                    Owner.QueueMainThreadAction(() => _onError(ex));
                }
                else
                {
                    Owner.QueueMainThreadAction(() => Owner.UnhandledException?.Invoke(ex));
                }
            }
        }
    }
}
