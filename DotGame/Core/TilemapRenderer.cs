using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using System;

namespace DotGame.Core;

public class TilemapRenderer : Control
{
    // Properties for tilemap rendering
    public int TileWidth { get; set; } = 32;
    public int TileHeight { get; set; } = 32;
    public int[,]? TileData { get; set; } // 2D array representing the tilemap

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (TileData == null)
            return;

        // Calculate visible tiles based on the viewport
        var viewport = Bounds;
        int startX = (int)(viewport.X / TileWidth);
        int startY = (int)(viewport.Y / TileHeight);
        int endX = (int)((viewport.X + viewport.Width) / TileWidth);
        int endY = (int)((viewport.Y + viewport.Height) / TileHeight);

        // Clip to the tilemap bounds
        startX = Math.Max(0, startX);
        startY = Math.Max(0, startY);
        endX = Math.Min(TileData.GetLength(1) - 1, endX);
        endY = Math.Min(TileData.GetLength(0) - 1, endY);

        // Render visible tiles
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                int tileId = TileData[y, x];

                // Example: Draw a rectangle for each tile
                var tileRect = new Rect(x * TileWidth, y * TileHeight, TileWidth, TileHeight);
                context.FillRectangle(Brushes.Gray, tileRect);
            }
        }
    }
}
