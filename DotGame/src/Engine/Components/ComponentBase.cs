using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Convenience base class that supplies common plumbing for game components.
    /// </summary>
    public abstract class ComponentBase : IGameEntityComponent
    {
        public bool Enabled { get; set; } = true;

        public Entity Owner { get; private set; } = null!;

        public virtual void OnAttached(Entity owner)
        {
            Owner = owner;
        }

        public virtual void Initialize()
        {
        }

        public virtual void Update(GameTime gameTime)
        {
        }

        public virtual void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
        }
    }
}

