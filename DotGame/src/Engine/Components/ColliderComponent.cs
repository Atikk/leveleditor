using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using DotGameAvalonia.Engine;

namespace DotGameAvalonia.Engine.Components
{
    /// <summary>
    /// Axis-aligned bounding box collider with optional trigger/static semantics.
    /// </summary>
    public sealed class ColliderComponent : ComponentBase
    {
        private RectangleF _bounds;

        public Vector2 Size { get; set; }

        public Vector2 Offset { get; set; } = Vector2.Zero;

        public bool IsStatic { get; set; }

        public bool IsTrigger { get; set; }

        public RectangleF Bounds => _bounds;

        public event Action<Entity>? Collision;

        public override void Initialize()
        {
            UpdateBounds();
        }

        public override void Update(GameTime gameTime)
        {
            UpdateBounds();
        }

        public void UpdateBounds()
        {
            var position = Owner.Transform.Position + Offset;
            _bounds = new RectangleF(position.X, position.Y, Size.X, Size.Y);
        }

        internal void RaiseCollision(Entity other)
        {
            Collision?.Invoke(other);
        }
    }
}
