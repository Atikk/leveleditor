namespace DotGame.Core.Quests;

public sealed class QuestStage
{
    public required string Id { get; init; }

    public string? Narrative { get; init; }

    public IReadOnlyList<QuestObjective> Objectives { get; init; } = Array.Empty<QuestObjective>();
}
