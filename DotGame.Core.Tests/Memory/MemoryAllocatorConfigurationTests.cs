using System;
using System.Collections.Generic;
using DotGame.Core.Memory;
using Xunit;

namespace DotGame.Core.Tests.Memory;

public sealed class MemoryAllocatorConfigurationTests
{
    [Fact]
    public void FromEnvironment_AppliesOverrides()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTGAME_ALLOCATOR_ARENA_NAME"] = "arena_test",
            ["DOTGAME_ALLOCATOR_ARENA_CAPACITY_MB"] = "64",
            ["DOTGAME_ALLOCATOR_ARENA_WARN_THRESHOLD"] = "0.95",
            ["DOTGAME_ALLOCATOR_STACK_NAME"] = "frame_stack",
            ["DOTGAME_ALLOCATOR_STACK_CAPACITY_KB"] = "8192",
            ["DOTGAME_ALLOCATOR_POOL_NAME"] = "pool_test",
            ["DOTGAME_ALLOCATOR_POOL_BLOCK_SIZE"] = "1024",
            ["DOTGAME_ALLOCATOR_POOL_BLOCK_COUNT"] = "2048",
            ["DOTGAME_ALLOCATOR_AUTO_DIAGNOSTICS"] = "false"
        };

        var configuration = MemoryAllocatorConfiguration.FromEnvironment(key =>
            variables.TryGetValue(key, out var value) ? value : null);

        Assert.Equal("arena_test", configuration.Arena.Name);
        Assert.Equal(64 * 1024 * 1024, configuration.Arena.CapacityBytes);
        Assert.Equal(0.95d, configuration.Arena.WarningThreshold, 3);
        Assert.Equal("frame_stack", configuration.Stack.Name);
        Assert.Equal(8192 * 1024, configuration.Stack.CapacityBytes);
        Assert.Equal("pool_test", configuration.Pool.Name);
        Assert.Equal(1024, configuration.Pool.BlockSizeBytes);
        Assert.Equal(2048, configuration.Pool.BlockCount);
        Assert.False(configuration.AutoRegisterDiagnostics);
    }

    [Fact]
    public void FromEnvironment_ClampsOutOfRangeValues()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTGAME_ALLOCATOR_ARENA_CAPACITY_MB"] = "-5",
            ["DOTGAME_ALLOCATOR_ARENA_WARN_THRESHOLD"] = "2.5",
            ["DOTGAME_ALLOCATOR_STACK_CAPACITY_KB"] = "0",
            ["DOTGAME_ALLOCATOR_POOL_BLOCK_SIZE"] = "-10",
            ["DOTGAME_ALLOCATOR_POOL_BLOCK_COUNT"] = "0"
        };

        var configuration = MemoryAllocatorConfiguration.FromEnvironment(key =>
            variables.TryGetValue(key, out var value) ? value : null);

        // Defaults enforce minimum positive values even when overrides are invalid.
        Assert.Equal(1 * 1024 * 1024, configuration.Arena.CapacityBytes);
        Assert.Equal(0.999d, configuration.Arena.WarningThreshold, 3);
        Assert.Equal(64 * 1024, configuration.Stack.CapacityBytes);
        Assert.Equal(16, configuration.Pool.BlockSizeBytes);
        Assert.Equal(1, configuration.Pool.BlockCount);
    }
}
