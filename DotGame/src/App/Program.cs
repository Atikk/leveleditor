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

        // Log application lifecycle events (startup/shutdown)
        var logger = DotGame.Core.Logging.LogManager.GetLogger("App.Program");
        logger.Info("DotGame Avalonia application starting");

        try
        {
            // Register platform/application services that may be resolved by UI components.
            try
            {
                // Register a default tile service implementation for composition roots and tests.
                var defaultTileService = new Dotgame.Avalonia.Services.TileService();
                // Register the UI interface (old) for backward compatibility
                DotGame.Core.Platform.ServiceContainer.RegisterSingleton<Dotgame.Avalonia.Services.ITileService>(defaultTileService);
                // Also register using the new core interface so consumers can depend on DotGame.Core.Services.ITileService
                DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.ITileService>(new Dotgame.Avalonia.Services.Adapters.TileServiceAdapter(defaultTileService));
                // Register preview adapter for core preview contract (lightweight adapter)
                DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.IPreviewService>(new Dotgame.Avalonia.Services.Adapters.MonoGamePreviewAdapter());
            }
            catch
            {
                // best-effort registration; continue even if tile service cannot be created at startup
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            logger.Info("DotGame Avalonia application exiting");
        }
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


