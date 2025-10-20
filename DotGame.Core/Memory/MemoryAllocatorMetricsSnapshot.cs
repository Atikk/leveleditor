using System;

namespace DotGame.Core.Memory;

/// <summary>
/// Snapshot of allocator usage emitted to telemetry collectors.
/// </summary>
public readonly struct MemoryAllocatorMetricsSnapshot
{
    public MemoryAllocatorMetricsSnapshot(
        string name,
        MemoryAllocatorKind kind,
        long capacityBytes,
        long totalAllocatedBytes,
        long currentUsageBytes,
        long peakUsageBytes,
        long freeBytes,
        long largestFreeBlockBytes,
        long fragmentedBytes,
        int allocationCount,
        int resetCount,
        int outstandingBlocks,
        int releasedBlocks,
        int failedAllocations,
        int lastAllocationBytes,
        DateTimeOffset lastAllocationTimestamp,
        DateTimeOffset lastResetTimestamp)
    {
        Name = name;
        Kind = kind;
        CapacityBytes = capacityBytes;
        TotalAllocatedBytes = totalAllocatedBytes;
        CurrentUsageBytes = currentUsageBytes;
        PeakUsageBytes = peakUsageBytes;
        FreeBytes = freeBytes;
        LargestFreeBlockBytes = largestFreeBlockBytes;
        FragmentedBytes = fragmentedBytes;
        AllocationCount = allocationCount;
        ResetCount = resetCount;
        OutstandingBlocks = outstandingBlocks;
        ReleasedBlocks = releasedBlocks;
        FailedAllocations = failedAllocations;
        LastAllocationBytes = lastAllocationBytes;
        LastAllocationTimestamp = lastAllocationTimestamp;
        LastResetTimestamp = lastResetTimestamp;
    }

    public string Name { get; }

    public MemoryAllocatorKind Kind { get; }

    public long CapacityBytes { get; }

    public long TotalAllocatedBytes { get; }

    public long CurrentUsageBytes { get; }

    public long PeakUsageBytes { get; }

    public long FreeBytes { get; }

    public long LargestFreeBlockBytes { get; }

    public long FragmentedBytes { get; }

    public int AllocationCount { get; }

    public int ResetCount { get; }

    public int OutstandingBlocks { get; }

    public int ReleasedBlocks { get; }

    public int FailedAllocations { get; }

    public int LastAllocationBytes { get; }

    public DateTimeOffset LastAllocationTimestamp { get; }

    public DateTimeOffset LastResetTimestamp { get; }

    public double UsageRatio => CapacityBytes <= 0 ? 0 : (double)CurrentUsageBytes / CapacityBytes;

    public double FreeRatio => CapacityBytes <= 0 ? 0 : (double)FreeBytes / CapacityBytes;

    public double FragmentationRatio => CapacityBytes <= 0 ? 0 : (double)FragmentedBytes / CapacityBytes;
}
