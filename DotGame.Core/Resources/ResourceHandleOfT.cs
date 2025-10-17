using System;
using System.Collections.Generic;
using System.Threading;
using DotGame.Core.Async;

namespace DotGame.Core.Resources;

public sealed class ResourceHandle<T> : ResourceHandle
{
    private readonly object _stateLock = new();
    private readonly Func<CancellationToken, T> _loader;
    private readonly Action<T>? _onRelease;
    private readonly AsyncTaskScheduler _scheduler;
    private readonly List<Action<ResourceHandle<T>>> _continuations = new();
    private readonly List<Action<ResourceHandle<T>>> _failureHandlers = new();
    private int _started;
    private T? _value;

    internal ResourceHandle(ResourceManager owner, ResourceKey key, Func<CancellationToken, T> loader, AsyncTaskScheduler scheduler, Action<T>? onRelease)
        : base(owner, key)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _onRelease = onRelease;
    }

    public T Value
    {
        get
        {
            if (!IsLoaded)
                throw new InvalidOperationException($"Resource '{Key}' has not finished loading.");

            return _value!;
        }
    }

    public bool TryGetValue(out T value)
    {
        if (IsLoaded)
        {
            value = _value!;
            return true;
        }

        value = default!;
        return false;
    }

    public void OnCompleted(Action<ResourceHandle<T>> continuation)
    {
        if (continuation == null)
            return;

        var invokeNow = false;
        lock (_stateLock)
        {
            if (IsLoaded)
            {
                invokeNow = true;
            }
            else if (!IsFaulted)
            {
                _continuations.Add(continuation);
            }
        }

        if (invokeNow)
        {
            continuation(this);
        }
    }

    public void OnFailed(Action<ResourceHandle<T>> handler)
    {
        if (handler == null)
            return;

        var invokeNow = false;
        lock (_stateLock)
        {
            if (IsFaulted)
            {
                invokeNow = true;
            }
            else if (!IsLoaded)
            {
                _failureHandlers.Add(handler);
            }
        }

        if (invokeNow)
        {
            handler(this);
        }
    }

    internal void Start()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        SetLoading();
        _scheduler.Enqueue(
            cancellationToken => _loader(cancellationToken),
            OnLoadSucceeded,
            OnLoadFailed);
    }

    private void OnLoadSucceeded(T value)
    {
        _value = value;
        SetLoaded();

        List<Action<ResourceHandle<T>>> continuations;
        lock (_stateLock)
        {
            continuations = new List<Action<ResourceHandle<T>>>(_continuations);
            _continuations.Clear();
            _failureHandlers.Clear();
        }

        foreach (var continuation in continuations)
        {
            continuation(this);
        }
    }

    private void OnLoadFailed(Exception exception)
    {
        SetFaulted(exception);

        List<Action<ResourceHandle<T>>> handlers;
        lock (_stateLock)
        {
            handlers = new List<Action<ResourceHandle<T>>>(_failureHandlers);
            _continuations.Clear();
            _failureHandlers.Clear();
        }

        foreach (var handler in handlers)
        {
            handler(this);
        }
    }

    protected override void OnDispose()
    {
        if (_onRelease != null && _value is not null)
        {
            _onRelease(_value);
        }
    }
}
