using System;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Input;
using DotGame.Runtime.Rendering;
using DotGame.Runtime.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Tiled;
using MonoGame.Extended.Tiled.Renderers;

namespace DotGame.Runtime.Scenes;

public sealed class GameplayState : GameStateBase
{
    private readonly RuntimeContext _runtime;
    private readonly EntityWorld _world;
    private readonly OrthographicCamera _camera;
    private readonly ContentManager _content;
    private TiledMap? _map;
    private TiledMapRenderer? _renderer;
    private readonly float _cameraSpeed = 256f;

    public GameplayState(RuntimeContext runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _world = runtime.World;
        _camera = runtime.Camera;
        _content = runtime.Content;
    }

    public override void OnEnter()
    {
        TryLoadDefaultMap();
    }

    public override void Update(in RuntimeUpdateContext context)
    {
        base.Update(context);
        _world.Update(context.Clock);
        HandleCameraInput(context.Input, context.Clock.Delta);
    var gameTime = new GameTime(context.Clock.Total, context.Clock.Delta);
    _renderer?.Update(gameTime);
    }

    public override void Draw(in RuntimeDrawContext context)
    {
        var runtime = context.Runtime;
        runtime.GraphicsDevice.Clear(Color.Black);

        var viewMatrix = _camera.GetViewMatrix();
        if (_renderer != null)
        {
            _renderer.Draw(viewMatrix);
        }

        var spriteBatch = runtime.SpriteBatch;
        spriteBatch.Begin(transformMatrix: viewMatrix);
        DrawWorld(spriteBatch);
        spriteBatch.End();
    }

    public override void OnExit()
    {
        base.OnExit();
        _renderer?.Dispose();
        _renderer = null;
        _map = null;
    }

    private void HandleCameraInput(InputSnapshot input, TimeSpan delta)
    {
        var direction = Vector2.Zero;
        if (input.IsKeyDown(Keys.W) || input.IsKeyDown(Keys.Up))
        {
            direction.Y -= 1f;
        }
        if (input.IsKeyDown(Keys.S) || input.IsKeyDown(Keys.Down))
        {
            direction.Y += 1f;
        }
        if (input.IsKeyDown(Keys.A) || input.IsKeyDown(Keys.Left))
        {
            direction.X -= 1f;
        }
        if (input.IsKeyDown(Keys.D) || input.IsKeyDown(Keys.Right))
        {
            direction.X += 1f;
        }

        if (direction != Vector2.Zero)
        {
            direction.Normalize();
            var movement = direction * _cameraSpeed * (float)delta.TotalSeconds;
            _camera.Move(movement);
        }
    }

    private void DrawWorld(SpriteBatch spriteBatch)
    {
    var rect = new RectangleF(-16f, -16f, 32f, 32f);
    spriteBatch.DrawRectangle(rect, Color.Lime);
    }

    private void TryLoadDefaultMap()
    {
        const string assetName = "Maps/sample";
        try
        {
            _map = _content.Load<TiledMap>(assetName);
            _renderer = new TiledMapRenderer(_runtime.GraphicsDevice, _map);
        }
        catch (ContentLoadException)
        {
            _map = null;
            _renderer = null;
        }
    }
}
