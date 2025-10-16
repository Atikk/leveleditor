namespace DotGame.Core.Quests;

public sealed class QuestObjective
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public bool IsOptional { get; init; }

    public IReadOnlyList<string> CompletionTriggers { get; init; } = Array.Empty<string>();
}
