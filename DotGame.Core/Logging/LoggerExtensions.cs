using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotGame.Core.Logging;

public static class LoggerExtensions
{
    public static void LogException(this ILogger logger, Exception exception, string message, LogLevel level = LogLevel.Error, IReadOnlyDictionary<string, object?>? properties = null, [CallerMemberName] string? member = null)
    {
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        var enriched = properties != null ? new Dictionary<string, object?>(properties) : new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(member) && !enriched.ContainsKey("member"))
            enriched["member"] = member;

        logger.Log(level, message, exception, enriched);
    }
}
