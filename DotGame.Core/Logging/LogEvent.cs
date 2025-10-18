using System;
using System.Collections.Generic;

namespace DotGame.Core.Logging;

public readonly struct LogEvent
{
    public LogEvent(DateTimeOffset timestamp, string category, LogLevel level, string message, Exception? exception, IReadOnlyDictionary<string, object?>? properties)
    {
        Timestamp = timestamp;
        Category = category;
        Level = level;
        Message = message;
        Exception = exception;
        Properties = properties;
    }

    public DateTimeOffset Timestamp { get; }

    public string Category { get; }

    public LogLevel Level { get; }

    public string Message { get; }

    public Exception? Exception { get; }

    public IReadOnlyDictionary<string, object?>? Properties { get; }
}
