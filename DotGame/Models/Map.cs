using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using DotGameAvalonia.Models;

namespace DotGameAvalonia.Models
{
    public sealed class Map
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int TileW { get; private set; }
        public int TileH { get; private set; }

        private string?[,] tiles = default!;
        private readonly Dictionary<string, SKBitmap> imageCache = new(StringComparer.Ordinal);
        public WriteableBitmap? Composite { get; private set; }

        private readonly List<Character> characters = new();
        private readonly List<Doodad> doodads = new();
        private readonly List<BehaviorTrigger> triggers = new();

    public string? ExternalTileMapAsset { get; private set; }

        public Map() {}

        public static Map LoadFromJson(string path)
        {
            var json = File.ReadAllText(path);
            var obj = JsonSerializer.Deserialize<MapDto>(json, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("Invalid map JSON.");

            if (obj.map is null || obj.cols <= 0 || obj.rows <= 0 || obj.tileW <= 0 || obj.tileH <= 0)
                throw new InvalidDataException("Map is missing required fields (map/cols/rows/tileW/tileH).");

            var map = new Map
            {
                Cols = obj.cols,
                Rows = obj.rows,
                TileW = obj.tileW,
                TileH = obj.tileH,
                tiles = new string?[obj.rows, obj.cols]
            };

            for (int y = 0; y < obj.rows; y++)
            {
                var row = obj.map[y];
                for (int x = 0; x < obj.cols; x++)
                    map.tiles[y, x] = row?[x];
            }

            if (obj.characters != null)
            {
                foreach (var charDto in obj.characters)
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
                foreach (var doodadDto in obj.doodads)
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
                foreach (var triggerDto in obj.triggers)
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
            return tiles[ty, tx];
        }

        public void InitializeFromArray(int cols, int rows, int tileW, int tileH, string?[,] tileData,
            IEnumerable<Character>? characterData = null,
            IEnumerable<Doodad>? doodadData = null,
            IEnumerable<BehaviorTrigger>? triggerData = null,
            string? externalTileMapAsset = null)
        {
            if (tileData.GetLength(0) != rows || tileData.GetLength(1) != cols)
                throw new ArgumentException("Tile data dimensions do not match provided rows/cols.", nameof(tileData));

            Cols = cols;
            Rows = rows;
            TileW = tileW;
            TileH = tileH;
            tiles = new string?[rows, cols];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                    tiles[y, x] = tileData[y, x];
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
                    copy[y, x] = tiles?[y, x];
            }

            clone.InitializeFromArray(Cols, Rows, TileW, TileH, copy, characters, doodads, triggers, ExternalTileMapAsset);
            return clone;
        }

        public void BuildComposite()
        {
            var surface = SKSurface.Create(new SKImageInfo(Cols * TileW, Rows * TileH));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    var url = tiles[y, x];
                    if (!string.IsNullOrEmpty(url))
                    {
                        var img = GetOrDecode(url!);
                        var destRect = SKRect.Create(x * TileW, y * TileH, TileW, TileH);
                        canvas.DrawBitmap(img, destRect);
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

        public IReadOnlyList<Character> Characters => characters;
        public IReadOnlyList<Doodad> Doodads => doodads;
        public IReadOnlyList<BehaviorTrigger> Triggers => triggers;

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
            public string?[][]? map { get; set; }
            public List<CharacterDto>? characters { get; set; }
            public List<DoodadDto>? doodads { get; set; }
            public List<TriggerDto>? triggers { get; set; }
            public string? externalTileMapAsset { get; set; }
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
    }
}
