namespace DotGame.Core.Dialogue;

public sealed class DialogueGraph
{
    public required string Id { get; init; }

    public IReadOnlyList<DialogueNode> Nodes { get; init; } = Array.Empty<DialogueNode>();

    public DialogueNode? FindNode(string nodeId)
        => Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
}
