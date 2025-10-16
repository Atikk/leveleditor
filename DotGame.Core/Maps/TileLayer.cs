namespace DotGame.Core.Maps;

public sealed class TileLayer
{
    public required string Name { get; init; }

    public required string Tileset { get; init; }

    public int Order { get; init; }

    public IReadOnlyList<int> TileData { get; init; } = Array.Empty<int>();
}
