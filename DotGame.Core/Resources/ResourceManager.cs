using System;
using System.Collections.Concurrent;
using System.Threading;
using DotGame.Core.Async;

namespace DotGame.Core.Resources;

public sealed class ResourceManager : IDisposable
{
    private readonly AsyncTaskScheduler _scheduler;
    private readonly ConcurrentDictionary<ResourceKey, ResourceHandle> _handles = new();
    private bool _disposed;

    public ResourceManager(AsyncTaskScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _scheduler.UnhandledException += OnSchedulerUnhandledException;
    }

    public AsyncTaskScheduler Scheduler => _scheduler;

    public event Action<Exception>? UnhandledException;

    public ResourceHandle<T> LoadAsync<T>(string key, Func<CancellationToken, T> loader, Action<T>? onRelease = null, Action<ResourceHandle<T>>? onCompleted = null, Action<ResourceHandle<T>>? onFailed = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Resource key must be provided.", nameof(key));
        if (loader == null)
            throw new ArgumentNullException(nameof(loader));

        ThrowIfDisposed();

        var resourceKey = new ResourceKey(typeof(T), key);
        var handle = (ResourceHandle<T>)_handles.GetOrAdd(resourceKey, static (rk, state) =>
        {
            var tuple = ((Func<CancellationToken, T> loader, Action<T>? release, ResourceManager owner))state;
            return new ResourceHandle<T>(tuple.owner, rk, tuple.loader, tuple.owner._scheduler, tuple.release);
        }, (loader, onRelease, this));

        handle.AddRef();

        if (onCompleted != null)
            handle.OnCompleted(onCompleted);

        if (onFailed != null)
            handle.OnFailed(onFailed);

        handle.Start();
        return handle;
    }

    public bool TryGetHandle<T>(string key, out ResourceHandle<T>? handle)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Resource key must be provided.", nameof(key));

        ThrowIfDisposed();

        var resourceKey = new ResourceKey(typeof(T), key);
        if (_handles.TryGetValue(resourceKey, out var baseHandle) && baseHandle is ResourceHandle<T> typed)
        {
            typed.AddRef();
            handle = typed;
            return true;
        }

        handle = null;
        return false;
    }

    public void Release(ResourceHandle handle)
    {
        if (handle == null)
            return;

        handle.Release();
    }

    public void PumpMainThread()
    {
        _scheduler.PumpMainThread();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _scheduler.UnhandledException -= OnSchedulerUnhandledException;

        foreach (var handle in _handles.Values)
        {
            handle.Release();
        }

        _handles.Clear();
    }

    internal void ForgetHandle(ResourceHandle handle)
    {
        _handles.TryRemove(handle.Key, out _);
    }

    internal void RaiseUnhandledException(Exception exception)
    {
        UnhandledException?.Invoke(exception);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ResourceManager));
    }

    private void OnSchedulerUnhandledException(Exception exception)
    {
        UnhandledException?.Invoke(exception);
    }
}
