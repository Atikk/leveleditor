using DotGame.Core.Entities;
using Microsoft.Xna.Framework;

namespace DotGame.Runtime.Components;

public sealed class MovementComponent : IComponent
{
    public float Speed { get; set; }

    public Vector2 Direction { get; set; }
}
