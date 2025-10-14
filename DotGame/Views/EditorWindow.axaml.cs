using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using IOPath = System.IO.Path;

namespace DotGameAvalonia.Views
{
    public partial class EditorWindow : Window
    {
        private List<Bitmap> tiles = new List<Bitmap>();
        private Bitmap? selectedTile = null;
        private Bitmap? spriteSheetImage = null;
        private int gridSize = 20;
        private int brushSize = 1;
        private Bitmap?[,] mapData;
        private bool isMouseDown = false;
        private Border? selectedTileBorder = null;

        private Canvas? mapCanvas;
        private WrapPanel? tilePalette;
        private NumericUpDown? numTileWidth, numTileHeight, numSpacing, numMargin, numBrushSize;
        private ComboBox? cmbGridSize;

        public EditorWindow()
        {
            InitializeComponent();
            mapData = new Bitmap?[gridSize, gridSize];
            AttachEvents();
        }

        private void AttachEvents()
        {
            mapCanvas = this.FindControl<Canvas>("MapCanvas");
            tilePalette = this.FindControl<WrapPanel>("TilePalette");
            numTileWidth = this.FindControl<NumericUpDown>("NumTileWidth");
            numTileHeight = this.FindControl<NumericUpDown>("NumTileHeight");
            numSpacing = this.FindControl<NumericUpDown>("NumSpacing");
            numMargin = this.FindControl<NumericUpDown>("NumMargin");
            numBrushSize = this.FindControl<NumericUpDown>("NumBrushSize");
            cmbGridSize = this.FindControl<ComboBox>("CmbGridSize");

            var btnLoadSpriteSheet = this.FindControl<Button>("BtnLoadSpriteSheet");
            var btnSplitSheet = this.FindControl<Button>("BtnSplitSheet");
            var btnLoadTiles = this.FindControl<Button>("BtnLoadTiles");
            var btnClearGrid = this.FindControl<Button>("BtnClearGrid");
            var btnSaveMap = this.FindControl<Button>("BtnSaveMap");
            var btnLoadMap = this.FindControl<Button>("BtnLoadMap");
            var btnSpriteEditor = this.FindControl<Button>("BtnSpriteEditor");

            if (btnLoadSpriteSheet != null)
                btnLoadSpriteSheet.Click += BtnLoadSpriteSheet_Click;
            if (btnSplitSheet != null)
                btnSplitSheet.Click += BtnSplitSheet_Click;
            if (btnLoadTiles != null)
                btnLoadTiles.Click += BtnLoadTiles_Click;
            if (btnClearGrid != null)
                btnClearGrid.Click += BtnClearGrid_Click;
            if (btnSaveMap != null)
                btnSaveMap.Click += BtnSaveMap_Click;
            if (btnLoadMap != null)
                btnLoadMap.Click += BtnLoadMap_Click;
            if (btnSpriteEditor != null)
                btnSpriteEditor.Click += BtnSpriteEditor_Click;
            
            if (cmbGridSize != null)
                cmbGridSize.SelectionChanged += CmbGridSize_SelectionChanged;
            
            if (numBrushSize != null)
                numBrushSize.ValueChanged += (s, e) => brushSize = (int)(numBrushSize.Value ?? 1);

            RenderMap();
        }

