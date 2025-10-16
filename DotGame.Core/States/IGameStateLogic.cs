namespace DotGame.Core.States;

public interface IGameStateLogic
{
    void OnEnter();

    void OnExit();

    void Update(GameClock clock);
}
