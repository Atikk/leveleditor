using System;
using System.Buffers;
using System.Collections.Generic;
using DotGame.Core.Logging;
using DotGame.Core.Timing;

namespace DotGame.Core.Memory;

public sealed class PoolAllocator : IDisposable, IMemoryAllocatorDiagnosticsSource
{
    private readonly byte[] buffer;
    private readonly Stack<int> freeBlockIndices;
    private readonly bool[] leasedBlocks;
    private readonly object gate = new();
    private readonly ILogger logger;
    private readonly string name;

    private MetricsState metrics;
    private bool disposed;

    public PoolAllocator(int blockSizeBytes, int blockCount, string? name = null, ILogger? logger = null, bool autoRegisterDiagnostics = true)
    {
        if (blockSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSizeBytes));
        if (blockCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockCount));

        BlockSizeBytes = blockSizeBytes;
        BlockCount = blockCount;
        CapacityBytes = blockSizeBytes * blockCount;
        buffer = new byte[CapacityBytes];
        leasedBlocks = new bool[blockCount];
        freeBlockIndices = new Stack<int>(blockCount);
        for (var i = blockCount - 1; i >= 0; i--)
            freeBlockIndices.Push(i);

        this.name = string.IsNullOrWhiteSpace(name) ? $"Pool[{blockSizeBytes}x{blockCount}]" : name;
        this.logger = logger ?? LogManager.GetLogger<PoolAllocator>();
        metrics.LastResetTimestamp = TimeSource.Current.GetCurrentTime();

        if (autoRegisterDiagnostics)
            MemoryAllocatorDiagnosticsManager.Register(this);
    }

    public string Name => name;

    public MemoryAllocatorKind Kind => MemoryAllocatorKind.Pool;

    public int CapacityBytes { get; }

    public int BlockSizeBytes { get; }

    public int BlockCount { get; }

    public event Action<MemoryAllocatorMetricsSnapshot>? MetricsUpdated;

    public IMemoryOwner<byte> Allocate(int sizeBytes)
    {
        if (!TryAllocate(sizeBytes, out var owner))
            throw new InvalidOperationException($"Pool allocator '{name}' cannot satisfy allocation of {sizeBytes} bytes (block size {BlockSizeBytes}, free blocks {freeBlockIndices.Count}).");

        return owner;
    }

    public bool TryAllocate(int sizeBytes, out IMemoryOwner<byte> owner)
    {
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        MemoryAllocatorMetricsSnapshot snapshot;
        bool success;
        PoolBlock? block = null;
        long liveBytes;

        lock (gate)
        {
            EnsureNotDisposed();

            if (sizeBytes > BlockSizeBytes || freeBlockIndices.Count == 0)
            {
                metrics.FailedAllocations++;
                snapshot = CreateSnapshotUnsafe();
                success = false;
                liveBytes = metrics.LiveBytes;
            }
            else
            {
                var blockIndex = freeBlockIndices.Pop();
                leasedBlocks[blockIndex] = true;
                var offset = blockIndex * BlockSizeBytes;
                var memory = new Memory<byte>(buffer, offset, BlockSizeBytes);
                block = new PoolBlock(this, blockIndex, memory);

                metrics.TotalAllocatedBytes += BlockSizeBytes;
                metrics.LiveBytes += BlockSizeBytes;
                metrics.PeakUsageBytes = Math.Max(metrics.PeakUsageBytes, metrics.LiveBytes);
                metrics.AllocationCount++;
                metrics.OutstandingBlocks++;
                metrics.LastAllocationBytes = sizeBytes;
                metrics.LastAllocationTimestamp = TimeSource.Current.GetCurrentTime();

                snapshot = CreateSnapshotUnsafe();
                success = true;
                liveBytes = metrics.LiveBytes;
            }
        }

        MetricsUpdated?.Invoke(snapshot);

        if (!success)
        {
            logger.Error($"Pool allocator '{name}' failed allocation: requested {sizeBytes} bytes, block size {BlockSizeBytes}, free blocks {freeBlockIndices.Count}.");
            owner = default!;
            return false;
        }

        owner = block!;
        return true;
    }

    public MemoryAllocatorMetricsSnapshot GetMetricsSnapshot()
    {
        lock (gate)
        {
            return CreateSnapshotUnsafe();
        }
    }

    public void Reset(bool clearBuffer = false)
    {
        MemoryAllocatorMetricsSnapshot snapshot;

        lock (gate)
        {
            EnsureNotDisposed();

            if (clearBuffer)
                Array.Clear(buffer, 0, buffer.Length);

            Array.Clear(leasedBlocks, 0, leasedBlocks.Length);
            freeBlockIndices.Clear();
            for (var i = BlockCount - 1; i >= 0; i--)
                freeBlockIndices.Push(i);

            metrics.LiveBytes = 0;
            metrics.OutstandingBlocks = 0;
            metrics.ResetCount++;
            metrics.LastResetTimestamp = TimeSource.Current.GetCurrentTime();

            logger.Debug($"Pool allocator '{name}' reset (clear={clearBuffer}).");
            snapshot = CreateSnapshotUnsafe();
        }

        MetricsUpdated?.Invoke(snapshot);
    }

    public void Dispose()
    {
        MemoryAllocatorMetricsSnapshot snapshot;

        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            snapshot = CreateSnapshotUnsafe();
        }

        MetricsUpdated?.Invoke(snapshot);
        MemoryAllocatorDiagnosticsManager.Unregister(this);
    }

    private void OnBlockReleased(int blockIndex)
    {
        MemoryAllocatorMetricsSnapshot snapshot;

        lock (gate)
        {
            if (disposed)
                return;

            if (!leasedBlocks[blockIndex])
                return;

            leasedBlocks[blockIndex] = false;
            freeBlockIndices.Push(blockIndex);
            metrics.OutstandingBlocks = Math.Max(0, metrics.OutstandingBlocks - 1);
            metrics.ReleasedBlocks++;
            metrics.LiveBytes = Math.Max(0, metrics.LiveBytes - BlockSizeBytes);
            snapshot = CreateSnapshotUnsafe();
        }

        MetricsUpdated?.Invoke(snapshot);
    }

    private MemoryAllocatorMetricsSnapshot CreateSnapshotUnsafe()
    {
        var freeBlocks = freeBlockIndices.Count;
        var freeBytes = (long)freeBlocks * BlockSizeBytes;
        var largestFreeBlockBytes = freeBlocks > 0 ? BlockSizeBytes : 0;
        var fragmentedBytes = Math.Max(0, freeBytes - largestFreeBlockBytes);

        return new MemoryAllocatorMetricsSnapshot(
            name,
            MemoryAllocatorKind.Pool,
            CapacityBytes,
            metrics.TotalAllocatedBytes,
            metrics.LiveBytes,
            metrics.PeakUsageBytes,
            freeBytes,
            largestFreeBlockBytes,
            fragmentedBytes,
            metrics.AllocationCount,
            metrics.ResetCount,
            metrics.OutstandingBlocks,
            metrics.ReleasedBlocks,
            metrics.FailedAllocations,
            metrics.LastAllocationBytes,
            metrics.LastAllocationTimestamp,
            metrics.LastResetTimestamp);
    }

    private void EnsureNotDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(name);
    }

    private sealed class PoolBlock : IMemoryOwner<byte>
    {
        private readonly PoolAllocator allocator;
        private readonly int index;
        private readonly Memory<byte> memory;
        private bool disposed;

        public PoolBlock(PoolAllocator allocator, int index, Memory<byte> memory)
        {
            this.allocator = allocator;
            this.index = index;
            this.memory = memory;
        }

        public Memory<byte> Memory => memory;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            allocator.OnBlockReleased(index);
        }
    }

    private struct MetricsState
    {
        public long TotalAllocatedBytes;
        public long LiveBytes;
        public long PeakUsageBytes;
        public int AllocationCount;
        public int ResetCount;
        public int OutstandingBlocks;
        public int ReleasedBlocks;
        public int FailedAllocations;
        public int LastAllocationBytes;
        public DateTimeOffset LastAllocationTimestamp;
        public DateTimeOffset LastResetTimestamp;
    }
}
