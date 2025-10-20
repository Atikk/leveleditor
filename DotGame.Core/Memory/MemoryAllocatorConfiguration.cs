using System;

namespace DotGame.Core.Memory;

/// <summary>
/// Provides configuration knobs for the runtime allocator bundle and helpers for environment-driven bootstrapping.
/// </summary>
public sealed class MemoryAllocatorConfiguration
{
    public MemoryAllocatorConfiguration(
        ArenaOptions? arena = null,
        StackOptions? stack = null,
        PoolOptions? pool = null,
        bool autoRegisterDiagnostics = true)
    {
        Arena = (arena ?? ArenaOptions.Default).Validate();
        Stack = (stack ?? StackOptions.Default).Validate();
        Pool = (pool ?? PoolOptions.Default).Validate();
        AutoRegisterDiagnostics = autoRegisterDiagnostics;
    }

    public ArenaOptions Arena { get; }

    public StackOptions Stack { get; }

    public PoolOptions Pool { get; }

    public bool AutoRegisterDiagnostics { get; }

    public static MemoryAllocatorConfiguration Default { get; } = new();

    public static MemoryAllocatorConfiguration FromEnvironment(Func<string, string?>? getVariable = null)
    {
        getVariable ??= Environment.GetEnvironmentVariable;

        var arenaName = ReadString(getVariable, "DOTGAME_ALLOCATOR_ARENA_NAME", ArenaOptions.Default.Name);
        var arenaCapacityMb = ReadInt(getVariable, "DOTGAME_ALLOCATOR_ARENA_CAPACITY_MB", 32, 1, 4096);
        var arenaWarnThreshold = ReadDouble(getVariable, "DOTGAME_ALLOCATOR_ARENA_WARN_THRESHOLD", 0.90d, 0.50d, 0.999d);

        var stackName = ReadString(getVariable, "DOTGAME_ALLOCATOR_STACK_NAME", StackOptions.Default.Name);
        var stackCapacityKb = ReadInt(getVariable, "DOTGAME_ALLOCATOR_STACK_CAPACITY_KB", 2048, 64, 524_288);

        var poolName = ReadString(getVariable, "DOTGAME_ALLOCATOR_POOL_NAME", PoolOptions.Default.Name);
        var poolBlockSize = ReadInt(getVariable, "DOTGAME_ALLOCATOR_POOL_BLOCK_SIZE", PoolOptions.Default.BlockSizeBytes, 16, 1_048_576);
        var poolBlockCount = ReadInt(getVariable, "DOTGAME_ALLOCATOR_POOL_BLOCK_COUNT", PoolOptions.Default.BlockCount, 1, 65_536);

        var autoDiagnostics = ReadBool(getVariable, "DOTGAME_ALLOCATOR_AUTO_DIAGNOSTICS", defaultValue: true);

        var arenaOptions = new ArenaOptions
        {
            Name = arenaName,
            CapacityBytes = ClampToInt((long)arenaCapacityMb * 1024L * 1024L),
            WarningThreshold = arenaWarnThreshold
        };

        var stackOptions = new StackOptions
        {
            Name = stackName,
            CapacityBytes = ClampToInt((long)stackCapacityKb * 1024L)
        };

        var poolOptions = new PoolOptions
        {
            Name = poolName,
            BlockSizeBytes = poolBlockSize,
            BlockCount = poolBlockCount
        };

        return new MemoryAllocatorConfiguration(arenaOptions, stackOptions, poolOptions, autoDiagnostics);
    }

    public MemoryAllocatorSet CreateAllocators()
    {
        return MemoryAllocatorSet.Create(this);
    }

    private static int ReadInt(Func<string, string?> getVariable, string key, int defaultValue, int min, int max)
    {
        var raw = getVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        return Math.Clamp(int.TryParse(raw, out var parsed) ? parsed : defaultValue, min, max);
    }

    private static double ReadDouble(Func<string, string?> getVariable, string key, double defaultValue, double min, double max)
    {
        var raw = getVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (!double.TryParse(raw, out var parsed))
            parsed = defaultValue;

        if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            parsed = defaultValue;

        return Math.Clamp(parsed, min, max);
    }

    private static string ReadString(Func<string, string?> getVariable, string key, string defaultValue)
    {
        var raw = getVariable(key);
        return string.IsNullOrWhiteSpace(raw) ? defaultValue : raw.Trim();
    }

    private static bool ReadBool(Func<string, string?> getVariable, string key, bool defaultValue)
    {
        var raw = getVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        raw = raw.Trim();
        return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static int ClampToInt(long value)
    {
        return (int)Math.Clamp(value, 1, int.MaxValue);
    }

    public sealed record class ArenaOptions
    {
        public static ArenaOptions Default { get; } = new()
        {
            Name = "arena",
            CapacityBytes = 32 * 1024 * 1024,
            WarningThreshold = 0.90d
        };

        public string Name { get; init; } = "arena";

        public int CapacityBytes { get; init; } = 32 * 1024 * 1024;

        public double WarningThreshold { get; init; } = 0.90d;

        public ArenaOptions Validate()
        {
            if (CapacityBytes <= 0)
                throw new InvalidOperationException("Arena capacity must be greater than zero.");
            if (WarningThreshold <= 0 || WarningThreshold >= 1)
                throw new InvalidOperationException("Arena warning threshold must be between 0 and 1.");
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Arena allocator name must be provided.");
            return this with { Name = Name.Trim() };
        }
    }

    public sealed record class StackOptions
    {
        public static StackOptions Default { get; } = new()
        {
            Name = "stack",
            CapacityBytes = 2 * 1024 * 1024
        };

        public string Name { get; init; } = "stack";

        public int CapacityBytes { get; init; } = 2 * 1024 * 1024;

        public StackOptions Validate()
        {
            if (CapacityBytes <= 0)
                throw new InvalidOperationException("Stack allocator capacity must be greater than zero.");
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Stack allocator name must be provided.");
            return this with { Name = Name.Trim() };
        }
    }

    public sealed record class PoolOptions
    {
        public static PoolOptions Default { get; } = new()
        {
            Name = "pool",
            BlockSizeBytes = 4096,
            BlockCount = 512
        };

        public string Name { get; init; } = "pool";

        public int BlockSizeBytes { get; init; } = 4096;

        public int BlockCount { get; init; } = 512;

        public PoolOptions Validate()
        {
            if (BlockSizeBytes <= 0)
                throw new InvalidOperationException("Pool block size must be greater than zero.");
            if (BlockCount <= 0)
                throw new InvalidOperationException("Pool block count must be greater than zero.");
            if ((long)BlockSizeBytes * BlockCount > int.MaxValue)
                throw new InvalidOperationException("Pool capacity must fit within a 32-bit address space.");
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Pool allocator name must be provided.");
            return this with { Name = Name.Trim() };
        }
    }
}
