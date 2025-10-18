using System;

namespace DotGame.Core.Logging;

public static class EngineAssert
{
    public static void True(bool condition, string message, ILogger? logger = null)
    {
        if (!condition)
            throw BuildFailure(message, logger);
    }

    public static T NotNull<T>(T? value, string message, ILogger? logger = null) where T : class
    {
        if (value == null)
            throw BuildFailure(message, logger);

        return value;
    }

    public static void NotNull(object? value, string message, ILogger? logger = null)
    {
        if (value == null)
            throw BuildFailure(message, logger);
    }

    private static InvalidOperationException BuildFailure(string message, ILogger? logger)
    {
        var safeMessage = string.IsNullOrWhiteSpace(message) ? "Assertion failed." : message;

        try
        {
            (logger ?? LogManager.GetLogger("Assertion")).Critical(safeMessage);
        }
        catch
        {
            // Swallow logging errors to avoid recursive failure.
        }

        return new InvalidOperationException(safeMessage);
    }
}
