using System;

namespace DotGame.Core.Logging;

public sealed class ConsoleLogSink : ILogSink
{
    private readonly object gate = new();

    public void Emit(in LogEvent logEvent)
    {
        var line = $"[{logEvent.Timestamp:O}] {logEvent.Level,-11} {logEvent.Category}: {logEvent.Message}";
        lock (gate)
        {
            if (logEvent.Level >= LogLevel.Error)
                Console.Error.WriteLine(line);
            else
                Console.Out.WriteLine(line);

            if (logEvent.Exception != null)
                Console.Error.WriteLine(logEvent.Exception);
        }
    }
}
