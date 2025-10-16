using DotGame.Runtime.Rendering;

namespace DotGame.Runtime.States;

public abstract class GameStateBase : IGameState
{
    public virtual bool AllowUpdateBelow => false;

    public virtual bool AllowDrawBelow => false;

    public virtual void OnEnter()
    {
    }

    public virtual void OnExit()
    {
    }

    public virtual void Update(in RuntimeUpdateContext context)
    {
    }

    public virtual void Draw(in RuntimeDrawContext context)
    {
    }

    public virtual void Dispose()
    {
    }
}
