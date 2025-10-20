using System;

namespace DotGame.Core.Memory;

/// <summary>
/// Bundles the runtime allocators used by the engine and ensures shared disposal semantics.
/// </summary>
public sealed class MemoryAllocatorSet : IDisposable
{
    private bool disposed;

    private MemoryAllocatorSet(ArenaAllocator arena, StackAllocator stack, PoolAllocator pool)
    {
        Arena = arena;
        Stack = stack;
        Pool = pool;
    }

    public ArenaAllocator Arena { get; }

    public StackAllocator Stack { get; }

    public PoolAllocator Pool { get; }

    public static MemoryAllocatorSet Create(MemoryAllocatorConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        ArenaAllocator? arena = null;
        StackAllocator? stack = null;
        PoolAllocator? pool = null;

        try
        {
            arena = new ArenaAllocator(
                configuration.Arena.CapacityBytes,
                configuration.Arena.Name,
                configuration.Arena.WarningThreshold,
                autoRegisterDiagnostics: configuration.AutoRegisterDiagnostics);

            stack = new StackAllocator(
                configuration.Stack.CapacityBytes,
                configuration.Stack.Name,
                autoRegisterDiagnostics: configuration.AutoRegisterDiagnostics);

            pool = new PoolAllocator(
                configuration.Pool.BlockSizeBytes,
                configuration.Pool.BlockCount,
                configuration.Pool.Name,
                autoRegisterDiagnostics: configuration.AutoRegisterDiagnostics);

            return new MemoryAllocatorSet(arena, stack, pool);
        }
        catch
        {
            pool?.Dispose();
            stack?.Dispose();
            arena?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Pool.Dispose();
        Stack.Dispose();
        Arena.Dispose();
    }
}
