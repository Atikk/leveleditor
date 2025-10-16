using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Input;
using DotGame.Runtime.Rendering;
using DotGame.Runtime.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using DotGame.Runtime.Content;
using MonoGame.Extended;
using TiledCS;

namespace DotGame.Runtime.Scenes;

public sealed class GameplayState : GameStateBase
{
    private readonly RuntimeContext _runtime;
    private readonly EntityWorld _world;
    private readonly OrthographicCamera _camera;
    private RuntimeTiledMap? _map;
    private readonly float _playerSpeed = 180f;
    private readonly Vector2 _playerSize = new(28f, 28f);
    private readonly List<RectangleF> _colliders = new();
    private RectangleF _worldBounds = new(-256f, -256f, 512f, 512f);
    private Vector2 _playerPosition = new(32f, 32f);

    public GameplayState(RuntimeContext runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _world = runtime.World;
    _camera = runtime.Camera;
    }

    public override void OnEnter()
    {
        TryLoadDefaultMap();
        InitializeWorldState();
    }

    public override void Update(in RuntimeUpdateContext context)
    {
        base.Update(context);
        _world.Update(context.Clock);
        HandlePlayerInput(context.Input, context.Clock.Delta);
        FollowPlayer();
    }

    public override void Draw(in RuntimeDrawContext context)
    {
        var runtime = context.Runtime;
        runtime.GraphicsDevice.Clear(Color.Black);

        var viewMatrix = _camera.GetViewMatrix();
        var spriteBatch = runtime.SpriteBatch;
        spriteBatch.Begin(transformMatrix: viewMatrix, samplerState: SamplerState.PointClamp);
        if (_map != null)
        {
            RuntimeTiledMapRenderer.DrawTileLayers(spriteBatch, _map);
        }
        DrawWorld(spriteBatch);
        spriteBatch.End();
    }

    public override void OnExit()
    {
        base.OnExit();
        DisposeMap();
    }

    private void HandlePlayerInput(InputSnapshot input, TimeSpan delta)
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
            var movement = direction * _playerSpeed * (float)delta.TotalSeconds;
            TryMovePlayer(new Vector2(movement.X, 0f));
            TryMovePlayer(new Vector2(0f, movement.Y));
        }
    }

    private void DrawWorld(SpriteBatch spriteBatch)
    {
        foreach (var collider in _colliders)
        {
            spriteBatch.DrawRectangle(collider, Color.OrangeRed * 0.9f);
        }

        var playerBounds = GetPlayerBounds();
        spriteBatch.FillRectangle(playerBounds, Color.White);
        spriteBatch.DrawRectangle(playerBounds, Color.Black);
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

    private void InitializeWorldState()
    {
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

        _playerPosition = ClampPositionToWorld(_playerPosition);
        FollowPlayer();
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
        {
            return;
        }

        foreach (var objectLayer in _map.ObjectLayers)
        {
            if (!string.Equals(objectLayer.name, "Collisions", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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
        {
            return;
        }

        var spawnLayer = _map.ObjectLayers.FirstOrDefault(layer => string.Equals(layer.name, "Spawns", StringComparison.OrdinalIgnoreCase));
        if (spawnLayer == null)
        {
            _playerPosition = new Vector2(_worldBounds.Width, _worldBounds.Height) * 0.5f - _playerSize * 0.5f;
            return;
        }

        var objects = spawnLayer.objects ?? Array.Empty<TiledObject>();
        var playerSpawn = objects.FirstOrDefault(obj => string.Equals(obj.name, "Player", StringComparison.OrdinalIgnoreCase));
        if (playerSpawn == null)
        {
            _playerPosition = new Vector2(_worldBounds.Width, _worldBounds.Height) * 0.5f - _playerSize * 0.5f;
            return;
        }

        if (playerSpawn.width > 0f && playerSpawn.height > 0f)
        {
            var rectCenter = new Vector2(playerSpawn.x + playerSpawn.width * 0.5f, playerSpawn.y + playerSpawn.height * 0.5f);
            _playerPosition = rectCenter - _playerSize * 0.5f;
            return;
        }

        _playerPosition = new Vector2(playerSpawn.x, playerSpawn.y) - _playerSize * 0.5f;
    }

    private void BuildFallbackEnvironment()
    {
        const float wallThickness = 32f;
        const float arenaSize = 640f;
        _worldBounds = new RectangleF(-arenaSize * 0.5f, -arenaSize * 0.5f, arenaSize, arenaSize);

        var bounds = _worldBounds;
        _colliders.Add(new RectangleF(bounds.X - wallThickness, bounds.Y - wallThickness, bounds.Width + wallThickness * 2f, wallThickness)); // top
        _colliders.Add(new RectangleF(bounds.X - wallThickness, bounds.Bottom, bounds.Width + wallThickness * 2f, wallThickness)); // bottom
        _colliders.Add(new RectangleF(bounds.X - wallThickness, bounds.Y, wallThickness, bounds.Height)); // left
        _colliders.Add(new RectangleF(bounds.Right, bounds.Y, wallThickness, bounds.Height)); // right

        _colliders.Add(new RectangleF(bounds.X + 96f, bounds.Y + 96f, 128f, 32f));
        _colliders.Add(new RectangleF(bounds.X + 64f, bounds.Bottom - 160f, 192f, 32f));
        _playerPosition = new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f) - _playerSize * 0.5f;
    }

    private void TryMovePlayer(Vector2 delta)
    {
        if (delta == Vector2.Zero)
        {
            return;
        }

        var proposed = GetPlayerBounds();
        proposed.X += delta.X;
        proposed.Y += delta.Y;

        if (Collides(proposed))
        {
            return;
        }

        _playerPosition += delta;
        _playerPosition = ClampPositionToWorld(_playerPosition);
    }

    private void DisposeMap()
    {
        _map?.Dispose();
        _map = null;
    }

    private void FollowPlayer()
    {
        if (_camera == null)
        {
            return;
        }

        _camera.LookAt(GetPlayerCenter());
    }

    private RectangleF GetPlayerBounds()
        => new(_playerPosition, _playerSize);

    private Vector2 GetPlayerCenter()
        => _playerPosition + (_playerSize * 0.5f);

    private bool Collides(RectangleF bounds)
    {
        if (bounds.Left < _worldBounds.Left || bounds.Right > _worldBounds.Right ||
            bounds.Top < _worldBounds.Top || bounds.Bottom > _worldBounds.Bottom)
        {
            return true;
        }

        return _colliders.Any(rect => rect.Intersects(bounds));
    }

    private Vector2 ClampPositionToWorld(Vector2 position)
    {
        var min = new Vector2(_worldBounds.X, _worldBounds.Y);
        var max = new Vector2(_worldBounds.Right - _playerSize.X, _worldBounds.Bottom - _playerSize.Y);
        return Vector2.Clamp(position, min, max);
    }
}
