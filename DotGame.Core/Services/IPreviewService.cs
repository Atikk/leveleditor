namespace DotGame.Core.Services
{
    /// <summary>
    /// Core-facing preview service contract. The UI registers an adapter that
    /// can start/stop a runtime preview (EditorGame) without exposing UI types
    /// into the core library.
    /// </summary>
    public interface IPreviewService
    {
        /// <summary>
        /// Start the runtime preview for a given map snapshot.
        /// Implementations should be resilient to being called multiple times.
        /// </summary>
        /// <param name="mapSerialized">A serialized representation of the map or a map id. Null if not applicable.</param>
        void StartPreview(string? mapSerialized = null);

        /// <summary>
        /// Stop the running runtime preview if present.
        /// </summary>
        void StopPreview();

        /// <summary>
        /// Returns true when the preview is currently running.
        /// </summary>
        bool IsRunning { get; }
    }
}
