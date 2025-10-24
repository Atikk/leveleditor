using System.Text.Json;
using Dotgame.Avalonia.Services;
using Xunit;

namespace DotGame.Tests;

[Xunit.Collection("BitmapFactory collection")]
public class TileServiceTests
{
    [Fact]
    public void CreateTileBuffer_ReturnsNonNull_WithDimensions()
    {
        var svc = new TileService();
        var buf = svc.CreateTileBuffer(10, 5);
        Assert.NotNull(buf);
        Assert.Equal(10, buf.GetLength(0));
        Assert.Equal(5, buf.GetLength(1));
    }

    [Fact]
    public void LoadTileEntry_DataUrl_ParsesBase64()
    {
        var svc = new TileService();
        // 1x1 PNG base64 (red)
        var dataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVQImWNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=";
        var bytes = svc.DecodeDataUrlToBytes(dataUrl);
        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 0);
    }

    [Fact]
    public void CreateTileBufferFromSerialized_NumericId_WithNullTileset_ReturnsNullEntries()
    {
    var svc = new TileService();
        JsonElement[][] matrix = new JsonElement[1][];
        matrix[0] = new JsonElement[1];
        using var doc = JsonDocument.Parse("[ [ 5 ] ]");
        // Extract the inner JsonElement matrix
        var root = doc.RootElement;
        var row = root[0];
        matrix[0][0] = row[0];

        var buf = svc.CreateTileBufferFromSerialized(matrix, System.Environment.CurrentDirectory, 1, 1, null);
        Assert.NotNull(buf);
        Assert.Null(buf[0,0]);
    }
}
