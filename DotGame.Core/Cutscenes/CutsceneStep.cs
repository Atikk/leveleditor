namespace DotGame.Core.Cutscenes;

public sealed class CutsceneStep
{
    public required string Type { get; init; }

    public TimeSpan Duration { get; init; }

    public IDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}
