using System;
using System.Collections.Generic;

namespace DotGame.Core.Logging;

public interface ILogger
{
    string Category { get; }

    bool IsEnabled(LogLevel level);

    void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    void Trace(string message, IReadOnlyDictionary<string, object?>? properties = null);

    void Debug(string message, IReadOnlyDictionary<string, object?>? properties = null);

    void Info(string message, IReadOnlyDictionary<string, object?>? properties = null);

    void Warn(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);
}
