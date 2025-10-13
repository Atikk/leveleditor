<Query Kind="Program">
  <IncludeUncapsulator>false</IncludeUncapsulator>
</Query>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

void Main()
{
	
}

namespace DotGameCSharp
{
    /// <summary>
    /// A simple tile-based map editor inspired by the JavaScript version of Dotgame.
    /// This editor allows you to load a sprite sheet or individual tile images,
    /// split the sprite sheet into tiles, paint them onto a square grid,
    /// adjust brush and grid sizes, and save or load maps as JSON files.
    /// Tiles are persisted as base64-encoded PNG images so the map is fully self-contained.
    /// </summary>
    public partial class EditorForm : Form
    {
        // UI controls
        private Button loadSpriteSheetButton;
        private Button loadTilesButton;
        private NumericUpDown tileWidthUpDown;
        private NumericUpDown tileHeightUpDown;
        private NumericUpDown tileSpacingUpDown;
        private NumericUpDown tileMarginUpDown;
        private Button splitSpriteSheetButton;
        private FlowLayoutPanel tilePalettePanel;
        private ComboBox gridSizeComboBox;
        private NumericUpDown brushSizeUpDown;
        private Button clearGridButton;
        private Button saveMapButton;
        private Button loadMapButton;
        private Panel mapPanel;

        // Data
        private List<Bitmap> tiles = new List<Bitmap>();
        private Bitmap? selectedTile = null;
        private int gridSize = 20;
        private int brushSize = 1;
        private Bitmap?[,] mapData;
        private bool isMouseDown = false;

        public EditorForm()
        {
            InitializeComponent();
            mapData = new Bitmap?[gridSize, gridSize];
            DoubleBuffered = true;
        }

        private void InitializeComponent()
        {
            // Basic form settings
            this.Text = "Dotgame Map Editor (C#)";
            this.Width = 1200;
            this.Height = 800;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create controls
            loadSpriteSheetButton = new Button { Text = "Load Sprite Sheet" };
            loadTilesButton = new Button { Text = "Load Tile Images" };
            tileWidthUpDown = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 32 };
            tileHeightUpDown = new NumericUpDown { Minimum = 1, Maximum = 512, Value = 32 };
            tileSpacingUpDown = new NumericUpDown { Minimum = 0, Maximum = 64, Value = 0 };
            tileMarginUpDown = new NumericUpDown { Minimum = 0, Maximum = 64, Value = 0 };
            splitSpriteSheetButton = new Button { Text = "Split Sheet" };
            tilePalettePanel = new FlowLayoutPanel { AutoScroll = true, Width = 200, Dock = DockStyle.Left, BorderStyle = BorderStyle.FixedSingle };
            gridSizeComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            brushSizeUpDown = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 1 };
            clearGridButton = new Button { Text = "Clear Grid" };
            saveMapButton = new Button { Text = "Save Map" };
            loadMapButton = new Button { Text = "Load Map" };
            mapPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // Populate grid size options
            foreach (var size in new[] { 10, 20, 30, 40, 50 })
            {
                gridSizeComboBox.Items.Add(size);
            }
            gridSizeComboBox.SelectedItem = gridSize;

