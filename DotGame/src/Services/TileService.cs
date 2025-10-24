using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dotgame.Avalonia.Views;
using Dotgame.Avalonia.Models;

namespace Dotgame.Avalonia.Services
{
    public interface ITileService
    {
        TileEntry?[,] CreateTileBuffer(int width, int height);
        TileEntry?[,] CreateTileBufferFromSerialized(JsonElement[][]? source, string baseDirectory, int fallbackCols, int fallbackRows, TilesetState? tilesetState);
        TileEntry? CreateTileEntryFromNumber(JsonElement element, TilesetState? tilesetState);
        bool InBounds(TileEntry?[,] buffer, int x, int y);
        TileEntry? GetTopmostTile(System.Collections.Generic.IList<LayerState> layers, int x, int y, out LayerState? owningLayer);
        TileEntry? LoadTileEntry(string storedValue, string baseDirectory, TilesetState? tilesetState, Action<string>? onWarn = null);
        bool EditorHasTilesPlaced(System.Collections.Generic.IEnumerable<LayerState> layers);
    }

    public class TileService : ITileService
    {
        private readonly IBitmapFactory bitmapFactory;

        public TileService(IBitmapFactory? bitmapFactory = null)
        {
            this.bitmapFactory = bitmapFactory ?? new AvaloniaBitmapFactory();
        }

        public TileEntry?[,] CreateTileBuffer(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return new TileEntry?[width, height];
        }

        public TileEntry?[,] CreateTileBufferFromSerialized(JsonElement[][]? source, string baseDirectory, int fallbackCols, int fallbackRows, TilesetState? tilesetState)
        {
            int height = Math.Max(1, source?.Length ?? fallbackRows);
            int width = fallbackCols;

            if (source != null && source.Length > 0)
            {
                width = source.Max(row => row?.Length ?? 0);
            }

            width = Math.Max(1, width);

            var buffer = new TileEntry?[width, height];

            if (source != null)
            {
                for (int y = 0; y < height; y++)
                {
                    var row = y < source.Length ? source[y] : null;
                    for (int x = 0; x < width; x++)
                    {
                        if (row == null || x >= row.Length)
                            continue;

                        var element = row[x];
                        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                            continue;

                        TileEntry? entry = null;
                        switch (element.ValueKind)
                        {
                            case JsonValueKind.String:
                                var stored = element.GetString();
                                entry = string.IsNullOrWhiteSpace(stored)
                                    ? null
                                    : LoadTileEntry(stored!, baseDirectory, tilesetState);
                                break;
                            case JsonValueKind.Number:
                                entry = CreateTileEntryFromNumber(element, tilesetState);
                                break;
                            default:
                                // ignore unsupported kinds
                                break;
                        }

                        if (entry != null)
                            buffer[x, y] = entry;
                    }
                }
            }

            return buffer;
        }

        public TileEntry? CreateTileEntryFromNumber(JsonElement element, TilesetState? tilesetState)
        {
            if (tilesetState == null)
                return null;

            int tileId;
            if (element.TryGetInt32(out var intValue))
            {
                tileId = intValue;
            }
            else
            {
                var dbl = element.GetDouble();
                tileId = (int)Math.Round(dbl);
            }

            try
            {
                return tilesetState.CreateTileEntry(tileId);
            }
            catch
            {
                return null;
            }
        }

        public bool InBounds(TileEntry?[,] buffer, int x, int y)
        {
            if (buffer == null)
                return false;

            return x >= 0 && y >= 0 && x < buffer.GetLength(0) && y < buffer.GetLength(1);
        }

        public TileEntry? GetTopmostTile(System.Collections.Generic.IList<LayerState> layers, int x, int y, out LayerState? owningLayer)
        {
            owningLayer = null;

            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (!layer.IsVisible)
                    continue;

                var tiles = layer.Tiles;
                if (x < 0 || y < 0 || x >= tiles.GetLength(0) || y >= tiles.GetLength(1))
                    continue;

                var entry = tiles[x, y];
                if (entry != null)
                {
                    owningLayer = layer;
                    return entry;
                }
            }

            return null;
        }

        public TileEntry? LoadTileEntry(string storedValue, string baseDirectory, TilesetState? tilesetState, Action<string>? onWarn = null)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
                return null;

            if (tilesetState != null && int.TryParse(storedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                try
                {
                    return tilesetState.CreateTileEntry(parsedId);
                }
                catch (Exception ex)
                {
                    onWarn?.Invoke($"Failed to resolve tile id {parsedId} via tileset: {ex.Message}");
                }
            }

            if (storedValue.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // decode base64 and create a Bitmap via the factory
                int comma = storedValue.IndexOf(',');
                string base64 = comma >= 0 ? storedValue[(comma + 1)..] : storedValue;
                try
                {
                    byte[] bytes = Convert.FromBase64String(base64);
                    using var ms = new MemoryStream(bytes);
                    var bmp = bitmapFactory.LoadFromStream(ms);
                    return new TileEntry(bmp, null, storedValue, storedValue);
                }
                catch (Exception ex)
                {
                    onWarn?.Invoke($"Failed to parse data URL tile: {ex.Message}");
                    return null;
                }
            }

            var resolved = Path.IsPathRooted(storedValue)
                ? storedValue
                : Path.Combine(baseDirectory, storedValue);

            if (!File.Exists(resolved))
            {
                onWarn?.Invoke($"Tile asset not found at {resolved}.");
                return null;
            }

            try
            {
                var bmp = bitmapFactory.LoadFromFile(resolved);
                return new TileEntry(bmp, resolved, null, storedValue);
            }
            catch (Exception ex)
            {
                onWarn?.Invoke($"Failed to load tile asset '{resolved}': {ex.Message}");
                return null;
            }
        }

        // Helper to decode data: URLs to raw bytes without constructing Bitmaps.
        public byte[]? DecodeDataUrlToBytes(string dataUrl, Action<string>? onWarn = null)
        {
            if (string.IsNullOrWhiteSpace(dataUrl))
                return null;

            if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                onWarn?.Invoke("Not a data URL.");
                return null;
            }

            // Take the payload after the last comma (handles cases where the mediatype contains commas)
            int comma = dataUrl.LastIndexOf(',');
            string payload = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
            payload = payload.Trim();

            if (payload.Length == 0)
            {
                onWarn?.Invoke("Empty data URL payload.");
                return null;
            }

            try
            {
                return Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                // Try a cleaned-up payload (remove whitespace/newlines which sometimes appear in long data URLs)
                try
                {
                    var cleaned = Regex.Replace(payload, "\\s+", "");
                    return Convert.FromBase64String(cleaned);
                }
                catch (Exception ex)
                {
                    onWarn?.Invoke($"Failed to decode data URL: {ex.Message}");
                    return null;
                }
            }
        }

        public bool EditorHasTilesPlaced(System.Collections.Generic.IEnumerable<LayerState> layers)
        {
            foreach (var layer in layers)
            {
                var buffer = layer.Tiles;
                int width = buffer.GetLength(0);
                int height = buffer.GetLength(1);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (buffer[x, y] != null)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
