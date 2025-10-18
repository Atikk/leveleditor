using System;
using System.IO;

namespace DotGame.Core.Logging;

public sealed class FileLogSink : ILogSink, IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;

    public FileLogSink(string filePath, bool append = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var fileStream = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer = new StreamWriter(fileStream) { AutoFlush = true };
    }

    public void Emit(in LogEvent logEvent)
    {
        lock (gate)
        {
            writer.WriteLine($"[{logEvent.Timestamp:O}] {logEvent.Level,-11} {logEvent.Category}: {logEvent.Message}");
            if (logEvent.Exception != null)
                writer.WriteLine(logEvent.Exception);
        }
    }

    public void Dispose()
    {
        writer.Dispose();
    }
}
