namespace DotGame.Core.Quests;

public sealed class QuestDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<QuestStage> Stages { get; init; } = Array.Empty<QuestStage>();

    public QuestStage? FindStage(string stageId)
        => Stages.FirstOrDefault(stage => string.Equals(stage.Id, stageId, StringComparison.Ordinal));
}
