using System;
using global::Avalonia.Threading;
using DotGame.Core.Services;

namespace Dotgame.Avalonia.Services
{
    /// <summary>
    /// UI-side preview service that delegates to the EditorWindow's preview lifecycle.
    /// Registered into the core ServiceContainer so core code can request previews.
    /// </summary>
    public sealed class EditorWindowPreviewService : IPreviewService
    {
    private readonly Views.EditorWindow _owner;

        public EditorWindowPreviewService(Views.EditorWindow owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public bool IsRunning => _owner.IsPreviewRunning;

        public void StartPreview(string? mapSerialized = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _owner.StartPreview(mapSerialized);
                }
                catch
                {
                    // best-effort
                }
            });
        }

        public void StopPreview()
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _owner.StopPreview();
                }
                catch
                {
                    // best-effort
                }
            });
        }
    }
}
