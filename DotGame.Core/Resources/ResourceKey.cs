using System;

namespace DotGame.Core.Resources;

public readonly struct ResourceKey : IEquatable<ResourceKey>
{
    public ResourceKey(Type resourceType, string identifier)
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
    }

    public Type ResourceType { get; }

    public string Identifier { get; }

    public bool Equals(ResourceKey other)
    {
        return ResourceType == other.ResourceType && string.Equals(Identifier, other.Identifier, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is ResourceKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ResourceType, Identifier.ToLowerInvariant());
    }

    public override string ToString()
    {
        return $"{ResourceType.Name}:{Identifier}";
    }
}
