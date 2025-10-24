using System.Text.Json;
using Xunit;

namespace DotGame.Core.Tests;

public class MapDocumentTests
{
    [Fact]
    public void MapDocument_SerializeDeserialize_RoundTripsPassability()
    {
        var doc = DotGame.Core.MapDocument.CreateEmpty(4, 3, 16, 16);
        // Mark some tiles as blocked
        doc.Passability![0][0] = false;
        doc.Passability[1][2] = false;
        doc.Map![2][3] = "data:image/png;base64,AAA";

        var options = new JsonSerializerOptions { WriteIndented = false };
        var json = JsonSerializer.Serialize(doc, options);

        var deserialized = JsonSerializer.Deserialize<DotGame.Core.MapDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(deserialized);
        Assert.Equal(doc.Cols, deserialized!.Cols);
        Assert.Equal(doc.Rows, deserialized.Rows);
        Assert.NotNull(deserialized.Passability);
        Assert.False(deserialized.Passability![0][0]);
        Assert.False(deserialized.Passability[1][2]);
        Assert.Equal("data:image/png;base64,AAA", deserialized.Map![2][3]);
    }
}
