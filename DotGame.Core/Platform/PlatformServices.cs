using System;

namespace DotGame.Core.Platform;

public static class PlatformServices
{
    private static readonly object Gate = new();
    private static IPlatformServices? current;

    public static bool IsInitialized
    {
        get
        {
            lock (Gate)
            {
                return current != null;
            }
        }
    }

    public static IPlatformServices Current
    {
        get
        {
            lock (Gate)
            {
                if (current == null)
                    throw new InvalidOperationException("Platform services have not been initialized.");
                return current;
            }
        }
    }

    public static void Initialize(IPlatformServices services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        lock (Gate)
        {
            current = services;
        }
    }
}
