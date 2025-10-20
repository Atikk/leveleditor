using System;
using DotGame.Core.Logging;
using DotGame.Core.Memory;
using Xunit;

namespace DotGame.Core.Tests.Memory;

public sealed class AllocatorBackedListTests
{
    static AllocatorBackedListTests()
    {
        LogManager.Initialize(LogLevel.Warning, Array.Empty<ILogSink>());
    }

    private readonly struct TestStruct
    {
        public TestStruct(int id, float value)
        {
            Id = id;
            Value = value;
        }

        public int Id { get; }
        public float Value { get; }
    }

    [Fact]
    public void FromArena_CopiesSourceData()
    {
        using var arena = new ArenaAllocator(4096, autoRegisterDiagnostics: false);
        var source = new[]
        {
            new TestStruct(1, 1.5f),
            new TestStruct(2, 2.5f),
            new TestStruct(3, 3.5f)
        };

        using var list = AllocatorBackedList<TestStruct>.FromArena(arena, source);

        Assert.Equal(source.Length, list.Count);
        for (var i = 0; i < source.Length; i++)
            Assert.Equal(source[i].Id, list[i].Id);
    }

    [Fact]
    public void AsSpan_AllowsInPlaceMutation()
    {
        using var arena = new ArenaAllocator(4096, autoRegisterDiagnostics: false);
        var source = new[]
        {
            new TestStruct(1, 1.5f),
            new TestStruct(2, 2.5f)
        };

        using var list = AllocatorBackedList<TestStruct>.FromArena(arena, source);
        var span = list.AsSpan();
        span[0] = new TestStruct(42, 9.5f);

        Assert.Equal(42, list[0].Id);
        Assert.Equal(9.5f, list[0].Value);
    }

    [Fact]
    public void EmptyList_ExposesZeroCountAndAllowsDispose()
    {
        using var arena = new ArenaAllocator(1024, autoRegisterDiagnostics: false);
        using var list = AllocatorBackedList<TestStruct>.FromArena(arena, Array.Empty<TestStruct>());

        var span = list.AsReadOnlySpan();
        Assert.True(span.IsEmpty);
        Assert.Equal(span.Length, list.Count);
    }
}
