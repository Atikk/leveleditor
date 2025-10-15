using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DotGameAvalonia.Models;
using SkiaSharp;
using IOPath = System.IO.Path;

namespace DotGameAvalonia.Views
{
    public partial class EditorWindow : Window
    {
        private sealed class TileEntry
        {
            public Bitmap Bitmap { get; }
            public string? SourceKey { get; }
            public string? DataUrl { get; }
            public string? SerializedValueOverride { get; }

            public TileEntry(Bitmap bitmap, string? sourceKey, string? dataUrl = null, string? serializedValueOverride = null)
            {
                Bitmap = bitmap;
                SourceKey = sourceKey;
                DataUrl = dataUrl;
                SerializedValueOverride = serializedValueOverride;
            }

            public string GetSerializedValue()
            {
                if (!string.IsNullOrWhiteSpace(SerializedValueOverride))
                    return SerializedValueOverride!;

                if (!string.IsNullOrWhiteSpace(SourceKey))
                    return SourceKey!;

                if (!string.IsNullOrEmpty(DataUrl))
                    return DataUrl!;

                using var ms = new System.IO.MemoryStream();
                Bitmap.Save(ms);
                var base64 = Convert.ToBase64String(ms.ToArray());
                return "data:image/png;base64," + base64;
            }

            public TileEntry Clone()
            {
                using var ms = new System.IO.MemoryStream();
                Bitmap.Save(ms);
                ms.Position = 0;
                return new TileEntry(new Bitmap(ms), SourceKey, DataUrl, SerializedValueOverride);
            }

            public static TileEntry FromDataUrl(string dataUrl)
            {
                int comma = dataUrl.IndexOf(',');
                string base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
                byte[] bytes = Convert.FromBase64String(base64);
                using var ms = new System.IO.MemoryStream(bytes);
                return new TileEntry(new Bitmap(ms), null, dataUrl, dataUrl);
            }
        }

        private readonly List<TileEntry> tiles = new();
        private TileEntry? selectedTile = null;
        private Bitmap? spriteSheetImage = null;
        private int gridSize = 20;
        private int brushSize = 1;
        private TileEntry?[,] mapData;
        private bool isMouseDown = false;
        private Border? selectedTileBorder = null;
    private Thread? previewThread;

        private Canvas? mapCanvas;
        private WrapPanel? tilePalette;
        private NumericUpDown? numTileWidth, numTileHeight, numSpacing, numMargin, numBrushSize;
        private ComboBox? cmbGridSize;

        private enum EditorMode
        {
            Tiles,
            Characters,
            Doodads
        }

        private EditorMode currentMode = EditorMode.Tiles;
        private List<Character> characters = new();
        private List<Doodad> doodads = new();

        private StackPanel? TilesToolsPanel => this.FindControl<StackPanel>("TilesTools");
        private StackPanel? CharactersToolsPanel => this.FindControl<StackPanel>("CharactersTools");
        private StackPanel? DoodadsToolsPanel => this.FindControl<StackPanel>("DoodadsTools");

        private Map map;

        public EditorWindow()
        {
            InitializeComponent();
            map = new Map(); // Initialize the map field
            mapData = new TileEntry?[gridSize, gridSize];
            AttachEvents();
            InitializeEditorUI();
            SyncMapFromEditorState();
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
            var btnMonoGamePreview = this.FindControl<Button>("BtnMonoGamePreview");
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
            if (btnMonoGamePreview != null)
                btnMonoGamePreview.Click += BtnMonoGamePreview_Click;
            if (btnSpriteEditor != null)
                btnSpriteEditor.Click += BtnSpriteEditor_Click;
            
            if (cmbGridSize != null)
                cmbGridSize.SelectionChanged += CmbGridSize_SelectionChanged;
            
            if (numBrushSize != null)
                numBrushSize.ValueChanged += (s, e) => brushSize = (int)(numBrushSize.Value ?? 1);

            SyncMapFromEditorState();
            RenderMap();
            SyncMapFromEditorState();
        }

