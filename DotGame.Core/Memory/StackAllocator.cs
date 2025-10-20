using System;
using System.Buffers;
using System.Collections.Generic;
using DotGame.Core.Logging;
using DotGame.Core.Timing;

namespace DotGame.Core.Memory;

public sealed class StackAllocator : IDisposable, IMemoryAllocatorDiagnosticsSource
{
    private readonly byte[] buffer;
    private readonly object gate = new();
    private readonly List<BlockInfo> blocks = new();
    private readonly ILogger logger;
    private readonly string name;
    private readonly int capacityBytes;

    private MetricsState metrics;
    private int offset;
    private bool disposed;

    public StackAllocator(int capacityBytes, string? name = null, ILogger? logger = null, bool autoRegisterDiagnostics = true)
    {
        if (capacityBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes));

        this.capacityBytes = capacityBytes;
        buffer = new byte[capacityBytes];
        this.name = string.IsNullOrWhiteSpace(name) ? $"Stack[{GetHashCode():x}]" : name;
        this.logger = logger ?? LogManager.GetLogger<StackAllocator>();
        metrics.LastResetTimestamp = TimeSource.Current.GetCurrentTime();

        if (autoRegisterDiagnostics)
            MemoryAllocatorDiagnosticsManager.Register(this);
    }

    public string Name => name;

    public MemoryAllocatorKind Kind => MemoryAllocatorKind.Stack;

    public int CapacityBytes => capacityBytes;

    public event Action<MemoryAllocatorMetricsSnapshot>? MetricsUpdated;

    public IMemoryOwner<byte> Allocate(int sizeBytes, int alignment = 16)
    {
        if (!TryAllocate(sizeBytes, out var owner, alignment))
            throw new InvalidOperationException($"Stack allocator '{name}' cannot satisfy allocation of {sizeBytes} bytes (capacity {capacityBytes} bytes).");

        return owner;
    }

    public bool TryAllocate(int sizeBytes, out IMemoryOwner<byte> owner, int alignment = 16)
    {
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            throw new ArgumentException("Alignment must be a power of two.", nameof(alignment));

        MemoryAllocatorMetricsSnapshot snapshot;
        StackBlock? block = null;
        bool success;
        long currentUsageBytes;

        lock (gate)
        {
            EnsureNotDisposed();

            var alignedOffset = Align(offset, alignment);
            var end = alignedOffset + sizeBytes;
            if (end > capacityBytes)
            {
                metrics.FailedAllocations++;
                snapshot = CreateSnapshotUnsafe();
                success = false;
                currentUsageBytes = metrics.LiveBytes;
            }
            else
            {
                var info = new BlockInfo(alignedOffset, end, sizeBytes);
                blocks.Add(info);
                offset = end;

                metrics.TotalAllocatedBytes += sizeBytes;
                metrics.LiveBytes += sizeBytes;
                metrics.PeakUsageBytes = Math.Max(metrics.PeakUsageBytes, metrics.LiveBytes);
                metrics.AllocationCount++;
                metrics.OutstandingBlocks++;
                metrics.LastAllocationBytes = sizeBytes;
                metrics.LastAllocationTimestamp = TimeSource.Current.GetCurrentTime();

                var memory = new Memory<byte>(buffer, alignedOffset, sizeBytes);
                block = new StackBlock(this, info, memory);

                snapshot = CreateSnapshotUnsafe();
                success = true;
                currentUsageBytes = metrics.LiveBytes;
            }
        }

        MetricsUpdated?.Invoke(snapshot);

        if (!success)
        {
            logger.Error($"Stack allocator '{name}' exceeded capacity while allocating {sizeBytes} bytes (usage {currentUsageBytes}/{capacityBytes} bytes).");
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

            blocks.Clear();
            offset = 0;
            metrics.LiveBytes = 0;
            metrics.OutstandingBlocks = 0;
            metrics.ResetCount++;
            metrics.LastResetTimestamp = TimeSource.Current.GetCurrentTime();

            logger.Debug($"Stack allocator '{name}' reset (clear={clearBuffer}).");
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
            blocks.Clear();
        }

        MetricsUpdated?.Invoke(snapshot);
        MemoryAllocatorDiagnosticsManager.Unregister(this);
    }

    private void OnBlockReleased(BlockInfo info)
    {
        MemoryAllocatorMetricsSnapshot snapshot;

        lock (gate)
        {
            if (disposed)
                return;

            if (info.IsActive)
            {
                info.IsActive = false;
                metrics.OutstandingBlocks = Math.Max(0, metrics.OutstandingBlocks - 1);
                metrics.ReleasedBlocks++;
                metrics.LiveBytes = Math.Max(0, metrics.LiveBytes - info.SizeBytes);
                DrainInactiveTailUnsafe();
            }

            snapshot = CreateSnapshotUnsafe();
        }

        MetricsUpdated?.Invoke(snapshot);
    }

    private void DrainInactiveTailUnsafe()
    {
        while (blocks.Count > 0 && !blocks[^1].IsActive)
        {
            offset = blocks[^1].StartOffset;
            blocks.RemoveAt(blocks.Count - 1);
        }

        if (blocks.Count > 0)
            offset = blocks[^1].EndOffset;
        else
            offset = 0;
    }

    private MemoryAllocatorMetricsSnapshot CreateSnapshotUnsafe()
    {
        var freeBytes = capacityBytes - metrics.LiveBytes;
        var largestFreeBlockBytes = Math.Max(0, capacityBytes - offset);
        var fragmentedBytes = Math.Max(0, freeBytes - largestFreeBlockBytes);

        return new MemoryAllocatorMetricsSnapshot(
            name,
            MemoryAllocatorKind.Stack,
            capacityBytes,
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

    private static int Align(int value, int alignment)
    {
        var mask = alignment - 1;
        return (value + mask) & ~mask;
    }

    private sealed class StackBlock : IMemoryOwner<byte>
    {
        private readonly StackAllocator allocator;
        private readonly BlockInfo blockInfo;
        private readonly Memory<byte> memory;
        private bool disposed;

        public StackBlock(StackAllocator allocator, BlockInfo blockInfo, Memory<byte> memory)
        {
            this.allocator = allocator;
            this.blockInfo = blockInfo;
            this.memory = memory;
        }

        public Memory<byte> Memory => memory;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            allocator.OnBlockReleased(blockInfo);
        }
    }

    private sealed class BlockInfo
    {
        public BlockInfo(int startOffset, int endOffset, int sizeBytes)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            SizeBytes = sizeBytes;
            IsActive = true;
        }

        public int StartOffset { get; }
        public int EndOffset { get; }
        public int SizeBytes { get; }
        public bool IsActive { get; set; }
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
