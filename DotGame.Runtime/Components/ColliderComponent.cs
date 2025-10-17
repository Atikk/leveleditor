using DotGame.Core.Entities;
using Microsoft.Xna.Framework;

namespace DotGame.Runtime.Components;

public sealed class ColliderComponent : IComponent
{
    public Vector2 Size { get; set; }
}
