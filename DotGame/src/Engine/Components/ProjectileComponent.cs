using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Moves the owning entity in a straight line and applies damage on contact.
    /// </summary>
    public sealed class ProjectileComponent : ComponentBase
    {
        private ColliderComponent? _collider;
        private float _age;

        public Vector2 Velocity { get; set; }

        public float Lifetime { get; set; } = 3f;

        public float Damage { get; set; } = 5f;

        public Entity? Source { get; set; }

        public override void Initialize()
        {
            _collider = Owner.GetComponent<ColliderComponent>();
            if (_collider != null)
            {
                _collider.IsTrigger = true;
                _collider.Collision += OnCollision;
            }
        }

        public override void Update(GameTime gameTime)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
                return;

            Owner.Transform.Position += Velocity * dt;
            _collider?.UpdateBounds();

            _age += dt;
            if (_age >= Lifetime)
            {
                Expire();
            }
        }

        private void OnCollision(Entity other)
        {
            if (Source != null && ReferenceEquals(other, Source))
                return;

            var health = other.GetComponent<HealthComponent>();
            if (health != null && health.IsAlive)
            {
                health.ApplyDamage(Damage);
            }

            Expire();
        }

        private void Expire()
        {
            if (_collider != null)
            {
                _collider.Collision -= OnCollision;
                _collider = null;
            }

            Owner.MarkForRemoval();
            Enabled = false;
        }
    }
}

