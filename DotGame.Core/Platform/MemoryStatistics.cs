namespace DotGame.Core.Platform;

public readonly struct MemoryStatistics
{
    public MemoryStatistics(ulong workingSetBytes, ulong privateBytes, ulong managedHeapBytes)
    {
        WorkingSetBytes = workingSetBytes;
        PrivateBytes = privateBytes;
        ManagedHeapBytes = managedHeapBytes;
    }

    public ulong WorkingSetBytes { get; }

    public ulong PrivateBytes { get; }

    public ulong ManagedHeapBytes { get; }
}
