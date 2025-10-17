using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DotGame.Core.Entities;

public sealed class Entity
{
    private readonly ConcurrentDictionary<Type, IComponent> _components = new();

    public Guid Id { get; } = Guid.NewGuid();

    public Entity AddOrReplace(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components[component.GetType()] = component;
        return this;
    }

    public bool Remove<TComponent>() where TComponent : class, IComponent
        => _components.TryRemove(typeof(TComponent), out _);

    public bool TryGet<TComponent>([NotNullWhen(true)] out TComponent? component) where TComponent : class, IComponent
    {
        if (_components.TryGetValue(typeof(TComponent), out var stored) && stored is TComponent typed)
        {
            component = typed;
            return true;
        }

        component = null;
        return false;
    }

    public TComponent? Get<TComponent>() where TComponent : class, IComponent
        => TryGet<TComponent>(out var component) ? component : null;

    public IEnumerable<IComponent> Components => _components.Values;
}
