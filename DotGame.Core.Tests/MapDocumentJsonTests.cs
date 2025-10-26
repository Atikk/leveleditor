using System;
using System.Text.Json;
using DotGame.Core;
using Xunit;

namespace DotGame.Core.Tests
{
    public class MapDocumentJsonTests
    {
        [Fact]
        public void Roundtrip_SerializeDeserialize_PreservesData()
        {
            var doc = MapDocument.CreateEmpty(3, 2, 16, 16);
            doc.Map[0][0] = "tileA";
            doc.Map[0][1] = null;
            doc.Map[0][2] = "tileC";
            doc.Map[1][0] = "x";
            doc.Map[1][1] = "y";
            doc.Map[1][2] = "z";

            // set some passability values
            doc.Passability![0][0] = true;
            doc.Passability[0][1] = false;
            doc.Passability[0][2] = true;
            doc.Passability[1][0] = false;
            doc.Passability[1][1] = false;
            doc.Passability[1][2] = true;

            // set camera metadata and verify it roundtrips
            doc.Camera = new MapCamera
            {
                Position = new double[] { 1.0, 2.0, 3.0 },
                RotationQuaternion = new double[] { 0.0, 0.0, 0.0, 1.0 }
            };

            var json = JsonSerializer.Serialize(doc);
            var other = JsonSerializer.Deserialize<MapDocument>(json);

            Assert.NotNull(other);
            Assert.Equal(doc.Cols, other!.Cols);
            Assert.Equal(doc.Rows, other.Rows);
            Assert.Equal(doc.TileW, other.TileW);
            Assert.Equal(doc.TileH, other.TileH);

            Assert.NotNull(other.Map);
            Assert.Equal(doc.Map!.Length, other.Map!.Length);
            for (int r = 0; r < doc.Map.Length; r++)
            {
                Assert.Equal(doc.Map[r]!.Length, other.Map[r]!.Length);
                for (int c = 0; c < doc.Map[r]!.Length; c++)
                    Assert.Equal(doc.Map[r]![c], other.Map[r]![c]);
            }

            Assert.NotNull(other.Passability);
            Assert.Equal(doc.Passability!.Length, other.Passability!.Length);
            for (int r = 0; r < doc.Passability.Length; r++)
            {
                Assert.Equal(doc.Passability[r]!.Length, other.Passability[r]!.Length);
                for (int c = 0; c < doc.Passability[r]!.Length; c++)
                    Assert.Equal(doc.Passability[r]![c], other.Passability[r]![c]);
            }

            // camera metadata should roundtrip
            Assert.NotNull(other.Camera);
            Assert.Equal(doc.Camera!.Position!.Length, other.Camera!.Position!.Length);
            for (int i = 0; i < doc.Camera.Position.Length; i++)
                Assert.Equal(doc.Camera.Position[i], other.Camera.Position[i]);
            Assert.Equal(doc.Camera.RotationQuaternion!.Length, other.Camera.RotationQuaternion!.Length);
            for (int i = 0; i < doc.Camera.RotationQuaternion.Length; i++)
                Assert.Equal(doc.Camera.RotationQuaternion[i], other.Camera.RotationQuaternion[i]);

        }

        [Fact]
        public void Deserialize_MalformedJson_ThrowsJsonException()
        {
            var bad = "{ this is not valid json...";
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MapDocument>(bad));
        }

        [Fact]
        public void ValidateDimensions_ThrowsOnMismatchedArrayLengths()
        {
            // Create a JSON payload where Rows/Cols disagree with the Map/Passability lengths
            var raw = "{ \"Cols\": 3, \"Rows\": 2, \"TileW\": 16, \"TileH\": 16, \"Map\": [[\"a\", \"b\"], [\"c\", \"d\"]], \"Passability\": [[true, false, true], [true, true, false]] }";
            var doc = JsonSerializer.Deserialize<MapDocument>(raw);
            Assert.NotNull(doc);
            // Map has rows with length 2 but Cols=3, so ValidateDimensions should throw
            Assert.Throws<InvalidOperationException>(() => doc!.ValidateDimensions());
        }
    }
}
