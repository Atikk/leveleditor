using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DotGame.Runtime.GameData;

public sealed class QuestDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("stages")]
    public List<QuestStage> Stages { get; set; } = new();

    public void Normalize()
    {
        Stages ??= new List<QuestStage>();
        foreach (var stage in Stages)
        {
            stage.Normalize();
        }
    }
}

public sealed class QuestStage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("narrative")]
    public string Narrative { get; set; } = string.Empty;

    [JsonPropertyName("objectives")]
    public List<QuestObjective> Objectives { get; set; } = new();

    public void Normalize()
    {
        Objectives ??= new List<QuestObjective>();
        foreach (var objective in Objectives)
        {
            objective.Normalize();
        }
    }
}

public sealed class QuestObjective
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("isOptional")]
    public bool IsOptional { get; set; }

    [JsonPropertyName("completionTriggers")]
    public List<string> CompletionTriggers { get; set; } = new();

    public void Normalize()
    {
        CompletionTriggers ??= new List<string>();
    }
}
