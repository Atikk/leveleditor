using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using global::Avalonia;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using SkiaSharp;
using Dotgame.Avalonia.Models;

namespace Dotgame.Avalonia.Models
{
    public sealed class Map
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int TileW { get; private set; }
        public int TileH { get; private set; }

        private string?[,] tiles = default!;
        private int?[,]? tileIds;
    private bool[,]? passability;
        private readonly Dictionary<string, SKBitmap> imageCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SKBitmap> atlasCache = new(StringComparer.Ordinal);
        public WriteableBitmap? Composite { get; private set; }

        private readonly List<Character> characters = new();
        private readonly List<Doodad> doodads = new();
        private readonly List<BehaviorTrigger> triggers = new();

        public string? ExternalTileMapAsset { get; private set; }
        public TilesetReference? Tileset { get; private set; }
        public string? SourceDirectory => sourceDirectory;

        private string? sourceDirectory;

        public Map() {}

        public static Map LoadFromJson(string path)
        {
            var json = File.ReadAllText(path);
            MapDto obj = JsonSerializer.Deserialize<MapDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("Invalid map JSON.");

            if (obj.cols <= 0 || obj.rows <= 0 || obj.tileW <= 0 || obj.tileH <= 0)
                throw new InvalidDataException("Map is missing required fields (cols/rows/tileW/tileH).");

            var tileMatrix = ResolveTileMatrix(obj);
            if (tileMatrix is null)
                throw new InvalidDataException("Map JSON is missing tile data; expected 'map' or 'layers[].tiles'.");

            var baseDirectory = Path.GetDirectoryName(path);
            var map = new Map
            {
                Cols = obj.cols,
                Rows = obj.rows,
                TileW = obj.tileW,
                TileH = obj.tileH,
                tiles = new string?[obj.rows, obj.cols],
                tileIds = null,
                sourceDirectory = baseDirectory
            };

            map.Tileset = CreateTileset(obj.tileset, baseDirectory, obj.tileW, obj.tileH);

            int?[,]? tileIdBuffer = null;
            for (int y = 0; y < obj.rows; y++)
            {
                var row = y < tileMatrix.Length ? tileMatrix[y] : null;
                for (int x = 0; x < obj.cols; x++)
                {
                    if (row == null || x >= row.Length)
                        continue;

                    var element = row[x];
                    if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                        continue;

                    if (element.ValueKind == JsonValueKind.String)
                    {
                        map.tiles[y, x] = element.GetString();
                    }
                    else if (element.ValueKind == JsonValueKind.Number)
                    {
                        tileIdBuffer ??= new int?[obj.rows, obj.cols];
                        if (element.TryGetInt32(out var id))
                        {
                            tileIdBuffer[y, x] = id;
                        }
                        else
                        {
                            var dbl = element.GetDouble();
                            tileIdBuffer[y, x] = (int)Math.Round(dbl);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException($"Unsupported tile value type '{element.ValueKind}' at ({x},{y}).");
                    }
                }
            }

            map.tileIds = tileIdBuffer;
            map.ExternalTileMapAsset = obj.externalTileMapAsset;

            // Load passability grid if present and dimensions match
            if (obj is not null)
            {
                var passArr = obj.passability;
                if (passArr != null && passArr.Length == obj.rows)
                {
                    try
                    {
                        var pb = new bool[obj.rows, obj.cols];
                        for (int y = 0; y < obj.rows; y++)
                        {
                            var row = passArr[y];
                            if (row == null) continue;
                            for (int x = 0; x < Math.Min(obj.cols, row.Length); x++)
                            {
                                pb[y, x] = row[x];
                            }
                        }
                        map.passability = pb;
                    }
                    catch
                    {
                        // ignore malformed passability data
                    }
                }
            }

            var chars = obj?.characters;
            if (chars != null)
            {
                foreach (var charDto in chars)
                {
                    var character = new Character(charDto.TileX, charDto.TileY, charDto.Class, charDto.Name)
                    {
                        BehaviorScript = charDto.BehaviorScript,
                        TriggerEvent = charDto.TriggerEvent
                    };
                    if (!string.IsNullOrWhiteSpace(charDto.Color))
                    {
                        try
                        {
                            character.Color = Color.Parse(charDto.Color);
                        }
                        catch
                        {
                            // ignore malformed color values
                        }
                    }
                    map.characters.Add(character);
                }
            }

            var doodadsArr = obj?.doodads;
            if (doodadsArr != null)
            {
                foreach (var doodadDto in doodadsArr)
                {
                    var doodad = new Doodad(doodadDto.TileX, doodadDto.TileY, doodadDto.Type)
                    {
                        Collidable = doodadDto.Collidable,
                        Interactable = doodadDto.Interactable,
                        Animated = doodadDto.Animated,
                        Trigger = doodadDto.Trigger,
                        OnInteract = doodadDto.OnInteract
                    };
                    if (!string.IsNullOrWhiteSpace(doodadDto.Color))
                    {
                        try
                        {
                            doodad.Color = Color.Parse(doodadDto.Color);
                        }
                        catch
                        {
                            // ignore malformed color values
                        }
                    }
                    map.doodads.Add(doodad);
                }
            }

            map.ExternalTileMapAsset = obj.externalTileMapAsset;
            var triggersArr = obj?.triggers;
            if (triggersArr != null)
            {
                foreach (var triggerDto in triggersArr)
                {
                    var trigger = new BehaviorTrigger
                    {
                        TileX = triggerDto.TileX,
                        TileY = triggerDto.TileY,
                        Name = triggerDto.Name ?? string.Empty
                    };
                    map.AddTrigger(trigger);
                }
            }
            map.BuildComposite();
            return map;
        }

        /// <summary>
        /// Load a Map from a JSON string in-memory. This mirrors <see cref="LoadFromJson(string)"/>
        /// but avoids filesystem IO so callers can supply raw JSON directly.
        /// </summary>
        /// <param name="json">The JSON payload representing the map.</param>
        /// <param name="baseDirectory">Optional base directory used to resolve relative asset paths.</param>
        public static Map LoadFromJsonString(string json, string? baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("json must be non-empty", nameof(json));

            MapDto obj = JsonSerializer.Deserialize<MapDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("Invalid map JSON.");

            if (obj.cols <= 0 || obj.rows <= 0 || obj.tileW <= 0 || obj.tileH <= 0)
                throw new InvalidDataException("Map is missing required fields (cols/rows/tileW/tileH).");

            var tileMatrix = ResolveTileMatrix(obj);
            if (tileMatrix is null)
                throw new InvalidDataException("Map JSON is missing tile data; expected 'map' or 'layers[].tiles'.");

            var map = new Map
            {
                Cols = obj.cols,
                Rows = obj.rows,
                TileW = obj.tileW,
                TileH = obj.tileH,
                tiles = new string?[obj.rows, obj.cols],
                tileIds = null,
                sourceDirectory = baseDirectory
            };

            map.Tileset = CreateTileset(obj.tileset, baseDirectory, obj.tileW, obj.tileH);

            int?[,]? tileIdBuffer = null;
            for (int y = 0; y < obj.rows; y++)
            {
                var row = y < tileMatrix.Length ? tileMatrix[y] : null;
                for (int x = 0; x < obj.cols; x++)
                {
                    if (row == null || x >= row.Length)
                        continue;

                    var element = row[x];
                    if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                        continue;

                    if (element.ValueKind == JsonValueKind.String)
                    {
                        map.tiles[y, x] = element.GetString();
                    }
                    else if (element.ValueKind == JsonValueKind.Number)
                    {
                        tileIdBuffer ??= new int?[obj.rows, obj.cols];
                        if (element.TryGetInt32(out var id))
                        {
                            tileIdBuffer[y, x] = id;
                        }
                        else
                        {
                            var dbl = element.GetDouble();
                            tileIdBuffer[y, x] = (int)Math.Round(dbl);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException($"Unsupported tile value type '{element.ValueKind}' at ({x},{y}).");
                    }
                }
            }

            map.tileIds = tileIdBuffer;
            map.ExternalTileMapAsset = obj.externalTileMapAsset;

            // Load passability grid if present and dimensions match
            if (obj is not null)
            {
                var passArr = obj.passability;
                if (passArr != null && passArr.Length == obj.rows)
                {
                    try
                    {
                        var pb = new bool[obj.rows, obj.cols];
                        for (int y = 0; y < obj.rows; y++)
                        {
                            var row = passArr[y];
                            if (row == null) continue;
                            for (int x = 0; x < Math.Min(obj.cols, row.Length); x++)
                            {
                                pb[y, x] = row[x];
                            }
                        }
                        map.passability = pb;
                    }
                    catch
                    {
                        // ignore malformed passability data
                    }
                }
            }

            if (obj.characters != null)
            {
                foreach (var charDto in obj.characters!)
                {
                    var character = new Character(charDto.TileX, charDto.TileY, charDto.Class, charDto.Name)
                    {
                        BehaviorScript = charDto.BehaviorScript,
                        TriggerEvent = charDto.TriggerEvent
                    };
                    if (!string.IsNullOrWhiteSpace(charDto.Color))
                    {
                        try
                        {
                            character.Color = Color.Parse(charDto.Color);
                        }
                        catch
                        {
                            // ignore malformed color values
                        }
                    }
                    map.characters.Add(character);
                }
            }

            if (obj.doodads != null)
            {
                foreach (var doodadDto in obj.doodads!)
                {
                    var doodad = new Doodad(doodadDto.TileX, doodadDto.TileY, doodadDto.Type)
                    {
                        Collidable = doodadDto.Collidable,
                        Interactable = doodadDto.Interactable,
                        Animated = doodadDto.Animated,
                        Trigger = doodadDto.Trigger,
                        OnInteract = doodadDto.OnInteract
                    };
                    if (!string.IsNullOrWhiteSpace(doodadDto.Color))
                    {
                        try
                        {
                            doodad.Color = Color.Parse(doodadDto.Color);
                        }
                        catch
                        {
                            // ignore malformed color values
                        }
                    }
                    map.doodads.Add(doodad);
                }
            }

            map.ExternalTileMapAsset = obj.externalTileMapAsset;
            if (obj.triggers != null)
            {
                foreach (var triggerDto in obj.triggers!)
                {
                    var trigger = new BehaviorTrigger
                    {
                        TileX = triggerDto.TileX,
                        TileY = triggerDto.TileY,
                        Name = triggerDto.Name ?? string.Empty
                    };
                    map.AddTrigger(trigger);
                }
            }
            map.BuildComposite();
            return map;
        }

        public bool InBounds(int tx, int ty) => tx >= 0 && ty >= 0 && tx < Cols && ty < Rows;

        public Rect TileRect(int tx, int ty) => new(tx * TileW, ty * TileH, TileW, TileH);

        // Expose tile data (data URL or asset key) for external renderers (e.g., MonoGame)
        public string? GetTileDataUrl(int tx, int ty)
        {
            if (!InBounds(tx, ty)) return null;
            return tiles![ty, tx];
        }

        public int? GetTileId(int tx, int ty)
        {
            if (!InBounds(tx, ty) || tileIds == null)
                return null;

            return tileIds[ty, tx];
        }

        // Returns true if the tile is passable (no collision). If passability grid is absent, defaults to true.
        public bool IsTilePassable(int tx, int ty)
        {
            if (!InBounds(tx, ty)) return false;
            if (passability == null) return true;
            return passability[ty, tx];
        }

        // Allow external editors to set a passability grid. Expects dimensions [rows,cols] (y,x)
        public void SetPassability(bool[,] grid)
        {
            if (grid == null) return;
            if (grid.GetLength(0) != Rows || grid.GetLength(1) != Cols)
                throw new ArgumentException("Passability grid dimensions must match map rows and cols.");

            // convert from [rows,cols] to internal [rows,cols] layout but internal indexing is [y,x]
            var pb = new bool[Rows, Cols];
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    pb[y, x] = grid[y, x];
                }
            }
            passability = pb;
        }

