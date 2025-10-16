using DotGame.Core.Entities;
using DotGame.Core.States;

namespace DotGame.Core.Systems;

public abstract class EntitySystemBase : IEntitySystem
{
    public virtual int Order => 0;

    public virtual void Update(GameClock clock, EntityWorld world)
    {
    }

    public virtual void Draw(GameClock clock, EntityWorld world)
    {
    }
}
