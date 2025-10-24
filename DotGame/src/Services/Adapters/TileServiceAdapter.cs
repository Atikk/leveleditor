using System;
using System.Collections.Generic;
using System.Text.Json;
using DotGame.Core.Services;

namespace Dotgame.Avalonia.Services.Adapters
{
    // Adapter visible to the UI project that implements the core contract by delegating to the UI TileService.
    public sealed class TileServiceAdapter : DotGame.Core.Services.ITileService
    {
        private readonly Dotgame.Avalonia.Services.ITileService uiService;

        public TileServiceAdapter(Dotgame.Avalonia.Services.ITileService uiService)
        {
            this.uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        }

        public CoreTileEntry?[,] CreateTileBuffer(int width, int height)
        {
            var uiBuf = uiService.CreateTileBuffer(width, height);
            return ConvertBuffer(uiBuf);
        }

        public CoreTileEntry?[,] CreateTileBufferFromSerialized(JsonElement[][]? source, string baseDirectory, int fallbackCols, int fallbackRows, object? tilesetState)
        {
            var uiBuf = uiService.CreateTileBufferFromSerialized(source, baseDirectory, fallbackCols, fallbackRows, tilesetState as Dotgame.Avalonia.Views.TilesetState);
            return ConvertBuffer(uiBuf);
        }

        public CoreTileEntry? CreateTileEntryFromNumber(JsonElement element, object? tilesetState)
        {
            var ui = uiService.CreateTileEntryFromNumber(element, tilesetState as Dotgame.Avalonia.Views.TilesetState);
            return ConvertEntry(ui);
        }

        public bool InBounds(CoreTileEntry?[,] buffer, int x, int y)
        {
            if (buffer == null) return false;
            return x >= 0 && y >= 0 && x < buffer.GetLength(0) && y < buffer.GetLength(1);
        }

        public CoreTileEntry? GetTopmostTile(IList<object> layers, int x, int y, out object? owningLayer)
        {
            owningLayer = null;
            try
            {
                var uiLayers = new List<Dotgame.Avalonia.Views.LayerState>();
                foreach (var l in layers)
                {
                    if (l is Dotgame.Avalonia.Views.LayerState ls)
                        uiLayers.Add(ls);
                }

                Dotgame.Avalonia.Views.LayerState? uiOwning = null;
                var entry = uiService.GetTopmostTile(uiLayers, x, y, out uiOwning);
                owningLayer = uiOwning;
                return ConvertEntry(entry);
            }
            catch
            {
                return null;
            }
        }

        public CoreTileEntry? LoadTileEntry(string storedValue, string baseDirectory, object? tilesetState, Action<string>? onWarn = null)
        {
            var ui = uiService.LoadTileEntry(storedValue, baseDirectory, tilesetState as Dotgame.Avalonia.Views.TilesetState, onWarn);
            return ConvertEntry(ui);
        }

        public bool EditorHasTilesPlaced(IEnumerable<object> layers)
        {
            var uiLayers = new List<Dotgame.Avalonia.Views.LayerState>();
            foreach (var l in layers)
            {
                if (l is Dotgame.Avalonia.Views.LayerState ls)
                    uiLayers.Add(ls);
            }
            return uiService.EditorHasTilesPlaced(uiLayers);
        }

        private static CoreTileEntry?[,] ConvertBuffer(Dotgame.Avalonia.Views.TileEntry?[,] uiBuf)
        {
            if (uiBuf == null) return new CoreTileEntry?[0,0];
            int w = uiBuf.GetLength(0);
            int h = uiBuf.GetLength(1);
            var outBuf = new CoreTileEntry?[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                outBuf[x, y] = ConvertEntry(uiBuf[x, y]);
            return outBuf;
        }

        private static CoreTileEntry? ConvertEntry(Dotgame.Avalonia.Views.TileEntry? ui)
        {
            if (ui is null) return null;
            return new CoreTileEntry(ui.GetSerializedValue(), ui.TileId, ui.TilesetKey);
        }
    }
}
