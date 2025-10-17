using System;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace DotGame.Runtime.Systems;

public sealed class MovementSystem : RuntimeEntitySystemBase
{
    private readonly ICollisionWorld _collisionWorld;

    public MovementSystem(ICollisionWorld collisionWorld)
    {
        _collisionWorld = collisionWorld ?? throw new ArgumentNullException(nameof(collisionWorld));
    }

    public override int Order => -50;

    public override void Update(GameClock clock, EntityWorld world)
    {
        var deltaTime = (float)clock.Delta.TotalSeconds;
        if (deltaTime <= 0f)
            return;

        foreach (var entity in world.Entities)
        {
            if (!entity.TryGet(out MovementComponent? movement) ||
                !entity.TryGet(out TransformComponent? transform) ||
                !entity.TryGet(out ColliderComponent? collider))
            {
                continue;
            }

            var direction = movement.Direction;
            if (direction == Vector2.Zero)
                continue;

            var moveVector = direction * movement.Speed * deltaTime;
            var position = transform.Position;

            position = ResolveAxis(position, collider.Size, new Vector2(moveVector.X, 0f));
            position = ResolveAxis(position, collider.Size, new Vector2(0f, moveVector.Y));
            position = ClampToWorld(position, collider.Size);

            transform.Position = position;
        }
    }

    private Vector2 ResolveAxis(Vector2 position, Vector2 size, Vector2 delta)
    {
        if (delta == Vector2.Zero)
            return position;

        var candidate = position + delta;
        var bounds = new RectangleF(candidate, size);
        return Collides(bounds) ? position : candidate;
    }

    private bool Collides(RectangleF bounds)
    {
        var worldBounds = _collisionWorld.WorldBounds;
        if (bounds.Left < worldBounds.Left || bounds.Right > worldBounds.Right ||
            bounds.Top < worldBounds.Top || bounds.Bottom > worldBounds.Bottom)
        {
            return true;
        }

        foreach (var staticCollider in _collisionWorld.StaticColliders)
        {
            if (staticCollider.Intersects(bounds))
                return true;
        }

        return false;
    }

    private Vector2 ClampToWorld(Vector2 position, Vector2 size)
    {
        var worldBounds = _collisionWorld.WorldBounds;
        var min = new Vector2(worldBounds.X, worldBounds.Y);
        var max = new Vector2(worldBounds.Right - size.X, worldBounds.Bottom - size.Y);
        return Vector2.Clamp(position, min, max);
    }
}
