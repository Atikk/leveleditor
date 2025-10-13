<Query Kind="Program" />

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace DotGameCSharp
{
    public sealed class Map
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int TileW { get; private set; }
        public int TileH { get; private set; }

        // 2D array of Base64 data-URLs or null
        private string?[,] tiles = default!;
        private readonly Dictionary<string, Bitmap> imageCache = new(StringComparer.Ordinal);
        public Bitmap? Composite { get; private set; }

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

            // Copy jagged JSON array into 2D
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

        public Rectangle TileRect(int tx, int ty) => new(tx * TileW, ty * TileH, TileW, TileH);

        public void BuildComposite()
        {
            Composite?.Dispose();
            var bmp = new Bitmap(Cols * TileW, Rows * TileH);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Cols; x++)
                {
                    var url = tiles[y, x];
                    if (!string.IsNullOrEmpty(url))
                    {
                        var img = GetOrDecode(url!);
                        g.DrawImage(img, x * TileW, y * TileH, TileW, TileH);
                    }
                    // else: leave white, or draw grid if you like
                }
            }

            Composite = bmp;
        }

        private Bitmap GetOrDecode(string dataUrl)
        {
            if (imageCache.TryGetValue(dataUrl, out var cached))
                return cached;

            // data:image/png;base64,AAAA...
            var comma = dataUrl.IndexOf(',');
            var base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
            var bytes = Convert.FromBase64String(base64);

            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            imageCache[dataUrl] = bmp;
            return bmp;
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
