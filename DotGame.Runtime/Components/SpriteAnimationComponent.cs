using DotGame.Core.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DotGame.Runtime.Components;

public sealed class SpriteAnimationComponent : IComponent
{
    public string? TextureAsset { get; set; }

    public Texture2D? Texture { get; set; }

    public Point FrameSize { get; set; }

    public int FrameCount { get; set; } = 1;

    public float FrameDuration { get; set; } = 0.1f;

    public bool Loop { get; set; } = true;

    public bool Paused { get; set; }

    public Color Tint { get; set; } = Color.White;

    public Vector2 Origin { get; set; }

    public SpriteEffects Effects { get; set; } = SpriteEffects.None;

    internal float Accumulator;

    internal int CurrentFrame;
}
