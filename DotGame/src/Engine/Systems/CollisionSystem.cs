using System.Collections.Generic;
using Dotgame.Avalonia.Engine.Components;
using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Engine.Systems
{
    /// <summary>
    /// Performs broad-phase AABB collision checks and dispatches collision events.
    /// </summary>
    public sealed class CollisionSystem
    {
        private readonly List<ColliderComponent> _colliders = new();

        public void Process(GameTime gameTime, IReadOnlyList<Entity> entities)
        {
            _colliders.Clear();

            for (var i = 0; i < entities.Count; i++)
            {
                var collider = entities[i].GetComponent<ColliderComponent>();
                if (collider is { Enabled: true })
                {
                    collider.UpdateBounds();
                    _colliders.Add(collider);
                }
            }

            for (var i = 0; i < _colliders.Count - 1; i++)
            {
                var a = _colliders[i];
                for (var j = i + 1; j < _colliders.Count; j++)
                {
                    var b = _colliders[j];
                    if (a.Bounds.Intersects(b.Bounds))
                    {
                        HandleCollision(a, b);
                    }
                }
            }
        }

        private static void HandleCollision(ColliderComponent first, ColliderComponent second)
        {
            first.RaiseCollision(second.Owner);
            second.RaiseCollision(first.Owner);

            if (first.IsTrigger || second.IsTrigger)
                return;

            if (!first.IsStatic)
            {
                ResolveDynamic(first);
            }

            if (!second.IsStatic)
            {
                ResolveDynamic(second);
            }
        }

        private static void ResolveDynamic(ColliderComponent collider)
        {
            var mover = collider.Owner.GetComponent<MovementComponent>();
            if (mover != null)
            {
                mover.RevertPosition();
                collider.UpdateBounds();
            }
        }
    }
}

