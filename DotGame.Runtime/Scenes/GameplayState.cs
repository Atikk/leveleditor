using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using DotGame.Runtime.Content;
using DotGame.Runtime.Rendering;
using DotGame.Runtime.States;
using DotGame.Runtime.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using TiledCS;

namespace DotGame.Runtime.Scenes;

public sealed class GameplayState : GameStateBase
{
    private const int PlayerFrameWidth = 32;
    private const int PlayerFrameHeight = 32;
    private const int PlayerFrameCount = 4;
    private const float PlayerFrameDuration = 0.15f;

    private readonly RuntimeContext _runtime;
    private readonly EntityWorld _world;
    private readonly GameplayCollisionWorld _collisionWorld;
    private readonly List<RectangleF> _colliders = new();
    private readonly List<IRuntimeEntitySystem> _runtimeSystems = new();

    private RuntimeTiledMap? _map;
    private RectangleF _worldBounds = new(-256f, -256f, 512f, 512f);
    private Vector2 _playerSpawn = new(32f, 32f);
    private bool _systemsConfigured;

    private readonly float _playerSpeed = 180f;
    private readonly Vector2 _playerSize = new(PlayerFrameWidth, PlayerFrameHeight);

    private PlayerInputSystem? _playerInputSystem;
    private MovementSystem? _movementSystem;
    private SpriteAnimationSystem? _spriteAnimationSystem;
    private CameraFollowSystem? _cameraFollowSystem;
    private SpriteRenderSystem? _spriteRenderSystem;
    private PrimitiveRenderSystem? _primitiveRenderSystem;

    private Texture2D? _playerSpriteTexture;

    public RectangleF WorldBounds => _worldBounds;

    public GameplayState(RuntimeContext runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _world = runtime.World;
        _collisionWorld = new GameplayCollisionWorld(this);
    }

    public override void OnEnter()
    {
        EnsureSystemsConfigured();
        TryLoadDefaultMap();
        InitializeWorldState();
    }

    public override void Update(in RuntimeUpdateContext context)
    {
        base.Update(context);
        ApplyUpdateContext(context);
        _world.Update(context.Clock);
    }

    public override void Draw(in RuntimeDrawContext context)
    {
        var runtime = context.Runtime;
        runtime.GraphicsDevice.Clear(Color.Black);

        var spriteBatch = runtime.SpriteBatch;
        var viewMatrix = runtime.Camera.GetViewMatrix();

        if (_map != null)
        {
            spriteBatch.Begin(transformMatrix: viewMatrix, samplerState: SamplerState.PointClamp);
            RuntimeTiledMapRenderer.DrawTileLayers(spriteBatch, _map);
            spriteBatch.End();
        }

        ApplyDrawContext(context);
        _world.Draw(context.Clock);
    }

    public override void OnExit()
    {
        base.OnExit();
        DisposeMap();
        _world.ClearEntities();
        _playerSpriteTexture?.Dispose();
        _playerSpriteTexture = null;
    }

    private void EnsureSystemsConfigured()
    {
        if (_systemsConfigured)
            return;

        _playerInputSystem = new PlayerInputSystem();
        _movementSystem = new MovementSystem(_collisionWorld);
        _spriteAnimationSystem = new SpriteAnimationSystem();
        _cameraFollowSystem = new CameraFollowSystem();
        _spriteRenderSystem = new SpriteRenderSystem();
        _primitiveRenderSystem = new PrimitiveRenderSystem();

        RegisterRuntimeSystem(_playerInputSystem);
        RegisterRuntimeSystem(_movementSystem);
        RegisterRuntimeSystem(_spriteAnimationSystem);
        RegisterRuntimeSystem(_cameraFollowSystem);
        RegisterRuntimeSystem(_spriteRenderSystem);
        RegisterRuntimeSystem(_primitiveRenderSystem);

        _systemsConfigured = true;
    }

    private void RegisterRuntimeSystem(IRuntimeEntitySystem system)
    {
        _world.RegisterSystem(system);
        _runtimeSystems.Add(system);
    }

