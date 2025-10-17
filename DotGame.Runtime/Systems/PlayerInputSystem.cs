using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DotGame.Runtime.Systems;

public sealed class PlayerInputSystem : RuntimeEntitySystemBase
{
    public override int Order => -100;

    public override void Update(GameClock clock, EntityWorld world)
    {
        var input = UpdateContext.Input;
        foreach (var entity in world.Entities)
        {
            if (!entity.TryGet(out PlayerControlledComponent? _) || !entity.TryGet(out MovementComponent? movement))
            {
                continue;
            }

            var direction = Vector2.Zero;

            if (input.IsKeyDown(Keys.W) || input.IsKeyDown(Keys.Up))
                direction.Y -= 1f;
            if (input.IsKeyDown(Keys.S) || input.IsKeyDown(Keys.Down))
                direction.Y += 1f;
            if (input.IsKeyDown(Keys.A) || input.IsKeyDown(Keys.Left))
                direction.X -= 1f;
            if (input.IsKeyDown(Keys.D) || input.IsKeyDown(Keys.Right))
                direction.X += 1f;

            if (direction != Vector2.Zero)
            {
                direction.Normalize();
            }

            movement.Direction = direction;
        }
    }
}
