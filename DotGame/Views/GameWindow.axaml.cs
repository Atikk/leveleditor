using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DotGameAvalonia.Models;
using SkiaSharp;

namespace DotGameAvalonia.Views
{
    public partial class GameWindow : Window
    {
        private Map? map;
        private Character? player;
        private List<Monster> monsters = new();
        private Canvas? gameCanvas;
        private DispatcherTimer? timer;
        private CombatManager? combatManager;
        private bool playerMovedThisFrame = false;

        private Border? combatUI;
        private TextBlock? txtCombatMessage;
        private TextBlock? txtPlayerInfo;
        private TextBlock? txtEnemyInfo;
        private ProgressBar? barPlayerHP;
        private ProgressBar? barEnemyHP;
        private TextBlock? txtPlayerStats;
        private Button? btnAttack;
        private Button? btnDefend;

        public GameWindow() : this("", null, CharacterClass.Warrior, "Hero")
        {
        }

        public GameWindow(string mapPath, Bitmap? playerSprite, CharacterClass charClass = CharacterClass.Warrior, string charName = "Hero")
        {
            InitializeComponent();
            AttachEvents();
            if (!string.IsNullOrEmpty(mapPath))
            {
                LoadGame(mapPath, playerSprite, charClass, charName);
            }
        }

        private void AttachEvents()
        {
            gameCanvas = this.FindControl<Canvas>("GameCanvas");
            combatUI = this.FindControl<Border>("CombatUI");
            txtCombatMessage = this.FindControl<TextBlock>("TxtCombatMessage");
            txtPlayerInfo = this.FindControl<TextBlock>("TxtPlayerInfo");
            txtEnemyInfo = this.FindControl<TextBlock>("TxtEnemyInfo");
            barPlayerHP = this.FindControl<ProgressBar>("BarPlayerHP");
            barEnemyHP = this.FindControl<ProgressBar>("BarEnemyHP");
            txtPlayerStats = this.FindControl<TextBlock>("TxtPlayerStats");
            btnAttack = this.FindControl<Button>("BtnAttack");
            btnDefend = this.FindControl<Button>("BtnDefend");

            KeyDown += GameWindow_KeyDown;

            if (btnAttack != null)
                btnAttack.Click += (s, e) => HandlePlayerAttack();
            
            if (btnDefend != null)
                btnDefend.Click += (s, e) => HandlePlayerDefend();
        }

