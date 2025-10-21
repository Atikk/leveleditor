using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotGame.Core.Memory;

/// <summary>
/// Provides a read-only list view over allocator-backed memory. Intended for struct data sets materialized from custom allocators.
/// </summary>
public sealed class AllocatorBackedList<T> : IReadOnlyList<T>, IDisposable where T : unmanaged
{
    private IMemoryOwner<byte>? owner;
    private readonly int count;

    private AllocatorBackedList()
    {
        owner = null;
        count = 0;
    }

    private AllocatorBackedList(IMemoryOwner<byte> owner, int count)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.count = count;
    }

    public static AllocatorBackedList<T> FromArena(ArenaAllocator allocator, ReadOnlySpan<T> source)
    {
        if (allocator == null)
            throw new ArgumentNullException(nameof(allocator));

        if (source.Length == 0)
            return new AllocatorBackedList<T>();

        var size = Unsafe.SizeOf<T>();
        var totalBytes = checked(source.Length * size);
        var alignment = ComputeAlignment(size);
        var owner = allocator.Allocate(totalBytes, alignment);
        var span = MemoryMarshal.Cast<byte, T>(owner.Memory.Span);
        source.CopyTo(span[..source.Length]);
        return new AllocatorBackedList<T>(owner, source.Length);
    }

    public int Count => count;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return AsReadOnlySpan()[index];
        }
    }

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        if (owner == null || count == 0)
            return ReadOnlySpan<T>.Empty;

        return MemoryMarshal.Cast<byte, T>(owner.Memory.Span)[..count];
    }

    public Span<T> AsSpan()
    {
        if (owner == null || count == 0)
            return Span<T>.Empty;

        return MemoryMarshal.Cast<byte, T>(owner.Memory.Span)[..count];
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        owner?.Dispose();
        owner = null;
    }

    private static int ComputeAlignment(int elementSize)
    {
        var alignment = 1;
        while (alignment < elementSize && alignment < 64)
            alignment <<= 1;

        return alignment;
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly AllocatorBackedList<T> list;
        private int index;
    private T current;

        internal Enumerator(AllocatorBackedList<T> list)
        {
            this.list = list;
            index = -1;
            current = default;
        }

        public T Current => current;

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var next = index + 1;
            var span = list.AsReadOnlySpan();

            if (next >= span.Length)
            {
                current = default;
                return false;
            }

            index = next;
            current = span[next];
            return true;
        }

        public void Reset()
        {
            index = -1;
            current = default;
        }

        public void Dispose()
        {
        }
    }
}
