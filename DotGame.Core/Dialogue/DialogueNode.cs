namespace DotGame.Core.Dialogue;

public sealed class DialogueNode
{
    public required string Id { get; init; }

    public required string Speaker { get; init; }

    public required string Text { get; init; }

    public IReadOnlyList<DialogueChoice> Choices { get; init; } = Array.Empty<DialogueChoice>();

    public string? NextNodeId { get; init; }
}
