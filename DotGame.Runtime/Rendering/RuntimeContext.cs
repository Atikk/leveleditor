using System;
using DotGame.Core.Entities;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DotGame.Runtime.Rendering;

public sealed class RuntimeContext
{
    public RuntimeContext(ContentManager content, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, EntityWorld world, OrthographicCamera camera)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        SpriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    public ContentManager Content { get; }

    public GraphicsDevice GraphicsDevice { get; }

    public SpriteBatch SpriteBatch { get; }

    public EntityWorld World { get; }

    public OrthographicCamera Camera { get; }
}