        private async void BtnLoadSpriteSheet_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Load Sprite Sheet",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Enter sprite sheet path:" });
            var txtPath = new TextBox { Watermark = "e.g. sprites/tileset.png" };
            stack.Children.Add(txtPath);

            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var btnOk = new Button { Content = "Load", Width = 80 };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            btnOk.Click += (s, ev) =>
            {
                var path = txtPath.Text ?? "";
                if (!IOPath.IsPathRooted(path))
                    path = IOPath.Combine("/home/runner/workspace", path);

                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        spriteSheetImage = new Bitmap(path);
                        dialog.Close();
                    }
                    catch { }
                }
            };
            btnCancel.Click += (s, ev) => dialog.Close();

            await dialog.ShowDialog(this);
        }

        private void BtnSplitSheet_Click(object? sender, RoutedEventArgs e)
        {
            if (spriteSheetImage == null || numTileWidth == null || numTileHeight == null || 
                numSpacing == null || numMargin == null || tilePalette == null)
                return;

            var tw = (int)(numTileWidth.Value ?? 32);
            var th = (int)(numTileHeight.Value ?? 32);
            var spacing = (int)(numSpacing.Value ?? 0);
            var margin = (int)(numMargin.Value ?? 0);

            tiles.Clear();
            tilePalette.Children.Clear();

            using var skBitmap = BitmapToSKBitmap(spriteSheetImage);
            int cols = (skBitmap.Width - margin * 2 + spacing) / (tw + spacing);
            int rows = (skBitmap.Height - margin * 2 + spacing) / (th + spacing);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int sx = margin + x * (tw + spacing);
                    int sy = margin + y * (th + spacing);
                    
                    if (sx + tw > skBitmap.Width || sy + th > skBitmap.Height) continue;

                    var surface = SKSurface.Create(new SKImageInfo(tw, th));
                    var canvas = surface.Canvas;
                    var srcRect = new SKRect(sx, sy, sx + tw, sy + th);
                    var destRect = new SKRect(0, 0, tw, th);
                    canvas.DrawBitmap(skBitmap, srcRect, destRect);

                    var image = surface.Snapshot();
                    var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = new System.IO.MemoryStream(data.ToArray());
                    var tileBmp = new Bitmap(stream);

                    tiles.Add(tileBmp);
                    AddTileToPalette(tileBmp);
                }
            }

            if (tiles.Count > 0)
                selectedTile = tiles[0];
        }

        private async void BtnLoadTiles_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Load Tile Images",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Enter tile paths (one per line):" });
            var txtPaths = new TextBox { Height = 200, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(txtPaths);

            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var btnOk = new Button { Content = "Load", Width = 80 };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            btnOk.Click += (s, ev) =>
            {
                tiles.Clear();
                tilePalette?.Children.Clear();
                
                var paths = txtPaths.Text?.Split('\n') ?? Array.Empty<string>();
                foreach (var p in paths)
                {
                    var path = p.Trim();
                    if (string.IsNullOrEmpty(path)) continue;
                    
                    if (!IOPath.IsPathRooted(path))
                        path = IOPath.Combine("/home/runner/workspace", path);

                    if (System.IO.File.Exists(path))
                    {
                        try
                        {
                            var bmp = new Bitmap(path);
                            tiles.Add(bmp);
                            AddTileToPalette(bmp);
                        }
                        catch { }
                    }
                }

                if (tiles.Count > 0)
                    selectedTile = tiles[0];

                dialog.Close();
            };
            btnCancel.Click += (s, ev) => dialog.Close();

            await dialog.ShowDialog(this);
        }

        private void AddTileToPalette(Bitmap tile)
        {
            if (tilePalette == null) return;

            var border = new Border
            {
                Width = 48,
                Height = 48,
                Margin = new Thickness(2),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2),
                Child = new Image { Source = tile, Stretch = Stretch.Uniform }
            };

            border.PointerPressed += (s, e) =>
            {
                selectedTile = tile;
                if (selectedTileBorder != null)
                    selectedTileBorder.Background = Brushes.Transparent;
                border.Background = Brushes.LightBlue;
                selectedTileBorder = border;
            };

            tilePalette.Children.Add(border);

            if (selectedTileBorder == null)
            {
                selectedTileBorder = border;
                border.Background = Brushes.LightBlue;
            }
        }

        private void CmbGridSize_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (cmbGridSize?.SelectedItem is ComboBoxItem item)
            {
                gridSize = int.Parse(item.Content?.ToString() ?? "20");
                mapData = new Bitmap?[gridSize, gridSize];
                RenderMap();
            }
        }

        private void BtnClearGrid_Click(object? sender, RoutedEventArgs e)
        {
            mapData = new Bitmap?[gridSize, gridSize];
            RenderMap();
        }

        private async void BtnSaveMap_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Save Map",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Save map to:" });
            var txtPath = new TextBox { Text = "maps/mymap.json" };
            stack.Children.Add(txtPath);

            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var btnOk = new Button { Content = "Save", Width = 80 };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            btnOk.Click += (s, ev) =>
            {
                var path = txtPath.Text ?? "maps/mymap.json";
                if (!IOPath.IsPathRooted(path))
                    path = IOPath.Combine("/home/runner/workspace", path);

                System.IO.Directory.CreateDirectory(IOPath.GetDirectoryName(path) ?? ".");

                var tw = (int)(numTileWidth?.Value ?? 32);
                var th = (int)(numTileHeight?.Value ?? 32);

                string?[][] mapArray = new string?[gridSize][];
                for (int y = 0; y < gridSize; y++)
                {
                    mapArray[y] = new string?[gridSize];
                    for (int x = 0; x < gridSize; x++)
                    {
                        var bmp = mapData[x, y];
                        if (bmp != null)
                        {
                            using var ms = new System.IO.MemoryStream();
                            bmp.Save(ms);
                            string base64 = Convert.ToBase64String(ms.ToArray());
                            mapArray[y][x] = "data:image/png;base64," + base64;
                        }
                        else mapArray[y][x] = null;
                    }
                }

                var mapObject = new
                {
                    cols = gridSize,
                    rows = gridSize,
                    tileW = tw,
                    tileH = th,
                    map = mapArray
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(path, JsonSerializer.Serialize(mapObject, options));
                dialog.Close();
            };
            btnCancel.Click += (s, ev) => dialog.Close();

            await dialog.ShowDialog(this);
        }

        private async void BtnLoadMap_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Load Map",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Load map from:" });
            var txtPath = new TextBox { Text = "maps/sample.json" };
            stack.Children.Add(txtPath);

            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var btnOk = new Button { Content = "Load", Width = 80 };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            btnOk.Click += (s, ev) =>
            {
                var path = txtPath.Text ?? "";
                if (!IOPath.IsPathRooted(path))
                    path = IOPath.Combine("/home/runner/workspace", path);

                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        gridSize = root.GetProperty("cols").GetInt32();
                        mapData = new Bitmap?[gridSize, gridSize];

                        var mapArray = root.GetProperty("map");
                        for (int y = 0; y < gridSize && y < mapArray.GetArrayLength(); y++)
                        {
                            var row = mapArray[y];
                            for (int x = 0; x < gridSize && x < row.GetArrayLength(); x++)
                            {
                                if (row[x].ValueKind == JsonValueKind.String)
                                {
                                    string dataUrl = row[x].GetString()!;
                                    int comma = dataUrl.IndexOf(',');
                                    string base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
                                    byte[] bytes = Convert.FromBase64String(base64);
                                    using var ms = new System.IO.MemoryStream(bytes);
                                    mapData[x, y] = new Bitmap(ms);
                                }
                            }
                        }

                        RenderMap();
                        dialog.Close();
                    }
                    catch { }
                }
            };
            btnCancel.Click += (s, ev) => dialog.Close();

            await dialog.ShowDialog(this);
        }

        private async void BtnSpriteEditor_Click(object? sender, RoutedEventArgs e)
        {
            var spriteEditor = new SpriteEditorWindow();
            await spriteEditor.ShowDialog(this);
        }

        private void MapCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            isMouseDown = true;
            var point = e.GetPosition(mapCanvas);
            PaintAtPosition(point, e.GetCurrentPoint(this).Properties.IsRightButtonPressed);
        }

        private void MapCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (isMouseDown && mapCanvas != null)
            {
                var point = e.GetPosition(mapCanvas);
                PaintAtPosition(point, e.GetCurrentPoint(this).Properties.IsRightButtonPressed);
            }
        }

        private void MapCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            isMouseDown = false;
        }

        private void PaintAtPosition(Point location, bool erase)
        {
            if (mapCanvas == null) return;
            
            float cellSize = (float)Math.Min(mapCanvas.Bounds.Width, mapCanvas.Bounds.Height) / gridSize;
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
                        if (!erase && selectedTile != null)
                        {
                            using var ms = new System.IO.MemoryStream();
                            selectedTile.Save(ms);
                            ms.Position = 0;
                            mapData[x, y] = new Bitmap(ms);
                        }
                        else
                        {
                            mapData[x, y] = null;
                        }
                    }
                }
            }
            RenderMap();
        }

        private void RenderMap()
        {
            if (mapCanvas == null) return;
            mapCanvas.Children.Clear();

            float cellSize = 600f / gridSize;
            mapCanvas.Width = gridSize * cellSize;
            mapCanvas.Height = gridSize * cellSize;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var bmp = mapData[x, y];
                    if (bmp != null)
                    {
                        var img = new Image
                        {
                            Source = bmp,
                            Width = cellSize,
                            Height = cellSize
                        };
                        Canvas.SetLeft(img, x * cellSize);
                        Canvas.SetTop(img, y * cellSize);
                        mapCanvas.Children.Add(img);
                    }
                }
            }

            for (int i = 0; i <= gridSize; i++)
            {
                var vline = new Line
                {
                    StartPoint = new Point(i * cellSize, 0),
                    EndPoint = new Point(i * cellSize, gridSize * cellSize),
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };
                var hline = new Line
                {
                    StartPoint = new Point(0, i * cellSize),
                    EndPoint = new Point(gridSize * cellSize, i * cellSize),
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };
                mapCanvas.Children.Add(vline);
                mapCanvas.Children.Add(hline);
            }
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }
    }
}
