using System.Linq;
using DotGame.Core.States;
using DotGame.Core.Systems;

namespace DotGame.Core.Entities;

public sealed class EntityWorld
{
    private readonly List<Entity> _entities = new();
    private readonly List<IEntitySystem> _systems = new();

    public Entity CreateEntity()
    {
        var entity = new Entity();
        _entities.Add(entity);
        return entity;
    }

    public void DestroyEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _entities.Remove(entity);
    }

    public void ClearEntities()
    {
        _entities.Clear();
    }

    public void RegisterSystem(IEntitySystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (_systems.Contains(system))
        {
            return;
        }

        _systems.Add(system);
        _systems.Sort(static (left, right) => left.Order.CompareTo(right.Order));
    }

    public void UnregisterSystem(IEntitySystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _systems.Remove(system);
    }

    public void Update(GameClock clock)
    {
        foreach (var system in _systems)
        {
            system.Update(clock, this);
        }
    }

    public void Draw(GameClock clock)
    {
        foreach (var system in _systems)
        {
            system.Draw(clock, this);
        }
    }

    public IReadOnlyList<Entity> Entities => _entities;

    public TSystem? FindSystem<TSystem>() where TSystem : class, IEntitySystem
        => _systems.OfType<TSystem>().FirstOrDefault();
}