        // Return passability as jagged bool[y][x] where y = row
        public bool[][]? GetPassabilityAsJagged()
        {
            if (passability == null) return null;
            var result = new bool[Rows][];
            for (int y = 0; y < Rows; y++)
            {
                result[y] = new bool[Cols];
                for (int x = 0; x < Cols; x++)
                    result[y][x] = passability[y, x];
            }
            return result;
        }

    public bool HasTileIds => tileIds != null && Tileset is not null;

        public void InitializeFromArray(int cols, int rows, int tileW, int tileH, string?[,] tileData,
            IEnumerable<Character>? characterData = null,
            IEnumerable<Doodad>? doodadData = null,
            IEnumerable<BehaviorTrigger>? triggerData = null,
            string? externalTileMapAsset = null,
            int?[,]? tileIdData = null,
            TilesetReference? tileset = null,
            string? sourceDirectoryOverride = null)
        {
            if (tileData.GetLength(0) != rows || tileData.GetLength(1) != cols)
                throw new ArgumentException("Tile data dimensions do not match provided rows/cols.", nameof(tileData));

            Cols = cols;
            Rows = rows;
            TileW = tileW;
            TileH = tileH;
            tiles = new string?[rows, cols];
            tileIds = null;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                    tiles[y, x] = tileData[y, x];
            }

