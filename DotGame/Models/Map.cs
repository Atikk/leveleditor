using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

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

        private Map() {}

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

            map.BuildComposite();
            return map;
        }

        public bool InBounds(int tx, int ty) => tx >= 0 && ty >= 0 && tx < Cols && ty < Rows;

        public Rect TileRect(int tx, int ty) => new(tx * TileW, ty * TileH, TileW, TileH);

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

        public void AddCharacter(Character character)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            if (!InBounds(character.TileX, character.TileY))
                throw new InvalidOperationException("Character position is out of map bounds.");

            characters.Add(character);
            Console.WriteLine($"Character '{character.Name}' added to map at position ({character.TileX}, {character.TileY}).");
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
        }
    }
}
