using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Contract implemented by all runtime components that can be attached to an entity.
    /// </summary>
    public interface IGameEntityComponent
    {
        bool Enabled { get; set; }

        Entity Owner { get; }

        void OnAttached(Entity owner);

        void Initialize();

        void Update(GameTime gameTime);

        void Draw(GameTime gameTime, SpriteBatch spriteBatch);
    }
}

