using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Tracks the position, rotation, and scale of an entity within the world.
    /// </summary>
    public sealed class TransformComponent : ComponentBase
    {
        public Vector2 Position { get; set; } = Vector2.Zero;

        public float Rotation { get; set; }

        public Vector2 Scale { get; set; } = Vector2.One;

        public void Translate(Vector2 delta)
        {
            Position += delta;
        }
    }
}

