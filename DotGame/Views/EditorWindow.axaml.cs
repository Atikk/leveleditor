using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DotGameAvalonia.Models;
using DotGameAvalonia.MonoGameLayer;
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
    private EditorGame? previewGame;
    private readonly object previewGameLock = new();

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
    private Character? pendingCharacterTemplate;
    private Doodad? pendingDoodadTemplate;
    private Character? selectedCharacter;
    private Doodad? selectedDoodad;
    private bool suppressGridSizeEvent;

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
            var btnTileSelect = this.FindControl<Button>("BtnTileSelect");
            var btnTileFill = this.FindControl<Button>("BtnTileFill");
            var btnAddCharacter = this.FindControl<Button>("BtnAddCharacter");
            var btnEditCharacter = this.FindControl<Button>("BtnEditCharacter");
            var btnAddDoodad = this.FindControl<Button>("BtnAddDoodad");
            var btnEditDoodad = this.FindControl<Button>("BtnEditDoodad");

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
            if (btnTileSelect != null)
                btnTileSelect.Click += BtnTileSelect_Click;
            if (btnTileFill != null)
                btnTileFill.Click += BtnTileFill_Click;
            if (btnAddCharacter != null)
                btnAddCharacter.Click += BtnAddCharacter_Click;
            if (btnEditCharacter != null)
                btnEditCharacter.Click += BtnEditCharacter_Click;
            if (btnAddDoodad != null)
                btnAddDoodad.Click += BtnAddDoodad_Click;
            if (btnEditDoodad != null)
                btnEditDoodad.Click += BtnEditDoodad_Click;
            
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
            if (suppressGridSizeEvent)
                return;

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
            try
            {
                SyncMapFromEditorState();

                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Map",
                    SuggestedFileName = "mymap.json",
                    FileTypeChoices = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("JSON Map") { Patterns = new[] { "*.json" } }
                    }
                });

                if (file == null)
                    return;

                var path = file.Path?.LocalPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("Error: Unable to resolve the selected save path.");
                    return;
                }

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

                var characterData = characters.Select(c => new
                {
                    TileX = c.TileX,
                    TileY = c.TileY,
                    Name = c.Name,
                    Class = c.Class,
                    BehaviorScript = c.BehaviorScript,
                    TriggerEvent = c.TriggerEvent,
                    Color = c.Color.ToString()
                }).ToList();

                var doodadData = doodads.Select(d => new
                {
                    TileX = d.TileX,
                    TileY = d.TileY,
                    Type = d.Type,
                    Collidable = d.Collidable,
                    Interactable = d.Interactable,
                    Animated = d.Animated,
                    Trigger = d.Trigger,
                    Color = d.Color.ToString(),
                    OnInteract = d.OnInteract
                }).ToList();

                var triggerData = map.Triggers.Select(t => new
                {
                    TileX = t.TileX,
                    TileY = t.TileY,
                    Name = t.Name
                }).ToList();

                var mapObject = new
                {
                    cols = gridSize,
                    rows = gridSize,
                    tileW = tw,
                    tileH = th,
                    map = mapArray,
                    characters = characterData,
                    doodads = doodadData,
                    triggers = triggerData,
                    externalTileMapAsset = map.ExternalTileMapAsset
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(path, JsonSerializer.Serialize(mapObject, options));
                Console.WriteLine($"Map saved to {path}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving map: {ex.Message}");
            }
        }

        private async void BtnLoadMap_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var results = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Load Map",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("JSON Map") { Patterns = new[] { "*.json" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (results == null || results.Count == 0)
                    return;

                var path = results[0].Path?.LocalPath;
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                {
                    Console.WriteLine("Error: Selected map file could not be resolved.");
                    return;
                }

                var loadedMap = Map.LoadFromJson(path);
                map = loadedMap;

                gridSize = Math.Max(1, map.Cols);
                mapData = new TileEntry?[gridSize, gridSize];

                var mapDirectory = IOPath.GetDirectoryName(path) ?? AppContext.BaseDirectory ?? Environment.CurrentDirectory;

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

                characters = map.Characters.Select(CloneCharacterForEditor).ToList();
                doodads = map.Doodads.Select(CloneDoodadForEditor).ToList();
                selectedCharacter = null;
                selectedDoodad = null;
                pendingCharacterTemplate = null;
                pendingDoodadTemplate = null;

                if (numTileWidth != null) numTileWidth.Value = map.TileW;
                if (numTileHeight != null) numTileHeight.Value = map.TileH;

                UpdateGridSizeSelection(gridSize);

                SyncMapFromEditorState();
                RenderMap();
                Console.WriteLine($"Loaded map from {path}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading map: {ex.Message}");
            }
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

            float cellSize = mapCanvas.Width > 0 ? (float)mapCanvas.Width / gridSize : 600f / gridSize;
            int gridX = Math.Clamp((int)(location.X / cellSize), 0, gridSize - 1);
            int gridY = Math.Clamp((int)(location.Y / cellSize), 0, gridSize - 1);

            switch (currentMode)
            {
                case EditorMode.Tiles:
                    PaintTiles(gridX, gridY, erase);
                    RenderMap();
                    break;
                case EditorMode.Characters:
                    HandleCharacterPlacement(gridX, gridY, erase);
                    RenderMap();
                    SyncMapFromEditorState();
                    break;
                case EditorMode.Doodads:
                    HandleDoodadPlacement(gridX, gridY, erase);
                    RenderMap();
                    SyncMapFromEditorState();
                    break;
                default:
                    break;
            }
        }

        private void PaintTiles(int gridX, int gridY, bool erase)
        {
            if (!erase && selectedTile == null)
            {
                Console.WriteLine("Select a tile from the palette before painting.");
                return;
            }

            int offset = brushSize / 2;

            for (int dy = 0; dy < brushSize; dy++)
            {
                for (int dx = 0; dx < brushSize; dx++)
                {
                    int x = gridX - offset + dx;
                    int y = gridY - offset + dy;
                    if (x >= 0 && y >= 0 && x < gridSize && y < gridSize)
                    {
                        if (erase)
                        {
                            mapData[x, y] = null;
                        }
                        else if (selectedTile != null)
                        {
                            mapData[x, y] = selectedTile.Clone();
                        }
                    }
                }
            }
        }

        private void HandleCharacterPlacement(int tileX, int tileY, bool erase)
        {
            if (erase)
            {
                if (!RemoveCharacterAt(tileX, tileY))
                {
                    Console.WriteLine($"No character present at ({tileX}, {tileY}).");
                }
                return;
            }

            if (pendingCharacterTemplate == null)
            {
                var existing = GetCharacterAt(tileX, tileY);
                if (existing != null)
                {
                    selectedCharacter = existing;
                    Console.WriteLine($"Selected character {existing.Name} at ({tileX}, {tileY}).");
                }
                else
                {
                    Console.WriteLine("No character template selected. Use Add Character to create one.");
                }
                return;
            }

            var placement = CloneCharacterTemplate(pendingCharacterTemplate);
            placement.TileX = tileX;
            placement.TileY = tileY;

            RemoveCharacterAt(tileX, tileY);
            characters.Add(placement);
            selectedCharacter = placement;
            Console.WriteLine($"Placed character {placement.Name} at ({tileX}, {tileY}).");
        }

        private void HandleDoodadPlacement(int tileX, int tileY, bool erase)
        {
            if (erase)
            {
                if (!RemoveDoodadAt(tileX, tileY))
                {
                    Console.WriteLine($"No doodad present at ({tileX}, {tileY}).");
                }
                return;
            }

            if (pendingDoodadTemplate == null)
            {
                var existing = GetDoodadAt(tileX, tileY);
                if (existing != null)
                {
                    selectedDoodad = existing;
                    Console.WriteLine($"Selected doodad {existing.Type} at ({tileX}, {tileY}).");
                }
                else
                {
                    Console.WriteLine("No doodad template selected. Use Add Doodad to create one.");
                }
                return;
            }

            var placement = CloneDoodadTemplate(pendingDoodadTemplate);
            placement.TileX = tileX;
            placement.TileY = tileY;

            RemoveDoodadAt(tileX, tileY);
            doodads.Add(placement);
            selectedDoodad = placement;
            Console.WriteLine($"Placed doodad {placement.Type} at ({tileX}, {tileY}).");
        }

        private Character? GetCharacterAt(int tileX, int tileY)
        {
            return characters.FirstOrDefault(c => c.TileX == tileX && c.TileY == tileY);
        }

        private bool RemoveCharacterAt(int tileX, int tileY)
        {
            var existing = GetCharacterAt(tileX, tileY);
            if (existing == null)
                return false;

            RemoveCharacter(existing);
            if (ReferenceEquals(selectedCharacter, existing))
                selectedCharacter = null;
            return true;
        }

        private Doodad? GetDoodadAt(int tileX, int tileY)
        {
            return doodads.FirstOrDefault(d => d.TileX == tileX && d.TileY == tileY);
        }

        private bool RemoveDoodadAt(int tileX, int tileY)
        {
            var existing = GetDoodadAt(tileX, tileY);
            if (existing == null)
                return false;

            RemoveDoodad(existing);
            if (ReferenceEquals(selectedDoodad, existing))
                selectedDoodad = null;
            return true;
        }

        private Character CloneCharacterTemplate(Character template)
        {
            var clone = new Character(template.TileX, template.TileY, template.Class, template.Name)
            {
                Sprite = template.Sprite,
                Color = template.Color,
                BehaviorScript = template.BehaviorScript,
                TriggerEvent = template.TriggerEvent
            };
            clone.Direction = template.Direction;
            clone.CurrentHP = template.CurrentHP;
            return clone;
        }

        private Character CloneCharacterForEditor(Character source)
        {
            var clone = CloneCharacterTemplate(source);
            clone.TileX = source.TileX;
            clone.TileY = source.TileY;
            return clone;
        }

        private Doodad CloneDoodadTemplate(Doodad template)
        {
            return new Doodad(template.TileX, template.TileY, template.Type)
            {
                Sprite = template.Sprite,
                Color = template.Color,
                Collidable = template.Collidable,
                Interactable = template.Interactable,
                Animated = template.Animated,
                Trigger = template.Trigger,
                OnInteract = template.OnInteract
            };
        }

        private Doodad CloneDoodadForEditor(Doodad source)
        {
            var clone = CloneDoodadTemplate(source);
            clone.TileX = source.TileX;
            clone.TileY = source.TileY;
            return clone;
        }

        private static Color ParseColorOrDefault(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return Color.Parse(value);
            }
            catch
            {
                return fallback;
            }
        }

        private void UpdateGridSizeSelection(int size)
        {
            if (cmbGridSize == null)
                return;

            var match = cmbGridSize.Items?.OfType<ComboBoxItem>().FirstOrDefault(item =>
                int.TryParse(item.Content?.ToString(), out var parsed) && parsed == size);

            if (match != null)
            {
                suppressGridSizeEvent = true;
                cmbGridSize.SelectedItem = match;
                suppressGridSizeEvent = false;
            }
        }

        private async Task<Character?> PromptCharacterTemplateAsync()
        {
            var dialog = new CharacterCreationWindow();
            var result = await dialog.ShowDialog<bool?>(this);
            if (result != true)
                return null;

            var name = string.IsNullOrWhiteSpace(dialog.SelectedName) ? "Hero" : dialog.SelectedName!;
            var character = new Character(0, 0, dialog.SelectedClass, name)
            {
                Sprite = dialog.SelectedSprite,
                Color = dialog.SelectedSprite != null ? Colors.Transparent : Colors.DeepSkyBlue
            };
            return character;
        }

        private async Task<(Character? character, bool delete)> PromptCharacterEditAsync(Character target)
        {
            var dialog = new Window
            {
                Title = "Edit Character",
                Width = 360,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Name" });
            var nameBox = new TextBox { Text = target.Name };
            stack.Children.Add(nameBox);

            stack.Children.Add(new TextBlock { Text = "Class" });
            var classCombo = new ComboBox();
            classCombo.ItemsSource = Enum.GetValues(typeof(CharacterClass));
            classCombo.SelectedItem = target.Class;
            stack.Children.Add(classCombo);

            stack.Children.Add(new TextBlock { Text = "Color (#AARRGGBB)" });
            var colorBox = new TextBox { Text = target.Color.ToString() };
            stack.Children.Add(colorBox);

            stack.Children.Add(new TextBlock { Text = "Trigger Event" });
            var triggerBox = new TextBox { Text = target.TriggerEvent ?? string.Empty };
            stack.Children.Add(triggerBox);

            stack.Children.Add(new TextBlock { Text = "Behavior Script" });
            var scriptBox = new TextBox { Text = target.BehaviorScript ?? string.Empty };
            stack.Children.Add(scriptBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10
            };
            var saveButton = new Button { Content = "Save", Width = 80 };
            var deleteButton = new Button { Content = "Delete", Width = 80 };
            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            Character? updated = null;
            bool deleteRequested = false;

            saveButton.Click += (_, __) =>
            {
                var selectedClass = classCombo.SelectedItem is CharacterClass cls ? cls : target.Class;
                var name = string.IsNullOrWhiteSpace(nameBox.Text) ? target.Name : nameBox.Text!;
                var clone = new Character(target.TileX, target.TileY, selectedClass, name)
                {
                    Sprite = target.Sprite,
                    Color = ParseColorOrDefault(colorBox.Text, target.Color),
                    BehaviorScript = string.IsNullOrWhiteSpace(scriptBox.Text) ? null : scriptBox.Text,
                    TriggerEvent = string.IsNullOrWhiteSpace(triggerBox.Text) ? null : triggerBox.Text
                };
                clone.Direction = target.Direction;
                clone.CurrentHP = Math.Min(target.CurrentHP, clone.Attributes.MaxHP);
                updated = clone;
                dialog.Close();
            };

            deleteButton.Click += (_, __) =>
            {
                deleteRequested = true;
                dialog.Close();
            };

            cancelButton.Click += (_, __) => dialog.Close();

            await dialog.ShowDialog(this);
            return (updated, deleteRequested);
        }

        private async Task<Doodad?> PromptDoodadTemplateAsync()
        {
            var (doodad, _) = await PromptDoodadEditAsync(null);
            return doodad;
        }

        private async Task<(Doodad? doodad, bool delete)> PromptDoodadEditAsync(Doodad? target)
        {
            var dialog = new Window
            {
                Title = target == null ? "Add Doodad" : "Edit Doodad",
                Width = 360,
                Height = 340,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Type" });
            var typeBox = new TextBox { Text = target?.Type ?? "Doodad" };
            stack.Children.Add(typeBox);

            stack.Children.Add(new TextBlock { Text = "Color (#AARRGGBB)" });
            var colorBox = new TextBox { Text = (target?.Color ?? Colors.Transparent).ToString() };
            stack.Children.Add(colorBox);

            var collidableBox = new CheckBox { Content = "Collidable", IsChecked = target?.Collidable ?? false };
            stack.Children.Add(collidableBox);

            var interactableBox = new CheckBox { Content = "Interactable", IsChecked = target?.Interactable ?? false };
            stack.Children.Add(interactableBox);

            var animatedBox = new CheckBox { Content = "Animated", IsChecked = target?.Animated ?? false };
            stack.Children.Add(animatedBox);

            stack.Children.Add(new TextBlock { Text = "Trigger" });
            var triggerBox = new TextBox { Text = target?.Trigger ?? string.Empty };
            stack.Children.Add(triggerBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10
            };
            var saveButton = new Button { Content = "Save", Width = 80 };
            var deleteButton = new Button { Content = "Delete", Width = 80, IsVisible = target != null };
            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            buttonPanel.Children.Add(saveButton);
            if (target != null) buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            Doodad? updated = null;
            bool deleteRequested = false;

            saveButton.Click += (_, __) =>
            {
                var type = string.IsNullOrWhiteSpace(typeBox.Text) ? (target?.Type ?? "Doodad") : typeBox.Text!;
                var doodad = new Doodad(target?.TileX ?? 0, target?.TileY ?? 0, type)
                {
                    Sprite = target?.Sprite,
                    Color = ParseColorOrDefault(colorBox.Text, target?.Color ?? Colors.Transparent),
                    Collidable = collidableBox.IsChecked ?? false,
                    Interactable = interactableBox.IsChecked ?? false,
                    Animated = animatedBox.IsChecked ?? false,
                    Trigger = string.IsNullOrWhiteSpace(triggerBox.Text) ? null : triggerBox.Text,
                    OnInteract = target?.OnInteract
                };
                updated = doodad;
                dialog.Close();
            };

            deleteButton.Click += (_, __) =>
            {
                deleteRequested = true;
                dialog.Close();
            };

            cancelButton.Click += (_, __) => dialog.Close();

            await dialog.ShowDialog(this);
            return (updated, deleteRequested);
        }

        private void BtnTileSelect_Click(object? sender, RoutedEventArgs e)
        {
            SwitchToTilesMode(sender, e);
            if (selectedTile == null && tiles.Count > 0)
            {
                selectedTile = tiles[0];
                if (tilePalette?.Children.Count > 0 && tilePalette.Children[0] is Border border)
                {
                    if (selectedTileBorder != null)
                        selectedTileBorder.Background = Brushes.Transparent;
                    border.Background = Brushes.LightBlue;
                    selectedTileBorder = border;
                }
            }
            Console.WriteLine("Tile painting mode enabled. Select a tile from the palette to change the brush.");
        }

        private void BtnTileFill_Click(object? sender, RoutedEventArgs e)
        {
            if (selectedTile == null)
            {
                Console.WriteLine("Select a tile before using Fill Area.");
                return;
            }

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    mapData[x, y] = selectedTile.Clone();
                }
            }

            RenderMap();
            Console.WriteLine("Filled the entire map with the current tile selection.");
            SyncMapFromEditorState();
        }

        private async void BtnAddCharacter_Click(object? sender, RoutedEventArgs e)
        {
            var template = await PromptCharacterTemplateAsync();
            if (template == null)
                return;

            pendingCharacterTemplate = CloneCharacterTemplate(template);
            SwitchToCharactersMode(sender, e);
            Console.WriteLine("Character template ready. Left click on the map to place it.");
        }

        private async void BtnEditCharacter_Click(object? sender, RoutedEventArgs e)
        {
            if (characters.Count == 0)
            {
                Console.WriteLine("There are no characters to edit.");
                return;
            }

            var target = selectedCharacter ?? characters[0];
            var (updated, delete) = await PromptCharacterEditAsync(target);

            if (delete)
            {
                characters.Remove(target);
                if (ReferenceEquals(selectedCharacter, target))
                    selectedCharacter = null;
                Console.WriteLine($"Deleted character {target.Name}.");
            }
            else if (updated != null)
            {
                var index = characters.IndexOf(target);
                if (index >= 0)
                {
                    characters[index] = updated;
                    selectedCharacter = updated;
                    Console.WriteLine($"Updated character {updated.Name}.");
                }
            }

            RenderMap();
            SyncMapFromEditorState();
        }

        private async void BtnAddDoodad_Click(object? sender, RoutedEventArgs e)
        {
            var doodad = await PromptDoodadTemplateAsync();
            if (doodad == null)
                return;

            pendingDoodadTemplate = CloneDoodadTemplate(doodad);
            SwitchToDoodadsMode(sender, e);
            Console.WriteLine("Doodad template ready. Left click on the map to place it.");
        }

        private async void BtnEditDoodad_Click(object? sender, RoutedEventArgs e)
        {
            if (doodads.Count == 0)
            {
                Console.WriteLine("There are no doodads to edit.");
                return;
            }

            var target = selectedDoodad ?? doodads[0];
            var (updated, delete) = await PromptDoodadEditAsync(target);

            if (delete)
            {
                doodads.Remove(target);
                if (ReferenceEquals(selectedDoodad, target))
                    selectedDoodad = null;
                Console.WriteLine($"Deleted doodad {target.Type}.");
            }
            else if (updated != null)
            {
                updated.TileX = target.TileX;
                updated.TileY = target.TileY;
                var index = doodads.IndexOf(target);
                if (index >= 0)
                {
                    doodads[index] = updated;
                    selectedDoodad = updated;
                    Console.WriteLine($"Updated doodad {updated.Type}.");
                }
            }

            RenderMap();
            SyncMapFromEditorState();
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
            map.InitializeFromArray(gridSize, gridSize, tileW, tileH, tilesSnapshot, characters, doodads, triggerSnapshot, map.ExternalTileMapAsset);
            NotifyPreviewMapUpdate();
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
                EditorGame? localGame = null;
                try
                {
                    localGame = new EditorGame(previewMap);
                    localGame.TriggerActivated += (trigger, entity) =>
                    {
                        var triggerName = string.IsNullOrWhiteSpace(trigger.Name) ? "UnnamedTrigger" : trigger.Name;
                        Console.WriteLine($"[Preview] Trigger '{triggerName}' fired by '{entity.Name}'.");
                    };
                    lock (previewGameLock)
                    {
                        previewGame = localGame;
                    }

                    localGame.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MonoGame preview error: {ex.Message}");
                }
                finally
                {
                    localGame?.Dispose();
                    lock (previewGameLock)
                    {
                        previewGame = null;
                    }
                    previewThread = null;
                }
            })
            {
                IsBackground = true,
                Name = "MonoGamePreviewThread"
            };

            previewThread.Start();
        }

        private void NotifyPreviewMapUpdate()
        {
            EditorGame? game;
            lock (previewGameLock)
            {
                game = previewGame;
            }

            if (game == null)
                return;

            try
            {
                var snapshot = map.Clone();
                game.RequestMapSwap(snapshot);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MonoGame preview sync error: {ex.Message}");
            }
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
            currentMode = EditorMode.Tiles;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = true;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
            RenderMap();
        }

        private void SwitchToCharactersMode(object? sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Characters;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = true;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
            RenderMap();
        }

        private void SwitchToDoodadsMode(object? sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Doodads;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = true;
            RenderMap();
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
