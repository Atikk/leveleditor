namespace DotGame.Core.Cutscenes;

public sealed class CutsceneScript
{
    public required string Id { get; init; }

    public IReadOnlyList<CutsceneStep> Steps { get; init; } = Array.Empty<CutsceneStep>();
}
