using System;
using System.Text.Json;
using DotGame.Core.Services;
using Dotgame.Avalonia.Services.Adapters;
using Dotgame.Avalonia.Services;
using Xunit;

namespace DotGame.Tests
{
    [Collection("BitmapFactory collection")]
    public class TileServiceAdapterTests
    {
        [Fact]
        public void Adapter_CreateTileBuffer_Works()
        {
            var ui = new TileService(new FakeBitmapFactory());
            var adapter = new TileServiceAdapter(ui);
            var buf = adapter.CreateTileBuffer(4, 3);
            Assert.NotNull(buf);
            Assert.Equal(4, buf.GetLength(0));
            Assert.Equal(3, buf.GetLength(1));
        }

        [Fact]
        public void Adapter_CreateTileEntryFromNumber_WithNullTileset_ReturnsNull()
        {
            var ui = new TileService(new FakeBitmapFactory());
            var adapter = new TileServiceAdapter(ui);
            using var doc = JsonDocument.Parse("[5]");
            var elem = doc.RootElement[0];
            var entry = adapter.CreateTileEntryFromNumber(elem, null);
            Assert.Null(entry);
        }

        [Fact]
        public void Adapter_EditorHasTilesPlaced_DetectsPlaced()
        {
            var ui = new TileService(new FakeBitmapFactory());
            var adapter = new TileServiceAdapter(ui);

            // create a single layer object that mirrors the UI LayerState shape
            var layer = new Dotgame.Avalonia.Views.LayerState("id", "name", ui.CreateTileBuffer(1,1));
            var layers = new System.Collections.Generic.List<object> { layer };
            // Initially empty
            Assert.False(adapter.EditorHasTilesPlaced(layers));

            // place a tile in the UI buffer by creating a TileEntry instance without invoking its constructor
            // This avoids needing Avalonia platform services in unit tests.
            var tileEntryType = typeof(Dotgame.Avalonia.Views.TileEntry);
            var raw = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tileEntryType);
            // set the SerializedValueOverride backing field so the TileEntry looks valid to consumers
            var field = tileEntryType.GetField("<SerializedValueOverride>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(raw, "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVQImWNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=");
            var te = (Dotgame.Avalonia.Views.TileEntry)raw;
            layer.Tiles[0,0] = te;

            // Sanity-check: UI service should detect the placed tile.
            Assert.NotNull(layer.Tiles[0,0]);
            var uiResult = ui.EditorHasTilesPlaced(new System.Collections.Generic.List<Dotgame.Avalonia.Views.LayerState> { layer });
            Assert.True(uiResult, "UI did not detect the placed tile as expected.");

            // Sanity: list contains the same LayerState instance we created.
            Assert.True(layers[0] is Dotgame.Avalonia.Views.LayerState, "layers[0] is not LayerState");
            Assert.Same(layer, layers[0]);

            // Adapter should mirror that behavior.
            var adapterResult = adapter.EditorHasTilesPlaced(layers);
            Assert.True(adapterResult, $"Adapter returned false while UI returned {uiResult}.");
        }
    }
}
