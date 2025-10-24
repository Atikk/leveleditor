using System;
using System.Collections.Generic;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("UI-local MapDocument has been superseded by DotGame.Core.Maps.MapDocument. This legacy type remains for compatibility.")]
public class MapDocument
{
    // Basic map metadata
    public int Cols { get; set; }

    public int Rows { get; set; }

    public int TileW { get; set; }

    public int TileH { get; set; }

    // Primary tile matrix: rows x cols of optional data-URLs or asset keys
    public string?[][]? Map { get; set; }

    // Optional passability grid: true = passable, false = blocked
    public bool[][]? Passability { get; set; }

    public MapDocument()
    {
    }

    public static MapDocument CreateEmpty(int cols, int rows, int tileW = 32, int tileH = 32)
    {
        if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        var doc = new MapDocument
        {
            Cols = cols,
            Rows = rows,
            TileW = tileW,
            TileH = tileH,
            Map = new string?[rows][],
            Passability = new bool[rows][]
        };

        for (int y = 0; y < rows; y++)
        {
            doc.Map[y] = new string?[cols];
            doc.Passability[y] = new bool[cols];
            for (int x = 0; x < cols; x++)
                doc.Passability[y][x] = true; // default passable
        }

        return doc;
    }

    public void ValidateDimensions()
    {
        if (Map != null)
        {
            if (Map.Length != Rows) throw new InvalidOperationException("Map row count does not match Rows property.");
            for (int r = 0; r < Map.Length; r++)
            {
                if (Map[r] != null && Map[r].Length != Cols)
                    throw new InvalidOperationException($"Map row {r} length does not match Cols property.");
            }
        }

        if (Passability != null)
        {
            if (Passability.Length != Rows) throw new InvalidOperationException("Passability row count does not match Rows property.");
            for (int r = 0; r < Passability.Length; r++)
            {
                if (Passability[r] != null && Passability[r].Length != Cols)
                    throw new InvalidOperationException($"Passability row {r} length does not match Cols property.");
            }
        }
    }

    // Backwards compatibility helpers could be added here (e.g., converting to/from legacy Map DTOs)
}