        private void InitializeEditorUI()
        {
            this.KeyDown += (sender, e) =>
            {
                switch (e.Key)
                {
                    case Key.T:
                        SwitchToTilesMode(sender, e);
                        break;
                    case Key.C:
                        SwitchToCharactersMode(sender, e);
                        break;
                    case Key.D:
                        SwitchToDoodadsMode(sender, e);
                        break;
                }
            };

            // Initialize property panels
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = true;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
        }

        private async void BtnLoadSpriteSheet_Click(object? sender, RoutedEventArgs e)
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Sprite Sheet",
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (result != null && result.Count > 0)
            {
                var filePath = result[0].Path.LocalPath;
                try
                {
                    spriteSheetImage = new Bitmap(filePath);
                    Console.WriteLine($"Sprite sheet loaded: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading sprite sheet: {ex.Message}");
                }
            }
        }

        private void BtnSplitSheet_Click(object? sender, RoutedEventArgs e)
        {
            if (spriteSheetImage == null)
            {
                Console.WriteLine("Error: No sprite sheet loaded.");
                return;
            }

            if (numTileWidth == null || numTileHeight == null || 
                numSpacing == null || numMargin == null || tilePalette == null)
            {
                Console.WriteLine("Error: Missing UI elements for splitting parameters.");
                return;
            }

            var tw = (int)(numTileWidth.Value ?? 32);
            var th = (int)(numTileHeight.Value ?? 32);
            var spacing = (int)(numSpacing.Value ?? 0);
            var margin = (int)(numMargin.Value ?? 0);

            if (tw <= 0 || th <= 0)
            {
                Console.WriteLine("Error: Tile width and height must be greater than zero.");
                return;
            }

            tiles.Clear();
            tilePalette.Children.Clear();
            selectedTile = null;
            selectedTileBorder = null;

            using var skBitmap = BitmapToSKBitmap(spriteSheetImage);
            int cols = (skBitmap.Width - margin * 2 + spacing) / (tw + spacing);
            int rows = (skBitmap.Height - margin * 2 + spacing) / (th + spacing);

            if (cols <= 0 || rows <= 0)
            {
                Console.WriteLine("Error: Invalid tile dimensions or parameters. No tiles can be generated.");
                return;
            }

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int sx = margin + x * (tw + spacing);
                    int sy = margin + y * (th + spacing);

                    if (sx + tw > skBitmap.Width || sy + th > skBitmap.Height)
                    {
                        Console.WriteLine($"Skipping tile at ({x}, {y}) due to out-of-bounds dimensions.");
                        continue;
                    }

                    var surface = SKSurface.Create(new SKImageInfo(tw, th));
                    var canvas = surface.Canvas;
                    var srcRect = new SKRect(sx, sy, sx + tw, sy + th);
                    var destRect = new SKRect(0, 0, tw, th);
                    canvas.DrawBitmap(skBitmap, srcRect, destRect);

                    var image = surface.Snapshot();
                    var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = new System.IO.MemoryStream(data.ToArray());
                    var tileBitmap = new Bitmap(stream);
                    var entry = new TileEntry(tileBitmap, null);
                    tiles.Add(entry);
                    AddTileToPalette(entry);
                }
            }

            if (tiles.Count > 0)
            {
                selectedTile = tiles[0];
                Console.WriteLine($"Successfully split sprite sheet into {tiles.Count} tiles.");
            }
            else
            {
                Console.WriteLine("Error: No tiles generated from sprite sheet.");
            }
        }

        private async void BtnLoadTiles_Click(object? sender, RoutedEventArgs e)
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Tile Images",
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (result != null && result.Count > 0)
            {
                foreach (var file in result)
                {
                    var filePath = file.Path.LocalPath;
                    var bitmap = new Bitmap(filePath);
                    var entry = new TileEntry(bitmap, filePath);
                    tiles.Add(entry);
                    AddTileToPalette(entry);
                }
            }
        }

        private void AddTileToPalette(TileEntry entry)
        {
            if (tilePalette == null) return;

            var border = new Border
            {
                Width = 48,
                Height = 48,
                Margin = new Thickness(2),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2),
                Child = new Image { Source = entry.Bitmap, Stretch = Stretch.Uniform }
            };

            border.PointerPressed += (s, e) =>
            {
                selectedTile = entry;
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
                selectedTile = entry;
            }
        }

        private void CmbGridSize_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (cmbGridSize?.SelectedItem is ComboBoxItem item)
            {
                gridSize = int.Parse(item.Content?.ToString() ?? "20");
                mapData = new TileEntry?[gridSize, gridSize];
                SyncMapFromEditorState();
                RenderMap();
            }
        }

        private void BtnClearGrid_Click(object? sender, RoutedEventArgs e)
        {
            mapData = new TileEntry?[gridSize, gridSize];
            SyncMapFromEditorState();
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
                        var entry = mapData[x, y];
                        mapArray[y][x] = entry?.GetSerializedValue();
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
                var inputPath = txtPath.Text ?? string.Empty;
                var baseDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                var fullPath = IOPath.IsPathRooted(inputPath)
                    ? inputPath
                    : IOPath.Combine(baseDir, inputPath);

                if (!System.IO.File.Exists(fullPath))
                {
                    Console.WriteLine($"Error: Map file not found at {fullPath}.");
                    return;
                }

                try
                {
                    var loadedMap = Map.LoadFromJson(fullPath);
                    map = loadedMap;

                    gridSize = map.Cols;
                    mapData = new TileEntry?[gridSize, gridSize];

                    var mapDirectory = IOPath.GetDirectoryName(fullPath) ?? baseDir;

                    for (int y = 0; y < gridSize; y++)
                    {
                        for (int x = 0; x < gridSize; x++)
                        {
                            var stored = map.GetTileDataUrl(x, y);
                            if (string.IsNullOrWhiteSpace(stored))
                                continue;

                            if (stored.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            {
                                mapData[x, y] = TileEntry.FromDataUrl(stored);
                            }
                            else
                            {
                                var resolved = IOPath.IsPathRooted(stored)
                                    ? stored
                                    : IOPath.Combine(mapDirectory, stored);

                                if (System.IO.File.Exists(resolved))
                                {
                                    mapData[x, y] = new TileEntry(new Bitmap(resolved), resolved, null, stored);
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: Tile asset not found at {resolved}.");
                                    mapData[x, y] = null;
                                }
                            }
                        }
                    }

                    characters = new List<Character>(map.Characters);
                    doodads = new List<Doodad>(map.Doodads);

                    if (numTileWidth != null) numTileWidth.Value = map.TileW;
                    if (numTileHeight != null) numTileHeight.Value = map.TileH;

                    SyncMapFromEditorState();
                    RenderMap();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading map: {ex.Message}");
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
                            mapData[x, y] = selectedTile.Clone();
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

        private void SwitchMode(EditorMode mode)
        {
            currentMode = mode;
            RenderMap();
        }

        private void RenderMap()
        {
            if (mapCanvas == null) return;
            mapCanvas.Children.Clear();

            float cellSize = 600f / gridSize;
            mapCanvas.Width = gridSize * cellSize;
            mapCanvas.Height = gridSize * cellSize;

            // Render tiles
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    var entry = mapData[x, y];
                    if (entry != null)
                    {
                        var img = new Image
                        {
                            Source = entry.Bitmap,
                            Width = cellSize,
                            Height = cellSize
                        };
                        Canvas.SetLeft(img, x * cellSize);
                        Canvas.SetTop(img, y * cellSize);
                        mapCanvas.Children.Add(img);
                    }
                }
            }

            // Render characters
            if (currentMode == EditorMode.Characters || currentMode == EditorMode.Tiles)
            {
                foreach (var character in characters)
                {
                    var rect = new Rectangle
                    {
                        Width = cellSize,
                        Height = cellSize,
                        Fill = new SolidColorBrush(character.Color),
                        Stroke = Brushes.Black,
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(rect, character.TileX * cellSize);
                    Canvas.SetTop(rect, character.TileY * cellSize);
                    mapCanvas.Children.Add(rect);
                }
            }

            // Render doodads
            if (currentMode == EditorMode.Doodads || currentMode == EditorMode.Tiles)
            {
                foreach (var doodad in doodads)
                {
                    var rect = new Rectangle
                    {
                        Width = cellSize,
                        Height = cellSize,
                        Fill = doodad.Sprite != null ? new ImageBrush(doodad.Sprite) : new SolidColorBrush(doodad.Color),
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(rect, doodad.TileX * cellSize);
                    Canvas.SetTop(rect, doodad.TileY * cellSize);
                    mapCanvas.Children.Add(rect);
                }
            }

            // Render grid lines
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

        private void SyncMapFromEditorState()
        {
            int tileW = (int)(numTileWidth?.Value ?? 32);
            int tileH = (int)(numTileHeight?.Value ?? 32);
            var tilesSnapshot = new string?[gridSize, gridSize];

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    tilesSnapshot[y, x] = mapData[x, y]?.GetSerializedValue();
                }
            }

            var triggerSnapshot = new List<BehaviorTrigger>(map.Triggers);
            map.InitializeFromArray(gridSize, gridSize, tileW, tileH, tilesSnapshot, characters, doodads, triggerSnapshot);
        }

        private void BtnMonoGamePreview_Click(object? sender, RoutedEventArgs e)
        {
            LaunchMonoGamePreview();
        }

        private void LaunchMonoGamePreview()
        {
            if (previewThread != null && previewThread.IsAlive)
            {
                Console.WriteLine("MonoGame preview already running.");
                return;
            }

            SyncMapFromEditorState();
            var previewMap = map.Clone();

            previewThread = new Thread(() =>
            {
                try
                {
                    using var game = new DotGameAvalonia.MonoGameLayer.EditorGame(previewMap);
                    game.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MonoGame preview error: {ex.Message}");
                }
                finally
                {
                    previewThread = null;
                }
            })
            {
                IsBackground = true,
                Name = "MonoGamePreviewThread"
            };

            previewThread.Start();
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            try
            {
                using var stream = new System.IO.MemoryStream();
                bitmap.Save(stream);
                stream.Position = 0;
                return SKBitmap.Decode(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting Bitmap to SKBitmap: {ex.Message}");
                throw;
            }
        }

        private void SwitchToTilesMode(object? sender, RoutedEventArgs e)
        {
            if (sender == null) return;
            currentMode = EditorMode.Tiles;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = true;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
        }

        private void SwitchToCharactersMode(object? sender, RoutedEventArgs e)
        {
            if (sender == null) return;
            currentMode = EditorMode.Characters;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = true;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
        }

        private void SwitchToDoodadsMode(object? sender, RoutedEventArgs e)
        {
            if (sender == null) return;
            currentMode = EditorMode.Doodads;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = true;
        }

        private void PlaceCharacter(int tileX, int tileY, Character character)
        {
            if (!map.InBounds(tileX, tileY))
            {
                Console.WriteLine("Character placement out of bounds.");
                return;
            }

            character.TileX = tileX;
            character.TileY = tileY;
            characters.Add(character);
            Console.WriteLine($"Placed character {character.Name} at ({tileX}, {tileY}).");
        }

        private void RemoveCharacter(Character character)
        {
            if (characters.Remove(character))
            {
                Console.WriteLine($"Removed character {character.Name}.");
            }
        }

        private void PlaceDoodad(int tileX, int tileY, Doodad doodad)
        {
            if (!map.InBounds(tileX, tileY))
            {
                Console.WriteLine("Doodad placement out of bounds.");
                return;
            }

            doodad.TileX = tileX;
            doodad.TileY = tileY;
            doodads.Add(doodad);
            Console.WriteLine($"Placed doodad {doodad.Type} at ({tileX}, {tileY}).");
        }

        private void RemoveDoodad(Doodad doodad)
        {
            if (doodads.Remove(doodad))
            {
                Console.WriteLine($"Removed doodad {doodad.Type}.");
            }
        }

        private void AddBehaviorTrigger(int tileX, int tileY, string triggerName)
        {
            if (!map.InBounds(tileX, tileY))
            {
                Console.WriteLine("Trigger placement out of bounds.");
                return;
            }

            var trigger = new BehaviorTrigger
            {
                TileX = tileX,
                TileY = tileY,
                Name = triggerName
            };

            map.AddTrigger(trigger);
            Console.WriteLine($"Added behavior trigger '{triggerName}' at ({tileX}, {tileY}).");
        }

        private void RemoveBehaviorTrigger(BehaviorTrigger trigger)
        {
            map.RemoveTrigger(trigger);
            Console.WriteLine($"Removed behavior trigger '{trigger.Name}'.");
        }
    }
}
