using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace DotGame.Runtime.GameData;

public sealed class CutsceneScript
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<CutsceneStep> Steps { get; set; } = new();

    public void Normalize()
    {
        Steps ??= new List<CutsceneStep>();
        foreach (var step in Steps)
        {
            step.Normalize();
        }
    }
}

public sealed class CutsceneStep
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public string DurationText { get; set; } = "00:00:00";

    [JsonIgnore]
    public TimeSpan Duration { get; private set; } = TimeSpan.Zero;

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();

    public void Normalize()
    {
        Parameters ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!TimeSpan.TryParse(DurationText, CultureInfo.InvariantCulture, out var duration))
        {
            duration = TimeSpan.Zero;
        }

        Duration = duration;
    }
}
