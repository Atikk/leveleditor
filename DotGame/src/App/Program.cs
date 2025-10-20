using System;
using DotGame.Core.Platform;
using DotGame.Core.Timing;
using DotGame.Runtime.Platform;
using global::Avalonia;

namespace Dotgame.Avalonia;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        EnsurePlatformServices();

        using var logging = LoggingBootstrapper.Initialize();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void EnsurePlatformServices()
    {
        if (PlatformServices.IsInitialized)
            return;

        var services = new WindowsPlatformServices();
        PlatformServices.Initialize(services);
        TimeSource.Initialize(services.TimeSource);
    }
}


