namespace DotGame.Core.Memory;

/// <summary>
/// Identifies the type of memory allocator emitting telemetry.
/// </summary>
public enum MemoryAllocatorKind
{
    Arena,
    Stack,
    Pool
}
