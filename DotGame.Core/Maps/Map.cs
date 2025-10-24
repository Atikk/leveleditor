using System;
using System.Collections.Generic;

namespace DotGame.Core.Maps
{
    // Minimal, UI-agnostic Map domain model for core logic and testing.
    public sealed class Map
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int TileW { get; private set; }
        public int TileH { get; private set; }

    private string?[,] tiles = new string?[0,0];
        private bool[,]? passability;

        public Map() { }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Cols && y < Rows;

        public void InitializeFromArray(int cols, int rows, int tileW, int tileH, string?[,] tileData, bool[,]? passabilityGrid = null)
        {
            if (tileData.GetLength(0) != rows || tileData.GetLength(1) != cols)
                throw new ArgumentException("Tile data dimensions do not match provided rows/cols.", nameof(tileData));

            Cols = cols;
            Rows = rows;
            TileW = tileW;
            TileH = tileH;

            tiles = new string?[rows, cols];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    tiles[y, x] = tileData[y, x];

            if (passabilityGrid != null)
            {
                if (passabilityGrid.GetLength(0) != rows || passabilityGrid.GetLength(1) != cols)
                    throw new ArgumentException("Passability grid dimensions do not match provided rows/cols.", nameof(passabilityGrid));

                passability = new bool[rows, cols];
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        passability[y, x] = passabilityGrid[y, x];
            }
            else
            {
                passability = null;
            }
        }

        public Map Clone()
        {
            var m = new Map();
            if (Rows == 0 || Cols == 0)
                return m;

            var copy = new string?[Rows, Cols];
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                    copy[y, x] = tiles[y, x];

            bool[,]? passCopy = null;
            if (passability != null)
            {
                passCopy = new bool[Rows, Cols];
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x < Cols; x++)
                        passCopy[y, x] = passability[y, x];
            }

            m.InitializeFromArray(Cols, Rows, TileW, TileH, copy, passCopy);
            return m;
        }

        public void SetPassability(bool[,] grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (grid.GetLength(0) != Rows || grid.GetLength(1) != Cols)
                throw new ArgumentException("Passability grid dimensions must match map rows and cols.", nameof(grid));

            passability = new bool[Rows, Cols];
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                    passability[y, x] = grid[y, x];
        }

        public bool[][]? GetPassabilityAsJagged()
        {
            if (passability == null) return null;
            var result = new bool[Rows][];
            for (int y = 0; y < Rows; y++)
            {
                result[y] = new bool[Cols];
                for (int x = 0; x < Cols; x++)
                    result[y][x] = passability[y, x];
            }
            return result;
        }

        public string? GetTile(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            return tiles[y, x];
        }
    }
}
