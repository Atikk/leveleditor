using System;
using System.Collections.Generic;

namespace DotGame.Core.Logging;

public sealed class BufferedLogSink : ILogSink
{
    private readonly Queue<LogEvent> buffer;
    private readonly object gate = new();

    public BufferedLogSink(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
        buffer = new Queue<LogEvent>(capacity);
    }

    public int Capacity { get; }

    public event EventHandler<LogEvent>? LogReceived;

    public IReadOnlyList<LogEvent> Snapshot
    {
        get
        {
            lock (gate)
            {
                return buffer.ToArray();
            }
        }
    }

    public void Emit(in LogEvent logEvent)
    {
        EventHandler<LogEvent>? handler;
        lock (gate)
        {
            if (buffer.Count == Capacity)
                buffer.Dequeue();

            buffer.Enqueue(logEvent);
            handler = LogReceived;
        }

        handler?.Invoke(this, logEvent);
    }
}
