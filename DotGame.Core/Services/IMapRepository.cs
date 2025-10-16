using DotGame.Core.Maps;

namespace DotGame.Core.Services;

public interface IMapRepository
{
    MapDefinition? FindById(string mapId);

    IEnumerable<MapDefinition> Enumerate();
}
