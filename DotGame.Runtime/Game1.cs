using System;
using DotGame.Core.Async;
using DotGame.Core.Entities;
using DotGame.Core.Resources;
using DotGame.Core.States;
using DotGame.Runtime.GameData;
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
    private CameraController? _cameraController;
    private AsyncTaskScheduler? _scheduler;
    private ResourceManager? _resourceManager;
    private ResourceHandle<GameDataLoadReport>? _gameDataLoadHandle;

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
        _scheduler = new AsyncTaskScheduler(workerCount: 2, workerNamePrefix: "RuntimeWorker-");
        _resourceManager = new ResourceManager(_scheduler);
        _scheduler.UnhandledException += ex => Console.WriteLine($"[Scheduler] Unhandled exception: {ex.Message}");

        var spriteBatch = new SpriteBatch(GraphicsDevice);
        _spriteBatch = spriteBatch;
        var adapter = new BoxingViewportAdapter(Window, GraphicsDevice, 1280, 720);
        _viewportAdapter = adapter;
        var camera = new OrthographicCamera(adapter);
        _camera = camera;
        _cameraController = new CameraController(camera, adapter);
        var gameData = new GameDataRepository();
        _runtimeContext = new RuntimeContext(Content, GraphicsDevice, spriteBatch, _world, camera, gameData, _scheduler, _resourceManager);

        if (_resourceManager != null)
        {
            _gameDataLoadHandle = _resourceManager.LoadAsync(
                key: "gamedata:default",
                loader: _ => gameData.LoadAllFromContent(),
                onCompleted: OnGameDataLoaded,
                onFailed: OnGameDataFailed);
        }

        var gameplay = new GameplayState(_runtimeContext);
        _stateStack.Push(gameplay);
        UpdateCameraBounds(centerCamera: true);
    }

    protected override void UnloadContent()
    {
        base.UnloadContent();
        _stateStack.Clear();
        _viewportAdapter?.Dispose();
        _spriteBatch?.Dispose();
        if (_gameDataLoadHandle != null && _resourceManager != null)
        {
            _resourceManager.Release(_gameDataLoadHandle);
            _gameDataLoadHandle = null;
        }

        _resourceManager?.Dispose();
        _resourceManager = null;
        _scheduler?.Dispose();
        _scheduler = null;
    }

    protected override void Update(GameTime gameTime)
    {
        if (_runtimeContext is null)
        {
            base.Update(gameTime);
            return;
        }

    _resourceManager?.PumpMainThread();

        var clock = GameClock.From(gameTime.ElapsedGameTime, gameTime.TotalGameTime);
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        UpdateCameraBounds(centerCamera: false);
        _cameraController?.HandleInput(gameTime, keyboard, mouse);
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
        _cameraController?.HandleViewportResize();
    }

    private void UpdateCameraBounds(bool centerCamera)
    {
        var gameplay = _stateStack.ActiveState as GameplayState;
        var bounds = gameplay?.WorldBounds ?? RectangleF.Empty;
        if (bounds == RectangleF.Empty)
            return;

        _cameraController?.SetWorldBounds(bounds, centerCamera);
    }

    private void OnGameDataLoaded(ResourceHandle<GameDataLoadReport> handle)
    {
        var report = handle.Value;

        if (report.HasErrors)
        {
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"[GameData] Failed to load '{error.FilePath}': {error.Message}");
            }
        }

        Console.WriteLine($"[GameData] Loaded {report.DialogueCount} dialogues, {report.QuestCount} quests, {report.CutsceneCount} cutscenes.");

        if (_resourceManager != null)
        {
            _resourceManager.Release(handle);
        }

        if (ReferenceEquals(_gameDataLoadHandle, handle))
        {
            _gameDataLoadHandle = null;
        }
    }

    private void OnGameDataFailed(ResourceHandle<GameDataLoadReport> handle)
    {
        var exception = handle.Exception;
        Console.WriteLine(exception != null
            ? $"[GameData] Load failed: {exception.Message}"
            : "[GameData] Load failed with unknown error.");

        if (_resourceManager != null)
        {
            _resourceManager.Release(handle);
        }

        if (ReferenceEquals(_gameDataLoadHandle, handle))
        {
            _gameDataLoadHandle = null;
        }
    }
}