        private void LoadGame(string mapPath, Bitmap? playerSprite, CharacterClass charClass, string charName)
        {
            try
            {
                map = Map.LoadFromJson(mapPath);
                InitPlayer(playerSprite, charClass, charName);
                SpawnMonsters();
                FitWindowToMap();
                RenderGame();
                
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0) };
                timer.Tick += GameLoop;
                timer.Start();
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"Error loading map: {ex.Message}",
                    Foreground = Brushes.White,
                    Margin = new Thickness(10)
                };
                gameCanvas?.Children.Add(errorText);
            }
        }

        private void InitPlayer(Bitmap? sprite, CharacterClass cls, string name)
        {
            if (map == null) return;
            var cx = Math.Max(0, map.Cols / 2);
            var cy = Math.Max(0, map.Rows / 2);
            player = new Character(cx, cy, cls, name)
            {
                Sprite = sprite,
                Color = sprite != null ? Colors.Transparent : Colors.DeepSkyBlue
            };
            
            if (sprite != null)
            {
                player.InitializeAnimations(32, 32, 3);
            }
            
            // Update window title to show player info for easier testing/debugging
            this.Title = $"DotGame - {player.Name} ({player.Class}) HP {player.CurrentHP}/{player.Attributes.MaxHP}";
        }

        private void SpawnMonsters()
        {
            if (map == null) return;

            var random = new Random();
            int monsterCount = Math.Min(5, (map.Cols * map.Rows) / 50);
            
            for (int i = 0; i < monsterCount; i++)
            {
                int x = random.Next(0, map.Cols);
                int y = random.Next(0, map.Rows);
                
                if (player != null && (Math.Abs(x - player.TileX) < 3 || Math.Abs(y - player.TileY) < 3))
                    continue;

                var type = (MonsterType)random.Next(0, 3);
                monsters.Add(new Monster(x, y, type));
            }
        }

        private void FitWindowToMap()
        {
            if (map is null) return;

            var w = map.Cols * map.TileW;
            var h = map.Rows * map.TileH;

            Width = Math.Min(1200, w + 16);
            Height = Math.Min(900, h + 39);
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            if (player != null && combatManager == null)
            {
                if (!playerMovedThisFrame && player.CurrentState == AnimationState.Walk)
                {
                    player.SetAnimation(AnimationState.Idle);
                }
            }
            playerMovedThisFrame = false;

            player?.UpdateAnimation();
            
            foreach (var monster in monsters)
            {
                monster.UpdateAnimation();
                
                if (combatManager == null || !combatManager.CombatActive)
                {
                    monster.UpdateAI(map!, player!);
                    
                    if (!monster.DidMoveThisUpdate && monster.CurrentState == AnimationState.Walk)
                    {
                        monster.SetAnimation(AnimationState.Idle);
                    }
                }
                else if (monster.CurrentState == AnimationState.Walk)
                {
                    monster.SetAnimation(AnimationState.Idle);
                }
            }

            combatManager?.Update();
            CheckCombatTriggers();
            UpdateUI();
            RenderGame();
        }

        private void CheckCombatTriggers()
        {
            if (player == null || map == null) return;
            if (combatManager != null && combatManager.CombatActive) return;

            foreach (var monster in monsters.Where(m => m.IsAlive))
            {
                if (player.TileX == monster.TileX && player.TileY == monster.TileY)
                {
                    combatManager = new CombatManager(player, monster);
                    combatManager.StartCombat();
                    if (combatUI != null) combatUI.IsVisible = true;
                    break;
                }
            }
        }

        private void HandlePlayerAttack()
        {
            combatManager?.PlayerAttack();
        }

        private void HandlePlayerDefend()
        {
            combatManager?.PlayerDefend();
        }

        private void UpdateUI()
        {
            if (player != null && txtPlayerStats != null)
            {
                txtPlayerStats.Text = $"{player.Name} (Lv.1 {player.Class})\nHP: {player.CurrentHP}/{player.Attributes.MaxHP} | ATK: {player.Attributes.Attack} | DEF: {player.Attributes.Defense}";
            }

            if (combatManager != null && combatManager.CombatActive)
            {
                if (txtCombatMessage != null)
                    txtCombatMessage.Text = combatManager.LastMessage;

                if (txtPlayerInfo != null && player != null)
                    txtPlayerInfo.Text = $"{player.Name} HP: {player.CurrentHP}/{player.Attributes.MaxHP}";

                if (barPlayerHP != null && player != null)
                {
                    barPlayerHP.Maximum = player.Attributes.MaxHP;
                    barPlayerHP.Value = player.CurrentHP;
                }

                if (txtEnemyInfo != null && combatManager.Enemy != null)
                    txtEnemyInfo.Text = $"{combatManager.Enemy.Name} HP: {combatManager.Enemy.CurrentHP}/{combatManager.Enemy.MaxHP}";

                if (barEnemyHP != null && combatManager.Enemy != null)
                {
                    barEnemyHP.Maximum = combatManager.Enemy.MaxHP;
                    barEnemyHP.Value = combatManager.Enemy.CurrentHP;
                }
            }
            else
            {
                if (combatUI != null) combatUI.IsVisible = false;
            }
        }

        private void RenderGame()
        {
            if (map?.Composite == null || player == null || gameCanvas == null)
                return;

            gameCanvas.Children.Clear();

            var mapImg = new Image
            {
                Source = map.Composite,
                Width = map.Cols * map.TileW,
                Height = map.Rows * map.TileH
            };
            Canvas.SetLeft(mapImg, 0);
            Canvas.SetTop(mapImg, 0);
            gameCanvas.Children.Add(mapImg);

            var surface = SKSurface.Create(new SKImageInfo(map.Cols * map.TileW, map.Rows * map.TileH));
            var canvas = surface.Canvas;

            foreach (var monster in monsters)
            {
                if (!monster.IsAlive) continue;
                monster.Draw(canvas, map);
            }

            if (player != null)
            {
                player.Draw(canvas, map);
            }

            var image = surface.Snapshot();
            var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            var entityLayer = new Bitmap(stream);
            
            var entityImg = new Image
            {
                Source = entityLayer,
                Width = map.Cols * map.TileW,
                Height = map.Rows * map.TileH
            };
            Canvas.SetLeft(entityImg, 0);
            Canvas.SetTop(entityImg, 0);
            gameCanvas.Children.Add(entityImg);
        }


        private void GameWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (player is null || map is null) return;

            if (combatManager != null && combatManager.CombatActive)
            {
                return;
            }

            bool moved = false;
            switch (e.Key)
            {
                case Key.Up:
                case Key.W: player.TryMove(0, -1, map); moved = true; break;
                case Key.Down:
                case Key.S: player.TryMove(0, +1, map); moved = true; break;
                case Key.Left:
                case Key.A: player.TryMove(-1, 0, map); moved = true; break;
                case Key.Right:
                case Key.D: player.TryMove(+1, 0, map); moved = true; break;
                case Key.Escape: Close(); break;
            }
            
            if (moved)
            {
                playerMovedThisFrame = true;
            }
            
            RenderGame();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            timer?.Stop();
        }
    }
}
