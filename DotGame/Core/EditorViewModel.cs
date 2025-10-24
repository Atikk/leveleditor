using ReactiveUI;
using System.Reactive;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("Legacy UI-side copy of EditorViewModel. Use DotGame.Core.EditorViewModel in DotGame.Core instead.")]
public class EditorViewModel : ReactiveObject
{
    // Example property for binding
    private string _statusMessage = string.Empty; // Initialize with default value
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    // Example command for saving a map
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public EditorViewModel()
    {
        // Initialize commands
        SaveCommand = ReactiveCommand.Create(SaveMap);
    }

    private void SaveMap()
    {
        // TODO: Implement save logic
        StatusMessage = "Map saved successfully!";
    }
}