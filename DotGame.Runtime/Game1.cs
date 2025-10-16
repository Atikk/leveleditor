using System;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Input;
using DotGame.Runtime.Rendering;
using DotGame.Runtime.Scenes;
using DotGame.Runtime.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

namespace DotGame.Runtime;

public sealed class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly EntityWorld _world = new();
    private readonly GameStateStack _stateStack = new();
    private SpriteBatch? _spriteBatch;
    private RuntimeContext? _runtimeContext;
    private BoxingViewportAdapter? _viewportAdapter;
    private OrthographicCamera? _camera;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
        base.Initialize();
    }

    protected override void LoadContent()
    {
    var spriteBatch = new SpriteBatch(GraphicsDevice);
    _spriteBatch = spriteBatch;
    var adapter = new BoxingViewportAdapter(Window, GraphicsDevice, 1280, 720);
    _viewportAdapter = adapter;
    var camera = new OrthographicCamera(adapter);
    _camera = camera;
    _runtimeContext = new RuntimeContext(Content, GraphicsDevice, spriteBatch, _world, camera);

        var gameplay = new GameplayState(_runtimeContext);
        _stateStack.Push(gameplay);
    }

    protected override void UnloadContent()
    {
        base.UnloadContent();
        _stateStack.Clear();
        _viewportAdapter?.Dispose();
        _spriteBatch?.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_runtimeContext is null)
        {
            base.Update(gameTime);
            return;
        }

        var clock = GameClock.From(gameTime.ElapsedGameTime, gameTime.TotalGameTime);
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One);
        var input = new InputSnapshot(keyboard, mouse, gamePad, gamePad.IsConnected);
        var context = new RuntimeUpdateContext(_runtimeContext, clock, input);

        _stateStack.Update(context);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_runtimeContext is null)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
            return;
        }

        var clock = GameClock.From(gameTime.ElapsedGameTime, gameTime.TotalGameTime);
        var drawContext = new RuntimeDrawContext(_runtimeContext, clock);

        _stateStack.Draw(drawContext);

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Window.ClientSizeChanged -= OnClientSizeChanged;
            _stateStack.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        _viewportAdapter?.Reset();
    }
}
