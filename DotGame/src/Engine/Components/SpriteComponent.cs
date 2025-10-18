using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Renders a texture at the owning entity's transform.
    /// </summary>
    public sealed class SpriteComponent : ComponentBase
    {
        public Texture2D? Texture { get; set; }

        public Rectangle? SourceRectangle { get; set; }

        public Color Tint { get; set; } = Color.White;

        public Vector2 Origin { get; set; } = Vector2.Zero;

        public Vector2? SizeOverride { get; set; }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (!Enabled || Texture is null)
                return;

            var transform = Owner.Transform;
            var size = SizeOverride ?? new Vector2(Texture.Width, Texture.Height);
            var destination = new Rectangle(
                (int)transform.Position.X,
                (int)transform.Position.Y,
                (int)size.X,
                (int)size.Y);

            spriteBatch.Draw(Texture, destination, SourceRectangle, Tint, transform.Rotation, Origin, SpriteEffects.None, 0f);
        }
    }
}

