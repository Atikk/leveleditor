using System;
using DotGame.Core.Async;
using DotGame.Core.Entities;
using DotGame.Core.Resources;
using DotGame.Runtime.GameData;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DotGame.Runtime.Rendering;

public sealed class RuntimeContext
{
    public RuntimeContext(ContentManager content, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, EntityWorld world, OrthographicCamera camera, GameDataRepository gameData, AsyncTaskScheduler scheduler, ResourceManager resources)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        SpriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        GameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        Resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public ContentManager Content { get; }

    public GraphicsDevice GraphicsDevice { get; }

    public SpriteBatch SpriteBatch { get; }

    public EntityWorld World { get; }

    public OrthographicCamera Camera { get; }

    public GameDataRepository GameData { get; }

    public AsyncTaskScheduler Scheduler { get; }

    public ResourceManager Resources { get; }
}
