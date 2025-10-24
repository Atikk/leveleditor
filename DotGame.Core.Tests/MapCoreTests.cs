using System;
using Xunit;
using DotGame.Core.Maps;

namespace DotGame.Core.Tests
{
    public class MapCoreTests
    {
        [Fact]
        public void InitializeCloneAndPassability_RoundTrips()
        {
            var cols = 4;
            var rows = 3;
            var tileW = 16;
            var tileH = 16;

            var tiles = new string?[rows, cols];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    tiles[y, x] = $"t_{x}_{y}";

            var pass = new bool[rows, cols];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    pass[y, x] = (x + y) % 2 == 0;

            var m = new Map();
            m.InitializeFromArray(cols, rows, tileW, tileH, tiles, pass);

            var jagged = m.GetPassabilityAsJagged();
            Assert.NotNull(jagged);
            Assert.Equal(rows, jagged!.Length);
            Assert.Equal(cols, jagged[0].Length);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    Assert.Equal(pass[y, x], jagged[y][x]);

            var clone = m.Clone();
            Assert.Equal(cols, clone.Cols);
            Assert.Equal(rows, clone.Rows);
            Assert.Equal(tileW, clone.TileW);
            Assert.Equal(tileH, clone.TileH);
            Assert.Equal("t_0_0", clone.GetTile(0,0));
        }
    }
}
