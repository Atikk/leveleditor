using System;
using System.IO;
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
        private Canvas? gameCanvas;
        private DispatcherTimer? timer;

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
            KeyDown += GameWindow_KeyDown;
        }

        private void LoadGame(string mapPath, Bitmap? playerSprite, CharacterClass charClass, string charName)
        {
            try
            {
                map = Map.LoadFromJson(mapPath);
                InitPlayer(playerSprite, charClass, charName);
                FitWindowToMap();
                RenderGame();
                
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0) };
                timer.Tick += (s, e) =>
                {
                    player?.UpdateAnimation();
                    RenderGame();
                };
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
        }

        private void FitWindowToMap()
        {
            if (map is null) return;

            var w = map.Cols * map.TileW;
            var h = map.Rows * map.TileH;

            Width = Math.Min(1200, w + 16);
            Height = Math.Min(900, h + 39);
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

            var playerRect = map.TileRect(player.TileX, player.TileY);
            
            if (player.Sprite != null)
            {
                var croppedPlayer = CropSprite(player.Sprite, player.FrameIndex, player.FrameWidth, player.FrameHeight, (int)player.Direction);
                var playerImg = new Image
                {
                    Source = croppedPlayer,
                    Width = playerRect.Width,
                    Height = playerRect.Height
                };
                Canvas.SetLeft(playerImg, playerRect.X);
                Canvas.SetTop(playerImg, playerRect.Y);
                gameCanvas.Children.Add(playerImg);
            }
            else
            {
                var playerRect2 = new Avalonia.Controls.Shapes.Rectangle
                {
                    Width = playerRect.Width,
                    Height = playerRect.Height,
                    Fill = new SolidColorBrush(player.Color),
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(playerRect2, playerRect.X);
                Canvas.SetTop(playerRect2, playerRect.Y);
                gameCanvas.Children.Add(playerRect2);
            }
        }

        private Bitmap CropSprite(Bitmap sprite, int frameIndex, int frameW, int frameH, int direction)
        {
            var surface = SKSurface.Create(new SKImageInfo(frameW, frameH));
            var canvas = surface.Canvas;
            
            using var skSprite = BitmapToSKBitmap(sprite);
            var srcRect = new SKRect(frameIndex * frameW, direction * frameH, 
                                     (frameIndex + 1) * frameW, (direction + 1) * frameH);
            var destRect = new SKRect(0, 0, frameW, frameH);
            canvas.DrawBitmap(skSprite, srcRect, destRect);
            
            var image = surface.Snapshot();
            var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }

        private void GameWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (player is null || map is null) return;

            switch (e.Key)
            {
                case Key.Up:
                case Key.W: player.TryMove(0, -1, map); break;
                case Key.Down:
                case Key.S: player.TryMove(0, +1, map); break;
                case Key.Left:
                case Key.A: player.TryMove(-1, 0, map); break;
                case Key.Right:
                case Key.D: player.TryMove(+1, 0, map); break;
                case Key.Escape: Close(); break;
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
