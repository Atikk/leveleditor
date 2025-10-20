using System;
using System.Collections.Generic;
using System.Linq;
using DotGame.Core.Logging;
using DotGame.Core.Timing;

namespace DotGame.Core.Memory;

public static class MemoryAllocatorDiagnosticsManager
{
    private static readonly object Gate = new();
    private static readonly Dictionary<IMemoryAllocatorDiagnosticsSource, Subscription> Subscriptions = new();
    private static TimeSpan minimumPublishInterval = TimeSpan.FromSeconds(1);

    public static event Action<IMemoryAllocatorDiagnosticsSource>? AllocatorRegistered;

    public static event Action<IMemoryAllocatorDiagnosticsSource>? AllocatorUnregistered;

    public static TimeSpan MinimumPublishInterval
    {
        get => minimumPublishInterval;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));
            minimumPublishInterval = value;
        }
    }

    public static void Register(IMemoryAllocatorDiagnosticsSource allocator)
    {
        if (allocator == null)
            throw new ArgumentNullException(nameof(allocator));

        var added = false;
        lock (Gate)
        {
            if (Subscriptions.ContainsKey(allocator))
                return;

            var subscription = new Subscription(allocator);
            allocator.MetricsUpdated += subscription.OnMetricsUpdated;
            Subscriptions.Add(allocator, subscription);
            added = true;
        }

        if (added)
            AllocatorRegistered?.Invoke(allocator);
    }

    public static void Unregister(IMemoryAllocatorDiagnosticsSource allocator)
    {
        if (allocator == null)
            return;

        Subscription? subscription;
        var removed = false;
        lock (Gate)
        {
            if (!Subscriptions.TryGetValue(allocator, out subscription))
                return;

            Subscriptions.Remove(allocator);
            removed = true;
        }

        allocator.MetricsUpdated -= subscription.OnMetricsUpdated;

        if (removed)
            AllocatorUnregistered?.Invoke(allocator);
    }

    public static IReadOnlyList<IMemoryAllocatorDiagnosticsSource> GetRegisteredAllocatorsSnapshot()
    {
        lock (Gate)
        {
            return Subscriptions.Keys.ToArray();
        }
    }

    private sealed class Subscription
    {
        private readonly IMemoryAllocatorDiagnosticsSource allocator;
        private readonly ILogger logger;
        private MemoryAllocatorMetricsSnapshot lastSnapshot;
        private DateTimeOffset lastPublishTime;
        private bool first = true;
        private readonly object gate = new();

        public Subscription(IMemoryAllocatorDiagnosticsSource allocator)
        {
            this.allocator = allocator;
            logger = LogManager.GetLogger($"Allocator.{allocator.Name}");
        }

        public void OnMetricsUpdated(MemoryAllocatorMetricsSnapshot snapshot)
        {
            lock (gate)
            {
                var now = TimeSource.Current.GetCurrentTime();
                if (!ShouldPublish(snapshot, now))
                    return;

                var hasNewFailure = !first && snapshot.FailedAllocations > lastSnapshot.FailedAllocations;
                var level = DetermineLevel(snapshot, hasNewFailure);
                var properties = BuildProperties(snapshot);
                var message = $"Allocator '{snapshot.Name}' usage {snapshot.CurrentUsageBytes}/{snapshot.CapacityBytes} bytes, outstanding blocks {snapshot.OutstandingBlocks}.";

                logger.Log(level, message, properties: properties);

                lastSnapshot = snapshot;
                lastPublishTime = now;
                first = false;
            }
        }

        private bool ShouldPublish(in MemoryAllocatorMetricsSnapshot snapshot, DateTimeOffset now)
        {
            if (first)
                return true;

            if (snapshot.FailedAllocations > lastSnapshot.FailedAllocations)
                return true;

            if (snapshot.PeakUsageBytes != lastSnapshot.PeakUsageBytes)
                return true;

            if (snapshot.OutstandingBlocks != lastSnapshot.OutstandingBlocks)
                return true;

            if (snapshot.ResetCount != lastSnapshot.ResetCount)
                return true;

            return now - lastPublishTime >= MinimumPublishInterval;
        }

        private static LogLevel DetermineLevel(in MemoryAllocatorMetricsSnapshot snapshot, bool hasNewFailure)
        {
            if (hasNewFailure || snapshot.FailedAllocations > 0 || snapshot.UsageRatio >= 1.0)
                return LogLevel.Error;

            if (snapshot.UsageRatio >= 0.9)
                return LogLevel.Warning;

            if (snapshot.UsageRatio >= 0.8)
                return LogLevel.Information;

            return LogLevel.Debug;
        }

        private static IReadOnlyDictionary<string, object?> BuildProperties(in MemoryAllocatorMetricsSnapshot snapshot)
        {
            return new Dictionary<string, object?>
            {
                ["allocator"] = snapshot.Name,
                ["kind"] = snapshot.Kind.ToString(),
                ["capacityBytes"] = snapshot.CapacityBytes,
                ["currentUsageBytes"] = snapshot.CurrentUsageBytes,
                ["peakUsageBytes"] = snapshot.PeakUsageBytes,
                ["freeBytes"] = snapshot.FreeBytes,
                ["largestFreeBlockBytes"] = snapshot.LargestFreeBlockBytes,
                ["fragmentedBytes"] = snapshot.FragmentedBytes,
                ["allocationCount"] = snapshot.AllocationCount,
                ["resetCount"] = snapshot.ResetCount,
                ["outstandingBlocks"] = snapshot.OutstandingBlocks,
                ["releasedBlocks"] = snapshot.ReleasedBlocks,
                ["failedAllocations"] = snapshot.FailedAllocations,
                ["lastAllocationBytes"] = snapshot.LastAllocationBytes,
                ["lastAllocationTimestamp"] = snapshot.LastAllocationTimestamp,
                ["lastResetTimestamp"] = snapshot.LastResetTimestamp,
                ["usageRatio"] = snapshot.UsageRatio,
                ["fragmentationRatio"] = snapshot.FragmentationRatio
            };
        }
    }
}
