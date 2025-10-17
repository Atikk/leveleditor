using System;
using Avalonia.Media;
using DotGame.Runtime.Content;
using DotGame.Runtime.Rendering;
using DotGameAvalonia.Engine;
using DotGameAvalonia.Engine.Components;
using DotGameAvalonia.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.ViewportAdapters;
using MonoGame.Extended;
using AvaloniaColor = Avalonia.Media.Color;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace DotGameAvalonia.MonoGameLayer
{
    public sealed class EditorGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly ITextureResolver? _resolverOverride;
        private readonly object _mapLock = new();

        private Map _map;
        private AssetManager? _assets;
        private MapRenderer? _renderer;
        private ITextureResolver? _resolver;
        private GameWorld? _world;
        private SpriteBatch? _spriteBatch;
        private BoxingViewportAdapter? _viewportAdapter;
        private Texture2D? _whitePixel;
        private OrthographicCamera? _camera;
        private RuntimeTiledMap? _runtimeTiledMap;
        private Map? _pendingMap;
        private bool _mapDirty;
        private Entity? _playerEntity;
        private CameraController? _cameraController;

        public event Action<BehaviorTrigger, Entity>? TriggerActivated;

        public EditorGame(Map map, ITextureResolver? resolverOverride = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _resolverOverride = resolverOverride;
            Content.RootDirectory = "Content";

            int width = Math.Max(640, Math.Max(1, map.Cols) * Math.Max(1, map.TileW));
            int height = Math.Max(480, Math.Max(1, map.Rows) * Math.Max(1, map.TileH));

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = width,
                PreferredBackBufferHeight = height,
                SynchronizeWithVerticalRetrace = true
            };

            IsFixedTimeStep = false;
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            _assets = new AssetManager(Content, GraphicsDevice);
            _resolver = _resolverOverride ?? new FileTextureResolver(_assets);
            _renderer = new MapRenderer(GraphicsDevice, _resolver);

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _viewportAdapter = new BoxingViewportAdapter(Window, GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            _camera = new OrthographicCamera(_viewportAdapter);
            _whitePixel = new Texture2D(GraphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
            _cameraController = new CameraController(_camera, _viewportAdapter);
            Window.ClientSizeChanged += OnClientSizeChanged;

            _world = new GameWorld();
            BuildWorldFromMap();
            LoadExternalTileMap();
            UpdateCameraBounds(centerCamera: true);
        }

        protected override void Update(GameTime gameTime)
        {
            ApplyPendingMapSwap();
            var keyboard = Keyboard.GetState();
            var mouse = Mouse.GetState();
            _cameraController?.HandleInput(gameTime, keyboard, mouse);
            _world?.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new XnaColor(30, 30, 35));
            var viewMatrix = ComposeViewMatrix();

            if (_runtimeTiledMap != null && _spriteBatch != null)
            {
                _spriteBatch.Begin(transformMatrix: viewMatrix, samplerState: SamplerState.PointClamp);
                RuntimeTiledMapRenderer.DrawTileLayers(_spriteBatch, _runtimeTiledMap);
                _spriteBatch.End();
            }
            else
            {
                _renderer?.Draw(_map, Vector2.Zero, viewMatrix, includeActors: false);
            }

            if (_world != null && _spriteBatch != null)
            {
                _world.Draw(gameTime, _spriteBatch, viewMatrix);
            }

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Window.ClientSizeChanged -= OnClientSizeChanged;
                _assets?.Clear();
                _whitePixel?.Dispose();
                _runtimeTiledMap = null;
            }

            base.Dispose(disposing);
        }

        public void RequestMapSwap(Map mapSnapshot)
        {
            if (mapSnapshot == null)
                throw new ArgumentNullException(nameof(mapSnapshot));

            lock (_mapLock)
            {
                _pendingMap = mapSnapshot;
                _mapDirty = true;
            }
        }

        private void ApplyPendingMapSwap()
        {
            Map? nextMap = null;

            lock (_mapLock)
            {
                if (_mapDirty && _pendingMap != null)
                {
                    nextMap = _pendingMap;
                    _pendingMap = null;
                    _mapDirty = false;
                }
            }

            if (nextMap == null)
                return;

            _map = nextMap;
            _assets?.Clear();
            BuildWorldFromMap();
            LoadExternalTileMap();
            UpdateCameraBounds(centerCamera: true);
        }

        private void LoadExternalTileMap()
        {
            _runtimeTiledMap = null;

            if (_assets == null)
                return;

            if (string.IsNullOrWhiteSpace(_map.ExternalTileMapAsset))
                return;

            try
            {
                _runtimeTiledMap = _assets.GetRuntimeTiledMap(_map.ExternalTileMapAsset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load tiled map '{_map.ExternalTileMapAsset}': {ex.Message}");
                _runtimeTiledMap = null;
            }
        }

        private Matrix ComposeViewMatrix()
        {
            if (_camera != null)
                return _camera.GetViewMatrix();

            return Matrix.Identity;
        }

        private void BuildWorldFromMap()
        {
            if (_world == null || _whitePixel == null)
                return;

            _world.Clear();
            _playerEntity = null;

            var characters = _map.Characters;
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                var entity = new Entity(character.Name);
                entity.Transform.Position = new Vector2(character.TileX * _map.TileW, character.TileY * _map.TileH);

                var tint = ToXnaColor(character.Color, XnaColor.DeepSkyBlue);
                var sprite = new SpriteComponent
                {
                    Texture = _whitePixel,
                    Tint = tint,
                    SizeOverride = new Vector2(_map.TileW, _map.TileH)
                };

                entity.AddComponent(sprite);
                entity.AddComponent(new MovementComponent());
                entity.AddComponent(new ColliderComponent
                {
                    Size = new Vector2(_map.TileW, _map.TileH)
                });

                if (IsPlayerCharacter(character, i))
                {
                    entity.AddComponent(new PlayerTagComponent());
                    _playerEntity = entity;
                }

                _world.AddEntity(entity);
            }

            foreach (var doodad in _map.Doodads)
            {
                var entity = new Entity(doodad.Type);
                entity.Transform.Position = new Vector2(doodad.TileX * _map.TileW, doodad.TileY * _map.TileH);

                var avaloniaColor = doodad.Color;
                if (avaloniaColor.A == 0)
                    avaloniaColor = Colors.SaddleBrown;

                var tint = ToXnaColor(avaloniaColor, XnaColor.SaddleBrown);
                var sprite = new SpriteComponent
                {
                    Texture = _whitePixel,
                    Tint = tint,
                    SizeOverride = new Vector2(_map.TileW, _map.TileH)
                };

                entity.AddComponent(sprite);
                if (doodad.Collidable)
                {
                    entity.AddComponent(new ColliderComponent
                    {
                        Size = new Vector2(_map.TileW, _map.TileH),
                        IsStatic = true
                    });
                }
                _world.AddEntity(entity);
            }

            foreach (var trigger in _map.Triggers)
            {
                var name = string.IsNullOrWhiteSpace(trigger.Name) ? "Trigger" : trigger.Name;
                var entity = new Entity(name);
                entity.Transform.Position = new Vector2(trigger.TileX * _map.TileW, trigger.TileY * _map.TileH);

                var sprite = new SpriteComponent
                {
                    Texture = _whitePixel,
                    Tint = new XnaColor(255, 215, 0, 96),
                    SizeOverride = new Vector2(_map.TileW, _map.TileH)
                };

                entity.AddComponent(sprite);
                var triggerComponent = entity.AddComponent(new TriggerComponent(trigger));
                var collider = entity.AddComponent(new ColliderComponent
                {
                    Size = new Vector2(_map.TileW, _map.TileH),
                    IsTrigger = true
                });

                collider.Collision += other => HandleTriggerCollision(triggerComponent, collider, other);

                _world.AddEntity(entity);
            }
        }

        private void UpdateCameraBounds(bool centerCamera)
        {
            var width = Math.Max(1, _map.Cols * _map.TileW);
            var height = Math.Max(1, _map.Rows * _map.TileH);
            var bounds = new RectangleF(0, 0, width, height);
            _cameraController?.SetWorldBounds(bounds, centerCamera);
        }

        private void OnClientSizeChanged(object? sender, EventArgs e)
        {
            _cameraController?.HandleViewportResize();
        }

        private bool IsPlayerCharacter(Character character, int index)
        {
            if (index == 0)
                return true;

            if (string.IsNullOrWhiteSpace(character.Name))
                return false;

            var name = character.Name.Trim();
            return name.Equals("player", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("hero", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("main", StringComparison.OrdinalIgnoreCase);
        }

        private void HandleTriggerCollision(TriggerComponent triggerComponent, ColliderComponent triggerCollider, Entity other)
        {
            if (!triggerCollider.Enabled)
                return;

            if (!IsPlayerEntity(other))
                return;

            triggerCollider.Enabled = false;
            TriggerActivated?.Invoke(triggerComponent.Trigger, other);
            Console.WriteLine($"Trigger '{triggerComponent.Trigger.Name}' activated by '{other.Name}'.");
        }

        private bool IsPlayerEntity(Entity entity)
        {
            if (_playerEntity != null && ReferenceEquals(_playerEntity, entity))
                return true;

            return entity.GetComponent<PlayerTagComponent>() != null;
        }

        private static XnaColor ToXnaColor(AvaloniaColor color, XnaColor fallback)
        {
            if (color.A == 0 && fallback.A != 0)
            {
                return fallback;
            }

            var alpha = color.A == 0 ? fallback.A : color.A;
            return new XnaColor(color.R, color.G, color.B, alpha);
        }
    }
}
