using System;
using System.Threading;

namespace DotGame.Core.Resources;

public abstract class ResourceHandle : IDisposable
{
    private int _refCount = 1;
    private int _status = (int)ResourceStatus.NotLoaded;
    private Exception? _exception;
    private bool _disposed;

    protected ResourceHandle(ResourceManager owner, ResourceKey key)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Key = key;
    }

    internal ResourceManager Owner { get; }

    internal ResourceKey Key { get; }

    public ResourceStatus Status => (ResourceStatus)Volatile.Read(ref _status);

    public Exception? Exception => _exception;

    public bool IsLoaded => Status == ResourceStatus.Loaded;

    public bool IsFaulted => Status == ResourceStatus.Faulted;

    public bool IsReleased => Status == ResourceStatus.Released;

    public void AddRef()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ResourceHandle));

        Interlocked.Increment(ref _refCount);
    }

    public void Dispose()
    {
        Release();
    }

    internal bool Release()
    {
        if (_disposed)
            return false;

        var newCount = Interlocked.Decrement(ref _refCount);
        if (newCount < 0)
            throw new InvalidOperationException("Resource handle reference count dropped below zero.");

        if (newCount == 0)
        {
            _disposed = true;
            if (Status == ResourceStatus.Loaded)
            {
                try
                {
                    OnDispose();
                }
                catch (Exception ex)
                {
                    Owner.RaiseUnhandledException(ex);
                }
            }

            SetStatus(ResourceStatus.Released);
            Owner.ForgetHandle(this);
            return true;
        }

        return false;
    }

    internal void SetLoading()
    {
        SetStatus(ResourceStatus.Loading);
    }

    internal void SetLoaded()
    {
        SetStatus(ResourceStatus.Loaded);
    }

    internal void SetFaulted(Exception exception)
    {
        _exception = exception;
        SetStatus(ResourceStatus.Faulted);
    }

    protected abstract void OnDispose();

    protected void SetStatus(ResourceStatus status)
    {
        Interlocked.Exchange(ref _status, (int)status);
    }
}

public enum ResourceStatus
{
    NotLoaded = 0,
    Loading = 1,
    Loaded = 2,
    Faulted = 3,
    Released = 4
}