            // Layout panels
            var controlPanel = new Panel { Dock = DockStyle.Top, Height = 120 };
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Top, Height = 30 };
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Top, Height = 30 };
            var row3 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Top, Height = 30 };
            var row4 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Top, Height = 30 };

            // Row1: Sprite sheet controls
            row1.Controls.Add(loadSpriteSheetButton);
            row1.Controls.Add(new Label { Text = "Tile W:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            row1.Controls.Add(tileWidthUpDown);
            row1.Controls.Add(new Label { Text = "Tile H:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            row1.Controls.Add(tileHeightUpDown);
            row1.Controls.Add(new Label { Text = "Spacing:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            row1.Controls.Add(tileSpacingUpDown);
            row1.Controls.Add(new Label { Text = "Margin:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            row1.Controls.Add(tileMarginUpDown);
            row1.Controls.Add(splitSpriteSheetButton);

            // Row2: Individual tile images
            row2.Controls.Add(loadTilesButton);
            row2.Controls.Add(new Label { Text = "Brush Size:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            row2.Controls.Add(brushSizeUpDown);
            row2.Controls.Add(new Label { Text = "Grid Size:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            row2.Controls.Add(gridSizeComboBox);
            row2.Controls.Add(clearGridButton);

            // Row3: Save/load
            row3.Controls.Add(saveMapButton);
            row3.Controls.Add(loadMapButton);

            // Row4: Info label (spacer)
            var infoLabel = new Label { AutoSize = true, Text = "Select tiles and paint on the grid.", ForeColor = Color.DarkGray };
            row4.Controls.Add(infoLabel);

            controlPanel.Controls.Add(row1);
            controlPanel.Controls.Add(row2);
            controlPanel.Controls.Add(row3);
            controlPanel.Controls.Add(row4);

            // Add panels to form
            this.Controls.Add(mapPanel);
            this.Controls.Add(tilePalettePanel);
            this.Controls.Add(controlPanel);

            // Event handlers
            loadSpriteSheetButton.Click += LoadSpriteSheetButton_Click;
            splitSpriteSheetButton.Click += SplitSpriteSheetButton_Click;
            loadTilesButton.Click += LoadTilesButton_Click;
            gridSizeComboBox.SelectedIndexChanged += GridSizeComboBox_SelectedIndexChanged;
            brushSizeUpDown.ValueChanged += BrushSizeUpDown_ValueChanged;
            clearGridButton.Click += ClearGridButton_Click;
            saveMapButton.Click += SaveMapButton_Click;
            loadMapButton.Click += LoadMapButton_Click;
            mapPanel.Paint += MapPanel_Paint;
            mapPanel.MouseDown += MapPanel_MouseDown;
            mapPanel.MouseMove += MapPanel_MouseMove;
            mapPanel.MouseUp += MapPanel_MouseUp;
        }

        private void LoadSpriteSheetButton_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using var img = new Bitmap(ofd.FileName);
                    spriteSheetImage = new Bitmap(img);
                    MessageBox.Show("Sprite sheet loaded. Adjust tile size, spacing, and margin then click 'Split Sheet'.", "Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Bitmap? spriteSheetImage;

        private void SplitSpriteSheetButton_Click(object? sender, EventArgs e)
        {
            if (spriteSheetImage == null)
            {
                MessageBox.Show("Load a sprite sheet first.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var tw = (int)tileWidthUpDown.Value;
            var th = (int)tileHeightUpDown.Value;
            var spacing = (int)tileSpacingUpDown.Value;
            var margin = (int)tileMarginUpDown.Value;
            tiles.Clear();
            tilePalettePanel.Controls.Clear();

            int cols = (spriteSheetImage.Width - margin * 2 + spacing) / (tw + spacing);
            int rows = (spriteSheetImage.Height - margin * 2 + spacing) / (th + spacing);
            if (cols <= 0 || rows <= 0)
            {
                MessageBox.Show($"Invalid tile dimensions for this sprite sheet.\nSheet: {spriteSheetImage.Width}x{spriteSheetImage.Height}, Tile: {tw}x{th}", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int sx = margin + x * (tw + spacing);
                    int sy = margin + y * (th + spacing);
                    if (sx + tw > spriteSheetImage.Width || sy + th > spriteSheetImage.Height) continue;
                    var tileBmp = new Bitmap(tw, th);
                    using (var g = Graphics.FromImage(tileBmp))
                    {
                        g.DrawImage(spriteSheetImage, new Rectangle(0, 0, tw, th), new Rectangle(sx, sy, tw, th), GraphicsUnit.Pixel);
                    }
                    tiles.Add(tileBmp);
                    AddTileToPalette(tileBmp);
                }
            }
            if (tiles.Count > 0)
            {
                selectedTile = tiles[0];
            }
        }

        private void AddTileToPalette(Bitmap tile)
        {
            var pb = new PictureBox
            {
                Image = tile,
                Width = 32,
                Height = 32,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2)
            };
            pb.Click += (s, e) =>
            {
                selectedTile = tile;
                foreach (Control c in tilePalettePanel.Controls)
                {
                    if (c is PictureBox pic)
                    {
                        pic.BackColor = (pic.Image == tile) ? Color.LightBlue : Color.Transparent;
                    }
                }
            };
            tilePalettePanel.Controls.Add(pb);
        }

        private void LoadTilesButton_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                tiles.Clear();
                tilePalettePanel.Controls.Clear();
                foreach (var file in ofd.FileNames)
                {
                    try
                    {
                        var bmp = new Bitmap(file);
                        tiles.Add(bmp);
                        AddTileToPalette(bmp);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading {file}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                if (tiles.Count > 0) selectedTile = tiles[0];
            }
        }

        private void GridSizeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (gridSizeComboBox.SelectedItem is int size)
            {
                gridSize = size;
            }
            else
            {
                gridSize = int.Parse(gridSizeComboBox.SelectedItem.ToString()!);
            }
            // Resize map data
            mapData = new Bitmap?[gridSize, gridSize];
            mapPanel.Invalidate();
        }

        private void BrushSizeUpDown_ValueChanged(object? sender, EventArgs e)
        {
            brushSize = (int)brushSizeUpDown.Value;
        }

        private void ClearGridButton_Click(object? sender, EventArgs e)
        {
            mapData = new Bitmap?[gridSize, gridSize];
            mapPanel.Invalidate();
        }

private void SaveMapButton_Click(object? sender, EventArgs e)
{
    int cols = gridSize;
    int rows = gridSize;
    var tileW = (int)tileWidthUpDown.Value;
    var tileH = (int)tileHeightUpDown.Value;

    // Build 2D array of Base64 tiles
    string?[][] mapArray = new string?[rows][];
    for (int y = 0; y < rows; y++)
    {
        mapArray[y] = new string?[cols];
        for (int x = 0; x < cols; x++)
        {
            var bmp = mapData[x, y];
            if (bmp != null)
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                string base64 = Convert.ToBase64String(ms.ToArray());
                mapArray[y][x] = "data:image/png;base64," + base64;
            }
            else mapArray[y][x] = null;
        }
    }

    var mapObject = new
    {
        cols,
        rows,
        tileW,
        tileH,
        map = mapArray
    };

    using var sfd = new SaveFileDialog();
    sfd.Filter = "JSON files (*.json)|*.json";
    sfd.FileName = "tilemap.json";

    if (sfd.ShowDialog() == DialogResult.OK)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(mapObject, options));
        MessageBox.Show("Map saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}


private void LoadMapButton_Click(object? sender, EventArgs e)
{
    using var ofd = new OpenFileDialog();
    ofd.Filter = "JSON files (*.json)|*.json";
    if (ofd.ShowDialog() != DialogResult.OK) return;

    try
    {
        var json = File.ReadAllText(ofd.FileName);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        gridSize = root.GetProperty("cols").GetInt32();
        mapData = new Bitmap?[gridSize, gridSize];

        var mapArray = root.GetProperty("map");
        for (int y = 0; y < gridSize; y++)
        {
            var row = mapArray[y];
            for (int x = 0; x < gridSize; x++)
            {
                if (row[x].ValueKind == JsonValueKind.String)
                {
                    string dataUrl = row[x].GetString()!;
                    int comma = dataUrl.IndexOf(',');
                    string base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
                    byte[] bytes = Convert.FromBase64String(base64);
                    using var ms = new MemoryStream(bytes);
                    mapData[x, y] = new Bitmap(ms);
                }
            }
        }

        mapPanel.Invalidate();
        MessageBox.Show("Map loaded successfully.", "Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error loading map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}


        private void MapPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            if (gridSize <= 0) return;
            float cellSize = Math.Min(mapPanel.Width, mapPanel.Height) / (float)gridSize;
            // Draw tiles
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var bmp = mapData[x, y];
                    if (bmp != null)
                    {
                        g.DrawImage(bmp, x * cellSize, y * cellSize, cellSize, cellSize);
                    }
                }
            }
            // Draw grid lines
            using var pen = new Pen(Color.LightGray);
            for (int i = 0; i <= gridSize; i++)
            {
                g.DrawLine(pen, i * cellSize, 0, i * cellSize, gridSize * cellSize);
                g.DrawLine(pen, 0, i * cellSize, gridSize * cellSize, i * cellSize);
            }
        }

        private void MapPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            isMouseDown = true;
            PaintAtMouse(e.Location);
        }
        private void MapPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                PaintAtMouse(e.Location);
            }
        }
        private void MapPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            isMouseDown = false;
        }

        private void PaintAtMouse(Point location)
        {
            if (selectedTile == null) return;
            int width = mapPanel.Width;
            int height = mapPanel.Height;
            float cellSize = Math.Min(width, height) / (float)gridSize;
            int gridX = (int)(location.X / cellSize);
            int gridY = (int)(location.Y / cellSize);
            int offset = brushSize / 2;
            for (int dy = 0; dy < brushSize; dy++)
            {
                for (int dx = 0; dx < brushSize; dx++)
                {
                    int x = gridX - offset + dx;
                    int y = gridY - offset + dy;
                    if (x >= 0 && y >= 0 && x < gridSize && y < gridSize)
                    {
                        mapData[x, y] = new Bitmap(selectedTile);
                    }
                }
            }
            mapPanel.Invalidate();
        }

        // Level data structure for saving/loading
        public class LevelData
        {
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int TileWidth { get; set; }
            public int TileHeight { get; set; }
            public List<TileData> Tiles { get; set; } = new List<TileData>();
        }
        public class TileData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Image { get; set; } = "";
        }
    }
}
