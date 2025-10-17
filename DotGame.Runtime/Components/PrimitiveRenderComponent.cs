using DotGame.Core.Entities;
using Microsoft.Xna.Framework;

namespace DotGame.Runtime.Components;

public sealed class PrimitiveRenderComponent : IComponent
{
    public Vector2 Size { get; set; }

    public Color FillColor { get; set; } = Color.White;

    public Color? OutlineColor { get; set; } = Color.Black;
}
