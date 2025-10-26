using System;
using DotGame.Core.Maps;
using Xunit;

namespace DotGame.Core.Tests
{
    public class MapPassabilityTests
    {
        [Fact]
        public void Initialize_WithPassability_WorksAndReturnsJagged()
        {
            var cols = 3;
            var rows = 2;
            var tileW = 32;
            var tileH = 32;

            var tiles = new string?[rows, cols];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    tiles[y, x] = null;

            var pass = new bool[rows, cols];
            pass[0, 0] = true;
            pass[1, 2] = false;

            var m = new Map();
            m.InitializeFromArray(cols, rows, tileW, tileH, tiles, pass);

            var jagged = m.GetPassabilityAsJagged();
            Assert.NotNull(jagged);
            Assert.Equal(rows, jagged!.Length);
            Assert.Equal(cols, jagged[0].Length);
            Assert.True(jagged[0][0]);
            Assert.False(jagged[1][2]);
        }

        [Fact]
        public void SetPassability_DimensionMismatch_Throws()
        {
            var m = new Map();
            var tiles = new string?[2,2];
            m.InitializeFromArray(2, 2, 32, 32, tiles);

            var bad = new bool[3,2]; // rows mismatch
            Assert.Throws<ArgumentException>(() => m.SetPassability(bad));
        }

        [Fact]
        public void InitializeFromArray_TileDataDimensionMismatch_Throws()
        {
            var m = new Map();
            var tiles = new string?[2,3];
            // Provide rows=2 cols=2 but tile array has cols=3
            Assert.Throws<ArgumentException>(() => m.InitializeFromArray(2, 2, 32, 32, tiles));
        }

        [Fact]
        public void GetPassabilityAsJagged_ReturnsNullWhenNotSet()
        {
            var m = new Map();
            var tiles = new string?[1,1];
            m.InitializeFromArray(1, 1, 16, 16, tiles);
            var j = m.GetPassabilityAsJagged();
            Assert.Null(j);
        }
    }
}
