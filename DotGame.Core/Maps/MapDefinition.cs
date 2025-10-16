namespace DotGame.Core.Maps;

public sealed class MapDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public IReadOnlyList<TileLayer> TileLayers { get; init; } = Array.Empty<TileLayer>();

    public IReadOnlyList<MapEntitySpawn> EntitySpawns { get; init; } = Array.Empty<MapEntitySpawn>();
}
