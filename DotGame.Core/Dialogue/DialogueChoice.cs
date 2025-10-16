namespace DotGame.Core.Dialogue;

public sealed class DialogueChoice
{
    public required string Id { get; init; }

    public required string Text { get; init; }

    public string? NextNodeId { get; init; }

    public IReadOnlyList<string> RequiredFlags { get; init; } = Array.Empty<string>();
}
