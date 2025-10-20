using System;
using System.Collections.Generic;
using global::Avalonia.Media;
using DotGame.Core.Async;
using DotGame.Core.Resources;
using DotGame.Core.Async.Jobs;
using DotGame.Runtime.Content;
using DotGame.Runtime.Rendering;
using Dotgame.Avalonia.Engine;
using Dotgame.Avalonia.Engine.Components;
using Dotgame.Avalonia.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.ViewportAdapters;
using MonoGame.Extended;
using AvaloniaColor = Avalonia.Media.Color;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Dotgame.Avalonia.MonoGameLayer
{
	public sealed class EditorGame : Game
	{
		private readonly GraphicsDeviceManager _graphics;
		private readonly ITextureResolver? _resolverOverride;
		private readonly AsyncTaskScheduler _scheduler;
		private readonly ResourceManager _resourceManager;
		private readonly bool _ownsScheduler;
		private readonly bool _ownsResourceManager;
		private readonly IJobSystem _jobSystem;
		private readonly bool _ownsJobSystem;
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
		private Entity? _enemyEntity;
		private HealthComponent? _playerHealth;
		private HealthComponent? _enemyHealth;
		private float _playerAttackCooldownTimer;
		private float _playerAttackRange = 48f;
		private bool _playerDefeated;
		private Entity? _rangedEnemyEntity;
		private HealthComponent? _rangedEnemyHealth;
		private float _rangedEnemyAttackTimer;
		private float _rangedEnemyAttackRange = 320f;
		private readonly List<Entity> _testObstacles = new();
		private bool _loggedMovementInput;
		private bool _loggedFirstKey;
		private bool _loggedFallbackSpawn;
		private long _lastUpdateLogTick;
		private long _lastDrawLogTick;
		private KeyboardState _previousKeyboardState;
		private readonly bool _testModeEnabled = true;

		private const float PlayerMoveSpeed = 260f;
		private const float PlayerAttackDamage = 24f;
		private const float PlayerAttackCooldownSeconds = 0.45f;
		private const float EnemyBaseHealth = 140f;
		private const float PlayerBaseHealth = 120f;
		private const float EnemyAttackDamage = 10f;
		private const float RangedEnemyBaseHealth = 90f;
		private const float RangedEnemyAttackDamage = 12f;
		private const float RangedEnemyAttackIntervalSeconds = 1.75f;
		private const float RangedEnemyProjectileSpeed = 360f;
		private const float RangedEnemyProjectileLifetime = 3.25f;

		public event Action<BehaviorTrigger, Entity>? TriggerActivated;

		public EditorGame(Map map, ITextureResolver? resolverOverride = null, AsyncTaskScheduler? schedulerOverride = null, ResourceManager? resourceManagerOverride = null, IJobSystem? jobSystemOverride = null)
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
			_jobSystem = jobSystemOverride ?? new AsyncTaskJobSystem(workerCount: 1, workerNamePrefix: "EditorJob-");
			_ownsJobSystem = jobSystemOverride == null;
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
				UpdatePlayerCombat(gameTime, keyboard);
				UpdateRangedEnemyAttack(gameTime);
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

				DrawTestHud(gameTime);

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

				if (_ownsJobSystem)
				{
					_jobSystem.Dispose();
				}
			}

			base.Dispose(disposing);
		}

			public IJobSystem JobSystem => _jobSystem;

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

			CleanupTestHooks();
			_world.Clear();
			_playerEntity = null;
			_enemyEntity = null;
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

			if (_testModeEnabled)
			{
				SetupTestScenario();
			}
		}

		private void UpdatePlayerCombat(GameTime gameTime, KeyboardState keyboard)
		{
			if (!_testModeEnabled)
			{
				_previousKeyboardState = keyboard;
				return;
			}

			var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (dt > 0f)
			{
				_playerAttackCooldownTimer = MathF.Max(0f, _playerAttackCooldownTimer - dt);
			}

			var attackPressed = keyboard.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);
			_previousKeyboardState = keyboard;

			if (!attackPressed)
				return;

			TryPerformPlayerAttack();
		}

		private void TryPerformPlayerAttack()
		{
			if (_playerDefeated)
				return;

			if (_playerAttackCooldownTimer > 0f)
				return;

			if (_playerEntity == null)
				return;

			var playerCollider = _playerEntity.GetComponent<ColliderComponent>();
			var playerCenter = _playerEntity.Transform.Position + (playerCollider?.Size ?? new Vector2(_map.TileW, _map.TileH)) * 0.5f;

			var targets = new (Entity? entity, HealthComponent? health)[]
			{
				(_enemyEntity, _enemyHealth),
				(_rangedEnemyEntity, _rangedEnemyHealth)
			};

			var hitTarget = false;

			for (var i = 0; i < targets.Length; i++)
			{
				var entity = targets[i].entity;
				var health = targets[i].health;
				if (entity == null || health == null || !health.IsAlive)
					continue;

				var collider = entity.GetComponent<ColliderComponent>();
				var entityCenter = entity.Transform.Position + (collider?.Size ?? new Vector2(_map.TileW, _map.TileH)) * 0.5f;
				var distance = Vector2.Distance(playerCenter, entityCenter);
				if (distance > _playerAttackRange)
					continue;

				health.ApplyDamage(PlayerAttackDamage);
				hitTarget = true;

				var enemyAi = entity.GetComponent<EnemyAIComponent>();
				enemyAi?.ForceCooldown(PlayerAttackCooldownSeconds * 0.5f);
			}

			if (hitTarget)
			{
				_playerAttackCooldownTimer = PlayerAttackCooldownSeconds;
			}
		}

		private void SetupTestScenario()
		{
			if (_world == null || _playerEntity == null)
				return;

			EnsurePlayerTestComponents();
			EnsureEnemyTestEntity();
			EnsureRangedEnemyEntity();
			EnsureObstacleEntities();
		}

		private void EnsurePlayerTestComponents()
		{
			var movement = _playerEntity!.GetComponent<MovementComponent>();
			if (movement != null)
			{
				movement.MaxSpeed = PlayerMoveSpeed;
				movement.Enabled = true;
			}

			_playerHealth = _playerEntity.GetComponent<HealthComponent>() ?? _playerEntity.AddComponent(new HealthComponent());
			_playerHealth.Reset(PlayerBaseHealth);
			_playerHealth.Died -= OnPlayerDied;
			_playerHealth.Died += OnPlayerDied;
			_playerDefeated = false;

			var playerSprite = _playerEntity.GetComponent<SpriteComponent>();
			if (playerSprite != null)
			{
				playerSprite.Tint = new XnaColor(72, 191, 255, 200);
			}

			_playerAttackRange = MathF.Max(24f, MathF.Min(_map.TileW, _map.TileH) * 0.85f);
		}

		private void EnsureEnemyTestEntity()
		{
			if (_world == null || _playerEntity == null)
				return;

			if (_enemyEntity != null)
			{
				if (_enemyHealth != null)
				{
					_enemyHealth.Died -= OnEnemyDied;
				}
				_world.RemoveEntity(_enemyEntity);
			}

			var enemySpawn = _playerEntity.Transform.Position + new Vector2(_map.TileW * 3f, 0f);
			enemySpawn = ClampToMap(enemySpawn, new Vector2(_map.TileW, _map.TileH));
			var enemy = new Entity("TestEnemy");
			enemy.Transform.Position = enemySpawn;

			var sprite = new SpriteComponent
			{
				Texture = _whitePixel,
				Tint = new XnaColor(220, 70, 70, 220),
				SizeOverride = new Vector2(_map.TileW, _map.TileH)
			};

			enemy.AddComponent(sprite);
			enemy.AddComponent(new MovementComponent
			{
				MaxSpeed = PlayerMoveSpeed * 0.75f
			});
			enemy.AddComponent(new ColliderComponent
			{
				Size = new Vector2(_map.TileW, _map.TileH)
			});

			var health = enemy.AddComponent(new HealthComponent
			{
				MaxHealth = EnemyBaseHealth
			});
			health.Reset();
			health.Died -= OnEnemyDied;
			health.Died += OnEnemyDied;

			var ai = enemy.AddComponent(new EnemyAIComponent
			{
				Target = _playerEntity,
				MoveSpeed = PlayerMoveSpeed * 0.7f,
				AttackDamage = EnemyAttackDamage,
				AttackCooldownSeconds = 1.1f,
				AttackRange = MathF.Max(24f, MathF.Min(_map.TileW, _map.TileH) * 0.75f)
			});

			_world.AddEntity(enemy);
			_enemyEntity = enemy;
			_enemyHealth = health;
		}

		private void EnsureRangedEnemyEntity()
		{
			if (_world == null || _playerEntity == null)
				return;

			if (_rangedEnemyEntity != null)
			{
				if (_rangedEnemyHealth != null)
				{
					_rangedEnemyHealth.Died -= OnEnemyDied;
				}
				_world.RemoveEntity(_rangedEnemyEntity);
			}

			var spawnOffset = new Vector2(-_map.TileW * 4f, _map.TileH * 2f);
			var spawn = ClampToMap(_playerEntity.Transform.Position + spawnOffset, new Vector2(_map.TileW, _map.TileH));
			var ranged = new Entity("TestRangedEnemy");
			ranged.Transform.Position = spawn;

			var sprite = new SpriteComponent
			{
				Texture = _whitePixel,
				Tint = new XnaColor(255, 180, 90, 220),
				SizeOverride = new Vector2(_map.TileW, _map.TileH)
			};

			ranged.AddComponent(sprite);
			ranged.AddComponent(new ColliderComponent
			{
				Size = new Vector2(_map.TileW, _map.TileH)
			});

			var health = ranged.AddComponent(new HealthComponent
			{
				MaxHealth = RangedEnemyBaseHealth
			});
			health.Reset();
			health.Died -= OnEnemyDied;
			health.Died += OnEnemyDied;

			_world.AddEntity(ranged);
			_rangedEnemyEntity = ranged;
			_rangedEnemyHealth = health;
			_rangedEnemyAttackTimer = RangedEnemyAttackIntervalSeconds * 0.5f;
			_rangedEnemyAttackRange = MathF.Max(180f, MathF.Min(_map.Cols * _map.TileW, _map.Rows * _map.TileH) * 0.35f);
		}

		private void EnsureObstacleEntities()
		{
			if (_world == null || _whitePixel == null || _playerEntity == null)
				return;

			if (_testObstacles.Count > 0)
			{
				for (var i = 0; i < _testObstacles.Count; i++)
				{
					_world.RemoveEntity(_testObstacles[i]);
				}
				_testObstacles.Clear();
			}

			var obstacleSize = new Vector2(_map.TileW, _map.TileH);
			var basePosition = ClampToMap(_playerEntity.Transform.Position + new Vector2(_map.TileW * 2f, _map.TileH), obstacleSize);
			var offsets = new[]
			{
				Vector2.Zero,
				new Vector2(_map.TileW, 0f),
				new Vector2(0f, _map.TileH)
			};

			for (var i = 0; i < offsets.Length; i++)
			{
				var obstacle = new Entity($"TestObstacle_{i + 1}");
				obstacle.Transform.Position = ClampToMap(basePosition + offsets[i], obstacleSize);

				var sprite = new SpriteComponent
				{
					Texture = _whitePixel,
					Tint = new XnaColor(120, 130, 140, 220),
					SizeOverride = obstacleSize
				};

				obstacle.AddComponent(sprite);
				obstacle.AddComponent(new ColliderComponent
				{
					Size = obstacleSize,
					IsStatic = true
				});

				_world.AddEntity(obstacle);
				_testObstacles.Add(obstacle);
			}
		}

		private void UpdateRangedEnemyAttack(GameTime gameTime)
		{
			if (!_testModeEnabled)
				return;

			var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (dt > 0f)
			{
				_rangedEnemyAttackTimer = MathF.Max(0f, _rangedEnemyAttackTimer - dt);
			}

			if (_rangedEnemyEntity == null || _rangedEnemyHealth == null || !_rangedEnemyHealth.IsAlive)
				return;

			if (_rangedEnemyAttackTimer > 0f)
				return;

			if (_playerEntity == null || _playerHealth == null || !_playerHealth.IsAlive)
				return;

			var shooterCollider = _rangedEnemyEntity.GetComponent<ColliderComponent>();
			var playerCollider = _playerEntity.GetComponent<ColliderComponent>();

			var shooterCenter = _rangedEnemyEntity.Transform.Position + (shooterCollider?.Size ?? new Vector2(_map.TileW, _map.TileH)) * 0.5f;
			var playerCenter = _playerEntity.Transform.Position + (playerCollider?.Size ?? new Vector2(_map.TileW, _map.TileH)) * 0.5f;

			var toPlayer = playerCenter - shooterCenter;
			var distance = toPlayer.Length();
			if (distance <= 0.01f || distance > _rangedEnemyAttackRange)
			{
				_rangedEnemyAttackTimer = RangedEnemyAttackIntervalSeconds * 0.5f;
				return;
			}

			toPlayer /= distance;
			SpawnProjectile(_rangedEnemyEntity, shooterCenter, toPlayer);
			_rangedEnemyAttackTimer = RangedEnemyAttackIntervalSeconds;
		}

		private void SpawnProjectile(Entity source, Vector2 origin, Vector2 direction)
		{
			if (_world == null || _whitePixel == null)
				return;

			if (direction == Vector2.Zero)
				return;

			var size = new Vector2(MathF.Max(6f, _map.TileW * 0.25f), MathF.Max(6f, _map.TileH * 0.25f));
			var projectile = new Entity("TestProjectile");
			var launchOffset = MathF.Max(_map.TileW, _map.TileH) * 0.4f;
			var start = origin + direction * launchOffset;
			projectile.Transform.Position = start - size * 0.5f;

			var sprite = new SpriteComponent
			{
				Texture = _whitePixel,
				Tint = new XnaColor(255, 220, 80, 210),
				SizeOverride = size
			};

			projectile.AddComponent(sprite);
			projectile.AddComponent(new ColliderComponent
			{
				Size = size,
				IsTrigger = true
			});
			projectile.AddComponent(new ProjectileComponent
			{
				Velocity = direction * RangedEnemyProjectileSpeed,
				Lifetime = RangedEnemyProjectileLifetime,
				Damage = RangedEnemyAttackDamage,
				Source = source
			});

			_world.AddEntity(projectile);
		}

		private void OnPlayerDied(HealthComponent health)
		{
			_playerDefeated = true;
			if (_playerEntity == null)
				return;

			var movement = _playerEntity.GetComponent<MovementComponent>();
			if (movement != null)
			{
				movement.Stop();
				movement.Enabled = false;
			}

			var sprite = _playerEntity.GetComponent<SpriteComponent>();
			if (sprite != null)
			{
				sprite.Tint = new XnaColor(60, 80, 120, 180);
			}

			if (_enemyEntity != null)
			{
				var ai = _enemyEntity.GetComponent<EnemyAIComponent>();
				if (ai != null)
				{
					ai.Target = null;
				}
			}

			if (_rangedEnemyEntity != null)
			{
				_rangedEnemyAttackTimer = RangedEnemyAttackIntervalSeconds;
			}
		}

		private void OnEnemyDied(HealthComponent health)
		{
			var owner = health.Owner;
			if (owner == null)
				return;

			if (_enemyEntity != null && ReferenceEquals(owner, _enemyEntity))
			{
				HandleDefeatedEntity(_enemyEntity, new XnaColor(160, 70, 70, 120));
				return;
			}

			if (_rangedEnemyEntity != null && ReferenceEquals(owner, _rangedEnemyEntity))
			{
				_rangedEnemyAttackTimer = RangedEnemyAttackIntervalSeconds;
				HandleDefeatedEntity(_rangedEnemyEntity, new XnaColor(170, 120, 60, 120));
			}
		}

		private void HandleDefeatedEntity(Entity entity, XnaColor tint)
		{
			var movement = entity.GetComponent<MovementComponent>();
			movement?.Stop();
			if (movement != null)
			{
				movement.Enabled = false;
			}

			var collider = entity.GetComponent<ColliderComponent>();
			if (collider != null)
			{
				collider.Enabled = false;
			}

			var ai = entity.GetComponent<EnemyAIComponent>();
			if (ai != null)
			{
				ai.Enabled = false;
			}

			var sprite = entity.GetComponent<SpriteComponent>();
			if (sprite != null)
			{
				sprite.Tint = tint;
			}
		}

		private void CleanupTestHooks()
		{
			if (_playerHealth != null)
			{
				_playerHealth.Died -= OnPlayerDied;
			}

			if (_enemyHealth != null)
			{
				_enemyHealth.Died -= OnEnemyDied;
			}

			if (_rangedEnemyHealth != null)
			{
				_rangedEnemyHealth.Died -= OnEnemyDied;
			}

			if (_world != null && _testObstacles.Count > 0)
			{
				for (var i = 0; i < _testObstacles.Count; i++)
				{
					_world.RemoveEntity(_testObstacles[i]);
				}
			}

			_testObstacles.Clear();

			_playerHealth = null;
			_enemyHealth = null;
			_enemyEntity = null;
			_rangedEnemyHealth = null;
			_rangedEnemyEntity = null;
			_playerDefeated = false;
			_playerAttackCooldownTimer = 0f;
			_rangedEnemyAttackTimer = 0f;
		}

		private void DrawTestHud(GameTime gameTime)
		{
			if (!_testModeEnabled || _spriteBatch == null || _whitePixel == null)
				return;

			_spriteBatch.Begin(samplerState: SamplerState.PointClamp);
			var origin = new Vector2(16f, 16f);
			DrawHealthBar(origin, 220, 16, _playerHealth, new XnaColor(60, 190, 255, 220));
			DrawHealthBar(origin + new Vector2(0f, 24f), 220, 16, _enemyHealth, new XnaColor(255, 90, 90, 220));
			DrawHealthBar(origin + new Vector2(0f, 48f), 220, 16, _rangedEnemyHealth, new XnaColor(255, 180, 90, 220));
			_spriteBatch.End();
		}

		private void DrawHealthBar(Vector2 position, int width, int height, HealthComponent? health, XnaColor fillColor)
		{
			if (health == null || _spriteBatch == null || _whitePixel == null)
				return;

			var ratio = health.MaxHealth <= 0f ? 0f : health.CurrentHealth / health.MaxHealth;
			ratio = MathHelper.Clamp(ratio, 0f, 1f);
			var background = new Rectangle((int)position.X, (int)position.Y, width, height);
			_spriteBatch.Draw(_whitePixel, background, new XnaColor(0, 0, 0, 180));
			var fillWidth = MathF.Ceiling((width - 2) * ratio);
			var fill = new Rectangle(background.X + 1, background.Y + 1, (int)fillWidth, height - 2);
			_spriteBatch.Draw(_whitePixel, fill, fillColor);
			var border = new Rectangle(background.X, background.Y, background.Width, 1);
			_spriteBatch.Draw(_whitePixel, border, new XnaColor(255, 255, 255, 40));
			border.Y = background.Bottom - 1;
			_spriteBatch.Draw(_whitePixel, border, new XnaColor(255, 255, 255, 40));
		}

		private Vector2 ClampToMap(Vector2 position, Vector2 size)
		{
			var maxX = MathF.Max(0f, _map.Cols * _map.TileW - size.X);
			var maxY = MathF.Max(0f, _map.Rows * _map.TileH - size.Y);
			return new Vector2(MathHelper.Clamp(position.X, 0f, maxX), MathHelper.Clamp(position.Y, 0f, maxY));
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


