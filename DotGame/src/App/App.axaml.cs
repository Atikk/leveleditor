using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Markup.Xaml;
using Dotgame.Avalonia.Views;

namespace Dotgame.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Simple composition root accessor for small services used by views.
    private static Dotgame.Avalonia.Services.ITileService? _tileService;
    public static Dotgame.Avalonia.Services.ITileService TileService => _tileService ??= new Dotgame.Avalonia.Services.TileService();

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainMenuWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}


