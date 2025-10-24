using System.Collections.Generic;
using System.Text.Json;

namespace DotGame.Core.Services
{
    // Core-facing, UI-agnostic tile service contract.
    // Implementations in UI projects may map between UI types and these DTOs.
    public interface ITileService
    {
        CoreTileEntry?[,] CreateTileBuffer(int width, int height);
        CoreTileEntry?[,] CreateTileBufferFromSerialized(JsonElement[][]? source, string baseDirectory, int fallbackCols, int fallbackRows, object? tilesetState);
        CoreTileEntry? CreateTileEntryFromNumber(JsonElement element, object? tilesetState);
        bool InBounds(CoreTileEntry?[,] buffer, int x, int y);
        CoreTileEntry? GetTopmostTile(IList<object> layers, int x, int y, out object? owningLayer);
        CoreTileEntry? LoadTileEntry(string storedValue, string baseDirectory, object? tilesetState, System.Action<string>? onWarn = null);
        bool EditorHasTilesPlaced(IEnumerable<object> layers);
    }
}
