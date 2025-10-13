using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DotGameCSharp
{
    public sealed class GameForm : Form
    {
        private Map? map;
        private Character? player;
        private readonly Timer timer;

        public GameForm(string mapPath, Bitmap? playerSprite, CharacterClass charClass = CharacterClass.Warrior, string charName = "Hero")
        {
            Text = "DotGame – Play Test (Tile-based)";
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.Black;

            Load += (s, e) =>
            {
                map = Map.LoadFromJson(mapPath);
                InitPlayer(playerSprite, charClass, charName);
                FitWindowToMap();
            };

            KeyDown += (s, e) =>
            {
                if (player is null || map is null) return;

                switch (e.KeyCode)
                {
                    case Keys.Up:
                    case Keys.W: player.TryMove(0, -1, map); break;
                    case Keys.Down:
                    case Keys.S: player.TryMove(0, +1, map); break;
                    case Keys.Left:
                    case Keys.A: player.TryMove(-1, 0, map); break;
                    case Keys.Right:
                    case Keys.D: player.TryMove(+1, 0, map); break;
                    case Keys.Escape: Close(); break;
                }
                Invalidate();
            };

            timer = new Timer { Interval = 1000 / 30 };
            timer.Tick += (s, e) =>
            {
                player?.UpdateAnimation();
                Invalidate();
            };
            timer.Start();
        }

        private void EnsureMapLoaded(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                map = Map.LoadFromJson(path);
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Filter = "Tile Map (*.json)|*.json",
                Title = "Open a tilemap JSON saved by the editor"
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                map = Map.LoadFromJson(ofd.FileName);
            }
            else
            {
                MessageBox.Show(this, "No map selected. Closing.", "DotGame", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
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
                Color = Color.Transparent
            };
        }

        private void FitWindowToMap()
        {
            if (map is null) return;

            var w = map.Cols * map.TileW;
            var h = map.Rows * map.TileH;

            ClientSize = new Size(Math.Min(1200, w), Math.Min(900, h));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (map?.Composite is { } bg)
            {
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                e.Graphics.DrawImage(bg, 0, 0);

                player?.Draw(e.Graphics, map);
            }
            else
            {
                using var f = new Font(FontFamily.GenericSansSerif, 12);
                TextRenderer.DrawText(e.Graphics, "No map loaded.", f, new Point(10, 10), Color.White);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                timer?.Dispose();
                map?.Composite?.Dispose();
            }
        }
    }
}
