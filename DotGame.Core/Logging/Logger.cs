using System;
using System.Collections.Generic;

namespace DotGame.Core.Logging;

internal sealed class Logger : ILogger
{
    private readonly IReadOnlyList<ILogSink> sinks;
    private readonly LogLevel minimumLevel;

    public Logger(string category, IReadOnlyList<ILogSink> sinks, LogLevel minimumLevel)
    {
        Category = category;
        this.sinks = sinks;
        this.minimumLevel = minimumLevel;
    }

    public string Category { get; }

    public bool IsEnabled(LogLevel level) => level >= minimumLevel;

    public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (!IsEnabled(level))
            return;

        var safeMessage = message ?? string.Empty;
        IReadOnlyDictionary<string, object?>? propertySnapshot = null;
        if (properties != null && properties.Count > 0)
        {
            propertySnapshot = properties is Dictionary<string, object?> dictionary
                ? new Dictionary<string, object?>(dictionary)
                : new Dictionary<string, object?>(properties);
        }

        var logEvent = new LogEvent(DateTimeOffset.UtcNow, Category, level, safeMessage, exception, propertySnapshot);

        foreach (var sink in sinks)
        {
            try
            {
                sink.Emit(in logEvent);
            }
            catch
            {
                // Intentionally swallow sink exceptions to avoid cascading failures.
            }
        }
    }

    public void Trace(string message, IReadOnlyDictionary<string, object?>? properties = null)
        => Log(LogLevel.Trace, message, null, properties);

    public void Debug(string message, IReadOnlyDictionary<string, object?>? properties = null)
        => Log(LogLevel.Debug, message, null, properties);

    public void Info(string message, IReadOnlyDictionary<string, object?>? properties = null)
        => Log(LogLevel.Information, message, null, properties);

    public void Warn(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        => Log(LogLevel.Warning, message, exception, properties);

    public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        => Log(LogLevel.Error, message, exception, properties);

    public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        => Log(LogLevel.Critical, message, exception, properties);
}
