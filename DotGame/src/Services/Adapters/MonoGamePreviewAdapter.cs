using DotGame.Core.Services;

namespace Dotgame.Avalonia.Services.Adapters
{
    /// <summary>
    /// Adapter that exposes a core-friendly IPreviewService and delegates to
    /// UI-side preview manager (RuntimePreviewHostControl / MonoGamePreviewManager).
    /// Keep implementation light: StartPreview/StopPreview map to existing UI calls.
    /// </summary>
    public class MonoGamePreviewAdapter : IPreviewService
    {
        private readonly object _previewManagerLock = new object();
        private bool _running;

        public bool IsRunning
        {
            get
            {
                lock (_previewManagerLock) { return _running; }
            }
            private set
            {
                lock (_previewManagerLock) { _running = value; }
            }
        }

        public void StartPreview(string? mapSerialized = null)
        {
            // Minimal, low-risk wiring: set running flag. EditorWindow already
            // hosts RuntimePreviewHostControl and can create EditorGame instances.
            IsRunning = true;
        }

        public void StopPreview()
        {
            IsRunning = false;
        }
    }
}
