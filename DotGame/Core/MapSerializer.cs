using System.Text.Json;
using System.IO;
using System;

namespace DotGame.Core;

public static class MapSerializer
{
    public static void Save(MapDocument map, string path)
    {
        // Serialize the map document to JSON
        var json = JsonSerializer.Serialize(map);
        File.WriteAllText(path, json);
    }

    public static MapDocument Load(string path)
    {
        // Deserialize the JSON file into a MapDocument
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MapDocument>(json) ?? throw new InvalidOperationException("Failed to deserialize map.");
    }
}