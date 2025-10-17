using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using Microsoft.Xna.Framework;

namespace DotGame.Runtime.Systems;

public sealed class CameraFollowSystem : RuntimeEntitySystemBase
{
    public override int Order => 0;

    public override void Update(GameClock clock, EntityWorld world)
    {
        var runtime = UpdateContext.Runtime;
        if (runtime is null)
            return;

        var camera = runtime.Camera;
        foreach (var entity in world.Entities)
        {
            if (!entity.TryGet(out CameraTargetComponent? _) || !entity.TryGet(out TransformComponent? transform))
                continue;

            var focusPosition = transform.Position;
            if (entity.TryGet(out ColliderComponent? collider))
            {
                var half = collider.Size * 0.5f;
                focusPosition += new Vector2(half.X, half.Y);
            }

            camera.LookAt(focusPosition);
            break;
        }
    }
}
