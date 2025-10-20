using System;

namespace DotGame.Core.Memory;

/// <summary>
/// Common telemetry contract implemented by runtime memory allocators.
/// </summary>
public interface IMemoryAllocatorDiagnosticsSource
{
    /// <summary>
    /// Logical name for the allocator instance.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Allocator category (arena, stack, pool, etc.).
    /// </summary>
    MemoryAllocatorKind Kind { get; }

    /// <summary>
    /// Raised whenever the allocator produces an updated metrics snapshot.
    /// </summary>
    event Action<MemoryAllocatorMetricsSnapshot> MetricsUpdated;

    /// <summary>
    /// Returns the most recent metrics snapshot.
    /// </summary>
    MemoryAllocatorMetricsSnapshot GetMetricsSnapshot();
}
