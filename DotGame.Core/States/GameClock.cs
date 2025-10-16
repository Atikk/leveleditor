namespace DotGame.Core.States;

public readonly record struct GameClock(System.TimeSpan Delta, System.TimeSpan Total)
{
    public static GameClock From(System.TimeSpan delta, System.TimeSpan total)
        => new(delta, total);
}
