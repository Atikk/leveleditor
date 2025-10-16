namespace DotGame.Core.Maps;

public sealed class MapEntitySpawn
{
    public required string EntityPrototypeId { get; init; }

    public float X { get; init; }

    public float Y { get; init; }

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