            if (tileIdData != null)
            {
                if (tileIdData.GetLength(0) != rows || tileIdData.GetLength(1) != cols)
                    throw new ArgumentException("Tile ID data dimensions do not match provided rows/cols.", nameof(tileIdData));

                tileIds = new int?[rows, cols];
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                        tileIds[y, x] = tileIdData[y, x];
                }
            }

            characters.Clear();
            if (characterData != null)
            {
                foreach (var c in characterData)
                    characters.Add(c);
            }

            doodads.Clear();
            if (doodadData != null)
            {
                foreach (var d in doodadData)
                    doodads.Add(d);
            }

            triggers.Clear();
            if (triggerData != null)
            {
                foreach (var t in triggerData)
                    triggers.Add(t);
            }

            ExternalTileMapAsset = externalTileMapAsset;
            Tileset = tileset;
            sourceDirectory = sourceDirectoryOverride;
        }

        public Map Clone()
        {
            var clone = new Map();
            if (Rows == 0 || Cols == 0)
                return clone;

            var copy = new string?[Rows, Cols];
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                    copy[y, x] = tiles![y, x];
            }

            int?[,]? idCopy = null;
            if (tileIds != null)
            {
                idCopy = new int?[Rows, Cols];
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Cols; x++)
                        idCopy[y, x] = tileIds[y, x];
                }
            }

            clone.InitializeFromArray(Cols, Rows, TileW, TileH, copy, characters, doodads, triggers, ExternalTileMapAsset, idCopy, Tileset, sourceDirectory);
            return clone;
        }

        public void BuildComposite()
        {
            var surface = SKSurface.Create(new SKImageInfo(Cols * TileW, Rows * TileH));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            var drewTileset = DrawTilesFromTileset(canvas);
            if (!drewTileset)
            {
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Cols; x++)
                        {
                            var url = tiles![y, x];
                            if (!string.IsNullOrEmpty(url))
                            {
                                var img = GetOrDecode(url!);
                                var destRect = SKRect.Create(x * TileW, y * TileH, TileW, TileH);
                                canvas.DrawBitmap(img, destRect);
                            }
                        }
                }
            }

            var image = surface.Snapshot();
            var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            stream.Position = 0;
            Composite = WriteableBitmap.Decode(stream);
            
            if (Composite == null)
            {
                throw new InvalidDataException("Failed to create map composite bitmap.");
            }
        }

        private SKBitmap GetOrDecode(string dataUrl)
        {
            if (imageCache.TryGetValue(dataUrl, out var cached))
                return cached;

            var comma = dataUrl.IndexOf(',');
            var base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
            var bytes = Convert.FromBase64String(base64);

            var bmp = SKBitmap.Decode(bytes);
            if (bmp == null)
            {
                throw new InvalidDataException("Failed to decode tile image data. The image data may be corrupted.");
            }
            
            imageCache[dataUrl] = bmp;
            return bmp;
        }

        private bool DrawTilesFromTileset(SKCanvas canvas)
        {
            var tileset = Tileset;
            var ids = tileIds;
            if (tileset is null || ids is null)
                return false;

            var atlasPath = tileset.AbsoluteTexturePath ?? tileset.TextureKey;
            if (string.IsNullOrWhiteSpace(atlasPath))
                return false;

            var atlas = GetOrLoadAtlas(atlasPath);
            if (atlas == null)
                return false;

            var anyDrawn = false;
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    var id = ids[y, x];
                    if (!id.HasValue)
                        continue;

                    if (!tileset.TryGetSourceRegion(id.Value, atlas.Width, out var region))
                        continue;

                    var sourceRect = new SKRectI(region.X, region.Y, region.X + region.Width, region.Y + region.Height);
                    var destRect = SKRect.Create(x * TileW, y * TileH, TileW, TileH);
                    canvas.DrawBitmap(atlas, sourceRect, destRect);
                    anyDrawn = true;
                }
            }

            return anyDrawn;
        }

        private SKBitmap? GetOrLoadAtlas(string key)
        {
            if (atlasCache.TryGetValue(key, out var cached))
                return cached;

            try
            {
                Bitmap bitmap;
                var dispose = false;
                if (File.Exists(key))
                {
                    bitmap = AssetManager.Instance.LoadBitmap(key);
                    dispose = false; // AssetManager caches and manages instances
                }
                else
                {
                    bitmap = AssetManager.Instance.LoadBitmap(key);
                }

                try
                {
                    using var skBitmap = BitmapToSKBitmap(bitmap);
                    var clone = skBitmap.Copy();
                    atlasCache[key] = clone;
                    return clone;
                }
                finally
                {
                    if (dispose)
                        bitmap.Dispose();
                }
            }
            catch
            {
                return null;
            }
        }

        public IReadOnlyList<Character> Characters => characters;
        public IReadOnlyList<Doodad> Doodads => doodads;
        public IReadOnlyList<BehaviorTrigger> Triggers => triggers;

        private static JsonElement[][]? ResolveTileMatrix(MapDto dto)
        {
            if (dto.map != null && dto.map.Length > 0)
                return dto.map;

            if (dto.layers != null && dto.layers.Count > 0)
            {
                var index = dto.activeLayerIndex ?? 0;
                index = Math.Clamp(index, 0, dto.layers.Count - 1);

                JsonElement[][]? candidate = null;
                if (index >= 0 && index < dto.layers.Count)
                    candidate = dto.layers[index]?.tiles;

                if (candidate == null)
                    candidate = dto.layers.Select(layer => layer?.tiles).FirstOrDefault(t => t != null);

                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private static TilesetReference? CreateTileset(TilesetDto? dto, string? baseDirectory, int defaultTileWidth, int defaultTileHeight)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.texture))
                return null;

            var textureKey = dto.texture!.Trim();
            var tileWidth = dto.tileWidth ?? defaultTileWidth;
            var tileHeight = dto.tileHeight ?? defaultTileHeight;
            if (tileWidth <= 0 || tileHeight <= 0)
                throw new InvalidDataException("Tileset tile dimensions must be positive.");

            var columns = dto.columns ?? 0;
            var tileCount = dto.tileCount ?? 0;
            var margin = dto.margin ?? 0;
            var spacing = dto.spacing ?? 0;
            var firstId = dto.firstId ?? 0;

            var absolute = ResolveTexturePath(textureKey, baseDirectory);
            return new TilesetReference(textureKey, absolute, tileWidth, tileHeight, columns, tileCount, margin, spacing, firstId);
        }

        private static string? ResolveTexturePath(string textureKey, string? baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(textureKey))
                return null;

            var normalized = textureKey.Replace('\\', '/');

            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
                return Path.GetFullPath(normalized);

            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                var combined = Path.Combine(baseDirectory, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(combined))
                    return Path.GetFullPath(combined);
            }

            var contentBase = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
            var fallback = Path.Combine(contentBase, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fallback))
                return Path.GetFullPath(fallback);

            return null;
        }

        public void AddCharacter(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (!InBounds(character.TileX, character.TileY))
                throw new ArgumentException("Character position is out of bounds.", nameof(character));

            characters.Add(character);
        }

        public void RemoveCharacter(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            characters.Remove(character);
        }

        public void AddDoodad(Doodad doodad)
        {
            if (doodad == null) throw new ArgumentNullException(nameof(doodad));
            if (!InBounds(doodad.TileX, doodad.TileY))
                throw new ArgumentException("Doodad position is out of bounds.", nameof(doodad));

            doodads.Add(doodad);
        }

        public void RemoveDoodad(Doodad doodad)
        {
            if (doodad == null) throw new ArgumentNullException(nameof(doodad));
            doodads.Remove(doodad);
        }

        public void AddTrigger(BehaviorTrigger trigger)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            if (!InBounds(trigger.TileX, trigger.TileY))
                throw new ArgumentException("Trigger position is out of bounds.", nameof(trigger));

            triggers.Add(trigger);
        }

        public void RemoveTrigger(BehaviorTrigger trigger)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            triggers.Remove(trigger);
        }

        public void RenderCharacters(SKCanvas canvas)
        {
            foreach (var character in characters)
            {
                var rect = TileRect(character.TileX, character.TileY);
                var skRect = new SKRect((float)rect.X, (float)rect.Y, (float)(rect.X + rect.Width), (float)(rect.Y + rect.Height));

                if (character.Sprite != null)
                {
                    using var skSprite = BitmapToSKBitmap(character.Sprite);
                    canvas.DrawBitmap(skSprite, skRect);
                }
                else
                {
                    var paint = new SKPaint { Color = ToSKColor(character.Color), Style = SKPaintStyle.Fill };
                    canvas.DrawRect(skRect, paint);
                }
            }
        }

        public void RenderDoodads(SKCanvas canvas)
        {
            foreach (var doodad in doodads)
            {
                var rect = TileRect(doodad.TileX, doodad.TileY);
                var skRect = new SKRect((float)rect.X, (float)rect.Y, (float)(rect.X + rect.Width), (float)(rect.Y + rect.Height));

                if (doodad.Sprite != null)
                {
                    using var skSprite = BitmapToSKBitmap(doodad.Sprite);
                    canvas.DrawBitmap(skSprite, skRect);
                }
                else
                {
                    var paint = new SKPaint { Color = ToSKColor(doodad.Color), Style = SKPaintStyle.Fill };
                    canvas.DrawRect(skRect, paint);
                }
            }
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }

        private static SKColor ToSKColor(Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }

        private sealed class MapDto
        {
            public int cols { get; set; }
            public int rows { get; set; }
            public int tileW { get; set; }
            public int tileH { get; set; }
            public JsonElement[][]? map { get; set; }
            public bool[][]? passability { get; set; }
            public List<MapLayerDto>? layers { get; set; }
            public int? activeLayerIndex { get; set; }
            public List<CharacterDto>? characters { get; set; }
            public List<DoodadDto>? doodads { get; set; }
            public List<TriggerDto>? triggers { get; set; }
            public string? externalTileMapAsset { get; set; }
            public TilesetDto? tileset { get; set; }
        }

        private sealed class MapLayerDto
        {
            public JsonElement[][]? tiles { get; set; }
            public string? id { get; set; }
            public string? name { get; set; }
            public bool? isVisible { get; set; }
            public double? opacity { get; set; }
        }

        private sealed class TilesetDto
        {
            public string? texture { get; set; }
            public int? columns { get; set; }
            public int? tileCount { get; set; }
            public int? margin { get; set; }
            public int? spacing { get; set; }
            public int? firstId { get; set; }
            public int? tileWidth { get; set; }
            public int? tileHeight { get; set; }
    }
    private sealed class CharacterDto
        {
            public int TileX { get; set; }
            public int TileY { get; set; }
            public string Name { get; set; } = "Hero";
            public CharacterClass Class { get; set; } = CharacterClass.Warrior;
            public string? BehaviorScript { get; set; }
            public string? TriggerEvent { get; set; }
            public string? Color { get; set; }
        }

        private sealed class DoodadDto
        {
            public int TileX { get; set; }
            public int TileY { get; set; }
            public string Type { get; set; } = "";
            public bool Collidable { get; set; } = false;
            public bool Interactable { get; set; } = false;
            public bool Animated { get; set; } = false;
            public string? Trigger { get; set; }
            public string? Color { get; set; }
            public string? OnInteract { get; set; }
        }

        private sealed class TriggerDto
        {
            public int TileX { get; set; }
            public int TileY { get; set; }
            public string? Name { get; set; }
        }
    }

    public class BehaviorTrigger
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString()
        {
            var label = string.IsNullOrWhiteSpace(Name) ? "Trigger" : Name.Trim();
            return $"{label} @ {TileX},{TileY}";
        }
    }

    public sealed class TilesetReference
    {
        public string TextureKey { get; }
        public string? AbsoluteTexturePath { get; }
        public int TileWidth { get; }
        public int TileHeight { get; }
        public int Columns { get; }
        public int TileCount { get; }
        public int Margin { get; }
        public int Spacing { get; }
        public int FirstId { get; }

        internal TilesetReference(string textureKey, string? absoluteTexturePath, int tileWidth, int tileHeight, int columns, int tileCount, int margin, int spacing, int firstId)
        {
            TextureKey = textureKey;
            AbsoluteTexturePath = absoluteTexturePath;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            Columns = columns;
            TileCount = tileCount;
            Margin = Math.Max(0, margin);
            Spacing = Math.Max(0, spacing);
            FirstId = firstId;
        }

        public bool TryGetSourceRegion(int tileId, int atlasPixelWidth, out TileSourceRegion region)
        {
            region = default;
            var index = tileId - FirstId;
            if (index < 0)
                return false;

            if (TileCount > 0 && index >= TileCount)
                return false;

            var effectiveColumns = Columns;
            if (effectiveColumns <= 0)
            {
                if (atlasPixelWidth <= 0)
                    return false;

                var usableWidth = atlasPixelWidth - Margin * 2;
                var denominator = TileWidth + Spacing;
                if (denominator <= 0)
                    return false;

                effectiveColumns = Math.Max(1, (usableWidth + Spacing) / denominator);
            }

            if (effectiveColumns <= 0)
                return false;

            var col = index % effectiveColumns;
            var row = index / effectiveColumns;
            var sourceX = Margin + col * (TileWidth + Spacing);
            var sourceY = Margin + row * (TileHeight + Spacing);

            region = new TileSourceRegion(sourceX, sourceY, TileWidth, TileHeight);
            return true;
        }
    }

    public readonly struct TileSourceRegion
    {
        public TileSourceRegion(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }
}


