using System;
using System.IO;
using System.Text.Json;
using SkiaSharp;

// Create a simple 10x10 map with solid color tiles
var map = new {
    cols = 10,
    rows = 10,
    tileW = 32,
    tileH = 32,
    map = new string[10][]
};

for (int y = 0; y < 10; y++)
{
    map.map[y] = new string[10];
    for (int x = 0; x < 10; x++)
    {
        // Create a simple colored tile (green grass)
        var bitmap = new SKBitmap(32, 32);
        var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(34, 139, 34)); // Forest green
        
        // Encode to PNG
        var image = SKImage.FromBitmap(bitmap);
        var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var base64 = Convert.ToBase64String(data.ToArray());
        map.map[y][x] = $"data:image/png;base64,{base64}";
    }
}

var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText("/home/runner/workspace/maps/test.json", json);
Console.WriteLine("Created test map!");
