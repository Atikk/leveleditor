using System;
using System.Threading.Tasks;
using Dotgame.Avalonia.MonoGameLayer;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("Legacy UI-side copy of MonoGamePreviewManager. Use DotGame.Core.MonoGamePreviewManager in DotGame.Core instead.")]
public class MonoGamePreviewManager
{
    private EditorGame? _editorGame;
    private readonly object _gameLock = new();

    public bool IsInitialized { get; private set; }

    public void Initialize(EditorGame editorGame)
    {
        lock (_gameLock)
        {
            if (IsInitialized)
                throw new InvalidOperationException("Preview manager is already initialized.");

            _editorGame = editorGame;
            IsInitialized = true;
        }
    }

    public void LoadFallbackMap(string mapPath)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Preview manager is not initialized.");

        // TODO: Implement fallback map loading logic
    }

    public void Cleanup()
    {
        lock (_gameLock)
        {
            if (_editorGame != null)
            {
                // TODO: Implement resource cleanup logic
                _editorGame = null;
                IsInitialized = false;
            }
        }
    }
}
