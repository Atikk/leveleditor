namespace DotGame.Core.Services
{
    // Lightweight, serializable DTO representing a tile entry in core.
    public sealed class CoreTileEntry
    {
        public string? SerializedValue { get; init; }
        public int? TileId { get; init; }
        public string? TilesetKey { get; init; }

        public CoreTileEntry() { }

        public CoreTileEntry(string? serializedValue, int? tileId = null, string? tilesetKey = null)
        {
            SerializedValue = serializedValue;
            TileId = tileId;
            TilesetKey = tilesetKey;
        }
    }
}
