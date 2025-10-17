using System;
using Avalonia.Media;
using DotGame.Core.Async;
using DotGame.Core.Resources;
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
		private readonly AsyncTaskScheduler _scheduler;
		private readonly ResourceManager _resourceManager;
		private readonly bool _ownsScheduler;
		private readonly bool _ownsResourceManager;
		private readonly object _mapLock = new();

		private Map _map = null!;
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
		private RectangleF _cameraWorldBounds = RectangleF.Empty;
		private bool _loggedMovementInput;
		private bool _loggedFirstKey;
		private bool _loggedFallbackSpawn;
		private long _lastUpdateLogTick;
		private long _lastDrawLogTick;

		private const float PlayerMoveSpeed = 260f;

		public event Action<BehaviorTrigger, Entity>? TriggerActivated;

		public EditorGame(Map map, ITextureResolver? resolverOverride = null, AsyncTaskScheduler? schedulerOverride = null, ResourceManager? resourceManagerOverride = null)
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

			_scheduler = schedulerOverride ?? new AsyncTaskScheduler(workerCount: 1, workerNamePrefix: "EditorGameWorker-");
			_resourceManager = resourceManagerOverride ?? new ResourceManager(_scheduler);
			_ownsScheduler = schedulerOverride == null;
			_ownsResourceManager = resourceManagerOverride == null;

			LogStatus($"EditorGame constructed with map snapshot dimensions {map.Cols}x{map.Rows}.");
		}

		protected override void LoadContent()
		{
			try
			{
				LogStatus("LoadContent entering.");
				base.LoadContent();

				_assets = new AssetManager(Content, GraphicsDevice, _resourceManager);
				_resolver = _resolverOverride ?? new FileTextureResolver(_assets);
				_renderer = new MapRenderer(GraphicsDevice, _resolver);

				_spriteBatch = new SpriteBatch(GraphicsDevice);
				_viewportAdapter = new BoxingViewportAdapter(Window, GraphicsDevice, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
				_camera = new OrthographicCamera(_viewportAdapter);
				_whitePixel = new Texture2D(GraphicsDevice, 1, 1);
				_whitePixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
				_cameraController = new CameraController(_camera, _viewportAdapter)
				{
					AllowKeyboardPan = false
				};
				Window.ClientSizeChanged += OnClientSizeChanged;

				_world = new GameWorld();
				BuildWorldFromMap();
				LoadExternalTileMap();
				UpdateCameraBounds(centerCamera: true);
				SnapCameraToPlayer();
				LogStatus("LoadContent completed; world and renderer ready.");
			}
			catch (Exception ex)
			{
				LogStatus($"LoadContent exception: {ex}");
				throw;
			}
		}

		protected override void Update(GameTime gameTime)
		{
			try
			{
				_resourceManager.PumpMainThread();
				ApplyPendingMapSwap();
				var keyboard = Keyboard.GetState();
				var mouse = Mouse.GetState();
				UpdatePlayerMovement(keyboard);
				_cameraController?.HandleInput(gameTime, keyboard, mouse);
				_world?.Update(gameTime);
				UpdateCameraFollow(gameTime);
				base.Update(gameTime);
				LogRare(ref _lastUpdateLogTick, 1000, "Update loop running; input and camera handlers active.");
			}
			catch (Exception ex)
			{
				LogStatus($"Update exception: {ex}");
				throw;
			}
		}

		protected override void Draw(GameTime gameTime)
		{
			try
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
				LogRare(ref _lastDrawLogTick, 1000, "Draw loop running.");
			}
			catch (Exception ex)
			{
				LogStatus($"Draw exception: {ex}");
				throw;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				Window.ClientSizeChanged -= OnClientSizeChanged;
				_assets?.Clear();
				_whitePixel?.Dispose();
				_runtimeTiledMap = null;

				if (_ownsResourceManager)
				{
					_resourceManager.Dispose();
				}

				if (_ownsScheduler)
				{
					_scheduler.Dispose();
				}
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

			LogStatus($"Map swap requested for snapshot dimensions {mapSnapshot.Cols}x{mapSnapshot.Rows}.");
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
			SnapCameraToPlayer();
			LogStatus("Pending map swap applied and world rebuilt.");
		}

		private void LoadExternalTileMap()
		{
			_runtimeTiledMap = null;

			if (_assets == null)
				return;

			if (string.IsNullOrWhiteSpace(_map.ExternalTileMapAsset))
				return;

			_assets.RequestRuntimeTiledMap(
				_map.ExternalTileMapAsset,
				map => _runtimeTiledMap = map,
				ex =>
				{
					Console.WriteLine($"Failed to load tiled map '{_map.ExternalTileMapAsset}': {ex.Message}");
					_runtimeTiledMap = null;
				});

			LogStatus($"Requested runtime tiled map '{_map.ExternalTileMapAsset}'.");
		}

		private void UpdatePlayerMovement(KeyboardState keyboard)
		{
			if (_world == null)
				return;

			var input = Vector2.Zero;

			if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
				input.Y -= 1f;
			if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
				input.Y += 1f;
			if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
				input.X -= 1f;
			if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
				input.X += 1f;

			if (input != Vector2.Zero)
				input.Normalize();

			var velocity = input * PlayerMoveSpeed;

			if (!_loggedMovementInput)
			{
				_loggedMovementInput = true;
				var pressed = keyboard.GetPressedKeys();
				var pressedSummary = pressed.Length == 0 ? "<none>" : string.Join(',', pressed);
				LogStatus($"Processing keyboard input for player movement. Initial pressed keys: {pressedSummary}.");
			}

			if (input != Vector2.Zero && !_loggedFirstKey)
			{
				_loggedFirstKey = true;
				LogStatus($"Detected movement input vector {input}.");
			}

			foreach (var entity in _world.Entities)
			{
				if (entity.GetComponent<PlayerTagComponent>() == null)
					continue;

				var movement = entity.GetComponent<MovementComponent>();
				if (movement == null)
					continue;

				movement.Velocity = velocity;
			}
		}

		private void UpdateCameraFollow(GameTime gameTime)
		{
			if (_camera == null)
				return;

			var player = TryGetPlayerEntity();
			if (player == null)
				return;

			var playerCenter = player.Transform.Position + new Vector2(_map.TileW * 0.5f, _map.TileH * 0.5f);
			var current = _camera.Position;
			var lerpFactor = MathHelper.Clamp((float)gameTime.ElapsedGameTime.TotalSeconds * 10f, 0f, 1f);
			_camera.Position = Vector2.Lerp(current, playerCenter, lerpFactor);

			if (_cameraController != null && _cameraWorldBounds != RectangleF.Empty)
			{
				_cameraController.SetWorldBounds(_cameraWorldBounds, centerCamera: false);
			}
		}

		private Entity? TryGetPlayerEntity()
		{
			if (_playerEntity != null)
				return _playerEntity;

			if (_world == null)
				return null;

			foreach (var entity in _world.Entities)
			{
				if (entity.GetComponent<PlayerTagComponent>() != null)
					return entity;
			}

			return null;
		}

		private void SnapCameraToPlayer()
		{
			if (_camera == null)
				return;

			var player = TryGetPlayerEntity();
			if (player == null)
				return;

			var playerCenter = player.Transform.Position + new Vector2(_map.TileW * 0.5f, _map.TileH * 0.5f);
			_camera.Position = playerCenter;

			if (_cameraController != null && _cameraWorldBounds != RectangleF.Empty)
			{
				_cameraController.SetWorldBounds(_cameraWorldBounds, centerCamera: false);
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
			_loggedFallbackSpawn = false;

			var characters = _map.Characters;
			var doodads = _map.Doodads;
			var triggers = _map.Triggers;

			LogStatus($"BuildWorldFromMap starting. Characters={characters.Count}, Doodads={doodads.Count}, Triggers={triggers.Count}.");
			if (characters.Count == 0)
			{
				LogStatus("Map contains no characters; relying on fallback player.");
			}

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
				entity.AddComponent(new MovementComponent
				{
					MaxSpeed = PlayerMoveSpeed
				});
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

			if (doodads.Count == 0)
			{
				LogStatus("Map contains no doodads to build.");
			}

			foreach (var doodad in doodads)
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

			if (triggers.Count == 0)
			{
				LogStatus("Map contains no triggers to build.");
			}

			foreach (var trigger in triggers)
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

			if (_playerEntity == null)
			{
				var spawn = new Vector2(
					MathF.Max(0f, (_map.Cols * _map.TileW) * 0.5f - _map.TileW * 0.5f),
					MathF.Max(0f, (_map.Rows * _map.TileH) * 0.5f - _map.TileH * 0.5f));

				var entity = new Entity("PlayerPreview");
				entity.Transform.Position = spawn;

				var sprite = new SpriteComponent
				{
					Texture = _whitePixel,
					Tint = new XnaColor(72, 191, 255, 200),
					SizeOverride = new Vector2(_map.TileW, _map.TileH)
				};

				entity.AddComponent(sprite);
				entity.AddComponent(new MovementComponent
				{
					MaxSpeed = PlayerMoveSpeed
				});
				entity.AddComponent(new ColliderComponent
				{
					Size = new Vector2(_map.TileW, _map.TileH)
				});
				entity.AddComponent(new PlayerTagComponent());

				_world.AddEntity(entity);
				_playerEntity = entity;

				if (!_loggedFallbackSpawn)
				{
					_loggedFallbackSpawn = true;
					LogStatus("No player character detected; spawned fallback PlayerPreview entity.");
				}
			}

			LogStatus($"World built with {characters.Count} character(s), {doodads.Count} doodad(s), {triggers.Count} trigger(s).");
		}

		private void UpdateCameraBounds(bool centerCamera)
		{
			var width = Math.Max(1, _map.Cols * _map.TileW);
			var height = Math.Max(1, _map.Rows * _map.TileH);
			var bounds = new RectangleF(0, 0, width, height);
			_cameraWorldBounds = bounds;
			_cameraController?.SetWorldBounds(bounds, centerCamera);
		}

		private void OnClientSizeChanged(object? sender, EventArgs e)
		{
			_cameraController?.HandleViewportResize();
			LogStatus("Client size changed; viewport adapter notified.");
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

		private static void LogRare(ref long lastTick, int intervalMs, string message)
		{
			var now = Environment.TickCount64;
			if (lastTick == 0 || now - lastTick >= intervalMs)
			{
				lastTick = now;
				LogStatus(message);
			}
		}

		private static void LogStatus(string message)
		{
			var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
			Console.WriteLine($"[EditorGame] {timestamp} {message}");
		}
	}
}