    private void ApplyUpdateContext(in RuntimeUpdateContext context)
    {
        foreach (var system in _runtimeSystems)
        {
            system.ApplyUpdateContext(context);
        }
    }

    private void ApplyDrawContext(in RuntimeDrawContext context)
    {
        foreach (var system in _runtimeSystems)
        {
            system.ApplyDrawContext(context);
        }
    }

    private void InitializeWorldState()
    {
        _world.ClearEntities();
        _colliders.Clear();

        if (_map != null)
        {
            ExtractCollisionObjects();
            TryResolvePlayerSpawn();
        }
        else
        {
            BuildFallbackEnvironment();
        }

        _playerSpawn = ClampPositionToWorld(_playerSpawn);

        var playerTexture = GetPlayerSpriteTexture();

        _world.CreateEntity()
            .AddOrReplace(new TransformComponent { Position = _playerSpawn })
            .AddOrReplace(new MovementComponent { Speed = _playerSpeed })
            .AddOrReplace(new ColliderComponent { Size = _playerSize })
            .AddOrReplace(new PlayerControlledComponent())
            .AddOrReplace(new CameraTargetComponent())
            .AddOrReplace(new SpriteAnimationComponent
            {
                Texture = playerTexture,
                FrameSize = new Point(PlayerFrameWidth, PlayerFrameHeight),
                FrameCount = PlayerFrameCount,
                FrameDuration = PlayerFrameDuration,
                Origin = new Vector2(PlayerFrameWidth * 0.5f, PlayerFrameHeight * 0.5f),
                Tint = Color.White
            });
    }

    private void TryLoadDefaultMap()
    {
        DisposeMap();

        var mapPath = Path.Combine(AppContext.BaseDirectory, "Content", "Maps", "sample.tmx");
        if (!File.Exists(mapPath))
        {
            CaptureMapMetadata();
            return;
        }

        try
        {
            _map = new RuntimeTiledMap(_runtime.GraphicsDevice, mapPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TiledException)
        {
            _map = null;
        }

        CaptureMapMetadata();
    }

    private void CaptureMapMetadata()
    {
        if (_map == null)
        {
            _worldBounds = new RectangleF(-256f, -256f, 512f, 512f);
            return;
        }

        var width = Math.Max(1, _map.PixelWidth);
        var height = Math.Max(1, _map.PixelHeight);
        _worldBounds = new RectangleF(0f, 0f, width, height);
    }

    private void ExtractCollisionObjects()
    {
        if (_map == null)
            return;

        foreach (var objectLayer in _map.ObjectLayers)
        {
            if (!string.Equals(objectLayer.name, "Collisions", StringComparison.OrdinalIgnoreCase))
                continue;

            var objects = objectLayer.objects ?? Array.Empty<TiledObject>();
            foreach (var mapObject in objects)
            {
                if (mapObject.width > 0f && mapObject.height > 0f)
                {
                    var position = new Vector2(mapObject.x, mapObject.y);
                    var size = new Vector2(mapObject.width, mapObject.height);
                    _colliders.Add(new RectangleF(position, size));
                }
            }
        }
    }

    private void TryResolvePlayerSpawn()
    {
        if (_map == null)
            return;

        var spawnLayer = _map.ObjectLayers.FirstOrDefault(layer => string.Equals(layer.name, "Spawns", StringComparison.OrdinalIgnoreCase));
        if (spawnLayer == null)
        {
            _playerSpawn = new Vector2(_worldBounds.Width, _worldBounds.Height) * 0.5f - _playerSize * 0.5f;
            return;
        }

        var objects = spawnLayer.objects ?? Array.Empty<TiledObject>();
        var playerSpawn = objects.FirstOrDefault(obj => string.Equals(obj.name, "Player", StringComparison.OrdinalIgnoreCase));
        if (playerSpawn == null)
        {
            _playerSpawn = new Vector2(_worldBounds.Width, _worldBounds.Height) * 0.5f - _playerSize * 0.5f;
            return;
        }

        if (playerSpawn.width > 0f && playerSpawn.height > 0f)
        {
            var rectCenter = new Vector2(playerSpawn.x + playerSpawn.width * 0.5f, playerSpawn.y + playerSpawn.height * 0.5f);
            _playerSpawn = rectCenter - _playerSize * 0.5f;
            return;
        }

        _playerSpawn = new Vector2(playerSpawn.x, playerSpawn.y) - _playerSize * 0.5f;
    }

    private void BuildFallbackEnvironment()
    {
        const float wallThickness = 32f;
        const float arenaSize = 640f;
        _worldBounds = new RectangleF(-arenaSize * 0.5f, -arenaSize * 0.5f, arenaSize, arenaSize);

        var bounds = _worldBounds;
        _colliders.Add(new RectangleF(bounds.X - wallThickness, bounds.Y - wallThickness, bounds.Width + wallThickness * 2f, wallThickness));
        _colliders.Add(new RectangleF(bounds.X - wallThickness, bounds.Bottom, bounds.Width + wallThickness * 2f, wallThickness));
        _colliders.Add(new RectangleF(bounds.X - wallThickness, bounds.Y, wallThickness, bounds.Height));
        _colliders.Add(new RectangleF(bounds.Right, bounds.Y, wallThickness, bounds.Height));

        _colliders.Add(new RectangleF(bounds.X + 96f, bounds.Y + 96f, 128f, 32f));
        _colliders.Add(new RectangleF(bounds.X + 64f, bounds.Bottom - 160f, 192f, 32f));
        _playerSpawn = new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f) - _playerSize * 0.5f;
    }

