using System;
using System.Buffers;
using DotGame.Core.Logging;
using DotGame.Core.Timing;

namespace DotGame.Core.Memory;

public sealed class ArenaAllocator : IDisposable, IMemoryAllocatorDiagnosticsSource
{
    private static readonly double[] UsageThresholds = { 0.80d, 0.90d, 1.00d };

    private readonly byte[] buffer;
    private readonly object gate = new();
    private readonly ILogger logger;
    private readonly string name;
    private readonly double warningThreshold;

    private MetricsState metrics;
    private int offset;
    private int lastThresholdIndex = -1;
    private bool disposed;

    public ArenaAllocator(int capacityBytes, string? name = null, double warningThreshold = 0.90d, ILogger? logger = null, bool autoRegisterDiagnostics = true)
    {
        if (capacityBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        if (warningThreshold <= 0 || warningThreshold > 1)
            throw new ArgumentOutOfRangeException(nameof(warningThreshold));

        CapacityBytes = capacityBytes;
        buffer = new byte[capacityBytes];
        this.warningThreshold = warningThreshold;
        this.name = string.IsNullOrWhiteSpace(name) ? $"Arena[{GetHashCode():x}]" : name;
        this.logger = logger ?? LogManager.GetLogger<ArenaAllocator>();

        metrics.LastResetTimestamp = TimeSource.Current.GetCurrentTime();

        if (autoRegisterDiagnostics)
            MemoryAllocatorDiagnosticsManager.Register(this);
    }

    public int CapacityBytes { get; }

    public string Name => name;

    public MemoryAllocatorKind Kind => MemoryAllocatorKind.Arena;

    public event Action<MemoryAllocatorMetricsSnapshot>? MetricsUpdated;

    public IMemoryOwner<byte> Allocate(int sizeBytes, int alignment = 8)
    {
        if (!TryAllocate(sizeBytes, out var owner, alignment))
            throw new InvalidOperationException($"Arena '{name}' cannot satisfy allocation of {sizeBytes} bytes (capacity {CapacityBytes} bytes).");

        return owner;
    }

    public bool TryAllocate(int sizeBytes, out IMemoryOwner<byte> owner, int alignment = 8)
    {
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            throw new ArgumentException("Alignment must be a power of two.", nameof(alignment));

    MemoryAllocatorMetricsSnapshot snapshot;
    IMemoryOwner<byte> localOwner = default!;
    bool success;
    long currentUsageBytes;

        lock (gate)
        {
            EnsureNotDisposed();

            var alignedOffset = Align(offset, alignment);
            var end = alignedOffset + sizeBytes;
            if (end > CapacityBytes)
            {
                metrics.FailedAllocations++;
                snapshot = CreateSnapshotUnsafe();
                success = false;
                currentUsageBytes = metrics.LiveBytes;
            }
            else
            {
                offset = end;
                metrics.TotalAllocatedBytes += sizeBytes;
                metrics.LiveBytes += sizeBytes;
                metrics.PeakUsageBytes = Math.Max(metrics.PeakUsageBytes, metrics.LiveBytes);
                metrics.AllocationCount++;
                metrics.OutstandingBlocks++;
                metrics.LastAllocationBytes = sizeBytes;
                metrics.LastAllocationTimestamp = TimeSource.Current.GetCurrentTime();

                var memory = new Memory<byte>(buffer, alignedOffset, sizeBytes);
                localOwner = new ArenaBlock(this, memory, sizeBytes);

                EvaluateUsageThresholdsUnsafe();
                snapshot = CreateSnapshotUnsafe();
                success = true;
                currentUsageBytes = metrics.LiveBytes;
            }
        }

        MetricsUpdated?.Invoke(snapshot);

        if (!success)
        {
            logger.Error($"Arena '{name}' exceeded capacity while allocating {sizeBytes} bytes (usage {currentUsageBytes}/{CapacityBytes} bytes).");
            owner = default!;
            return false;
        }

        owner = localOwner;
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

            offset = 0;
            metrics.LiveBytes = 0;
            metrics.OutstandingBlocks = 0;
            metrics.ResetCount++;
            metrics.LastResetTimestamp = TimeSource.Current.GetCurrentTime();
            lastThresholdIndex = -1;

            logger.Debug($"Arena '{name}' reset (clear={clearBuffer}).");
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

    private void OnBlockReleased(int sizeBytes)
    {
        MemoryAllocatorMetricsSnapshot snapshot;

        lock (gate)
        {
            if (disposed)
                return;

            metrics.OutstandingBlocks = Math.Max(0, metrics.OutstandingBlocks - 1);
            metrics.ReleasedBlocks++;
            metrics.LiveBytes = Math.Max(0, metrics.LiveBytes - sizeBytes);
            snapshot = CreateSnapshotUnsafe();
        }

        MetricsUpdated?.Invoke(snapshot);
    }

    private void EnsureNotDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(name);
    }

    private void EvaluateUsageThresholdsUnsafe()
    {
    var usageRatio = CapacityBytes == 0 ? 0 : (double)metrics.LiveBytes / CapacityBytes;

        var threshold = -1;
        for (var i = 0; i < UsageThresholds.Length; i++)
        {
            if (usageRatio >= UsageThresholds[i])
                threshold = i;
        }

        if (threshold > lastThresholdIndex)
        {
            lastThresholdIndex = threshold;
            if (threshold >= 0)
                EmitUsageAlert(usageRatio, threshold);
        }
        else if (threshold < lastThresholdIndex && usageRatio < warningThreshold)
        {
            lastThresholdIndex = -1;
        }
    }

    private void EmitUsageAlert(double usageRatio, int thresholdIndex)
    {
        var percent = usageRatio * 100.0;
        switch (thresholdIndex)
        {
            case >= 2:
                logger.Error($"Arena '{name}' saturated ({percent:F1}% of capacity used).");
                break;
            case 1:
                logger.Warn($"Arena '{name}' usage high ({percent:F1}% of capacity used).");
                break;
            default:
                logger.Info($"Arena '{name}' usage at {percent:F1}% of capacity.");
                break;
        }
    }

    private MemoryAllocatorMetricsSnapshot CreateSnapshotUnsafe()
    {
        var freeBytes = CapacityBytes - metrics.LiveBytes;
        var largestFreeBlockBytes = Math.Max(0, CapacityBytes - offset);
        var fragmentedBytes = Math.Max(0, freeBytes - largestFreeBlockBytes);

        return new MemoryAllocatorMetricsSnapshot(
            name,
            MemoryAllocatorKind.Arena,
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

    private static int Align(int value, int alignment)
    {
        var mask = alignment - 1;
        return (value + mask) & ~mask;
    }

    private sealed class ArenaBlock : IMemoryOwner<byte>
    {
        private readonly ArenaAllocator allocator;
        private readonly Memory<byte> memory;
        private readonly int sizeBytes;
        private bool disposed;

        public ArenaBlock(ArenaAllocator allocator, Memory<byte> memory, int sizeBytes)
        {
            this.allocator = allocator;
            this.memory = memory;
            this.sizeBytes = sizeBytes;
        }

        public Memory<byte> Memory => memory;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            allocator.OnBlockReleased(sizeBytes);
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
