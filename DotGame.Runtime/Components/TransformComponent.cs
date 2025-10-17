using DotGame.Core.Entities;
using Microsoft.Xna.Framework;

namespace DotGame.Runtime.Components;

public sealed class TransformComponent : IComponent
{
    public Vector2 Position { get; set; }

    public float Rotation { get; set; }

    public Vector2 Scale { get; set; } = Vector2.One;
}