    private Texture2D GetPlayerSpriteTexture()
    {
        if (_playerSpriteTexture != null && !_playerSpriteTexture.IsDisposed)
            return _playerSpriteTexture;

        var totalWidth = PlayerFrameWidth * PlayerFrameCount;
        var texture = new Texture2D(_runtime.GraphicsDevice, totalWidth, PlayerFrameHeight);
        var data = new Color[totalWidth * PlayerFrameHeight];
        var bodyColors = new[] { Color.CornflowerBlue, Color.MediumSeaGreen, Color.Orange, Color.Violet };

        for (var frame = 0; frame < PlayerFrameCount; frame++)
        {
            var baseX = frame * PlayerFrameWidth;
            var bodyColor = bodyColors[frame % bodyColors.Length];

            for (var y = 0; y < PlayerFrameHeight; y++)
            {
                for (var x = 0; x < PlayerFrameWidth; x++)
                {
                    var index = y * totalWidth + baseX + x;
                    var color = bodyColor;

                    var isBorder = x < 2 || x >= PlayerFrameWidth - 2 || y < 2 || y >= PlayerFrameHeight - 2;
                    if (isBorder)
                    {
                        color = Color.Black;
                    }
                    else if (((x + y) + frame) % 6 == 0)
                    {
                        color = Color.White;
                    }

                    data[index] = color;
                }
            }
        }

        texture.SetData(data);
        _playerSpriteTexture = texture;
        return texture;
    }

    private Vector2 ClampPositionToWorld(Vector2 position)
    {
        var min = new Vector2(_worldBounds.X, _worldBounds.Y);
        var max = new Vector2(_worldBounds.Right - _playerSize.X, _worldBounds.Bottom - _playerSize.Y);
        return Vector2.Clamp(position, min, max);
    }

    private void DisposeMap()
    {
        _map?.Dispose();
        _map = null;
    }

    private sealed class GameplayCollisionWorld : ICollisionWorld
    {
        private readonly GameplayState _owner;

        public GameplayCollisionWorld(GameplayState owner)
        {
            _owner = owner;
        }

        public RectangleF WorldBounds => _owner._worldBounds;

        public IReadOnlyList<RectangleF> StaticColliders => _owner._colliders;
    }
}
