using System;
using System.Collections.Generic;

namespace DotGame.Core.Logging;

public static class LogManager
{
    private static readonly object Gate = new();
    private static LoggerFactory? factory;
    private static BufferedLogSink? bufferedSink;

    public static LogLevel MinimumLevel { get; private set; } = LogLevel.Information;

    public static void Initialize(LogLevel minimumLevel, IEnumerable<ILogSink> sinks)
    {
        if (sinks == null)
            throw new ArgumentNullException(nameof(sinks));

        lock (Gate)
        {
            var sinkList = new List<ILogSink>();
            bufferedSink = null;

            foreach (var sink in sinks)
            {
                if (sink == null)
                    continue;

                sinkList.Add(sink);
                if (bufferedSink == null && sink is BufferedLogSink candidate)
                    bufferedSink = candidate;
            }

            factory = new LoggerFactory(sinkList, minimumLevel);
            MinimumLevel = minimumLevel;
        }
    }

    public static ILogger GetLogger(string category)
    {
        lock (Gate)
        {
            if (factory == null)
                throw new InvalidOperationException("LogManager has not been initialized.");

            return factory.CreateLogger(category);
        }
    }

    public static ILogger GetLogger<T>() => GetLogger(typeof(T).FullName ?? typeof(T).Name);

    public static BufferedLogSink? GetBufferedSink()
    {
        lock (Gate)
        {
            return bufferedSink;
        }
    }
}
