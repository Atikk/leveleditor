using DotGame.Core.Entities;
using DotGame.Core.States;

namespace DotGame.Core.Systems;

public interface IEntitySystem
{
    int Order { get; }

    void Update(GameClock clock, EntityWorld world);

    void Draw(GameClock clock, EntityWorld world);
}
