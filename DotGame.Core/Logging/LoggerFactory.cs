using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DotGame.Core.Logging;

public sealed class LoggerFactory
{
    private readonly ConcurrentDictionary<string, ILogger> cache = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<ILogSink> sinks;

    public LoggerFactory(IEnumerable<ILogSink> sinks, LogLevel minimumLevel)
    {
        this.sinks = sinks.ToList();
        MinimumLevel = minimumLevel;
    }

    public LogLevel MinimumLevel { get; }

    public ILogger CreateLogger(string category)
    {
        return cache.GetOrAdd(category ?? string.Empty, CreateLoggerCore);
    }

    private ILogger CreateLoggerCore(string category)
    {
        return new Logger(category, sinks, MinimumLevel);
    }
}
