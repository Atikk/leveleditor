using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DotGame.Runtime.GameData;

public sealed class DialogueGraph
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nodes")]
    public List<DialogueNode> Nodes { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, DialogueNode> NodeLookup { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        Nodes ??= new List<DialogueNode>();
        NodeLookup = new Dictionary<string, DialogueNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in Nodes)
        {
            if (node is null)
                continue;

            if (string.IsNullOrWhiteSpace(node.Id))
                continue;

            node.Normalize();
            NodeLookup[node.Id] = node;
        }
    }

    public bool TryGetNode(string nodeId, [NotNullWhen(true)] out DialogueNode? node)
    {
        return NodeLookup.TryGetValue(nodeId, out node);
    }
}

public sealed class DialogueNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("speaker")]
    public string Speaker { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("nextNodeId")]
    public string? NextNodeId { get; set; }

    [JsonPropertyName("choices")]
    public List<DialogueChoice> Choices { get; set; } = new();

    public void Normalize()
    {
        Choices ??= new List<DialogueChoice>();
        foreach (var choice in Choices)
        {
            choice.Normalize();
        }
    }
}

public sealed class DialogueChoice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("nextNodeId")]
    public string? NextNodeId { get; set; }

    [JsonPropertyName("requiredFlags")]
    public List<string> RequiredFlags { get; set; } = new();

    public void Normalize()
    {
        RequiredFlags ??= new List<string>();
    }
}
