using System;
using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Simple chase-and-attack controller used for test combat scenarios.
    /// </summary>
    public sealed class EnemyAIComponent : ComponentBase
    {
        private float _attackCooldownTimer;

        public Entity? Target { get; set; }

        public float MoveSpeed { get; set; } = 160f;

        public float AttackRange { get; set; } = 48f;

        public float AttackDamage { get; set; } = 10f;

        public float AttackCooldownSeconds { get; set; } = 1.25f;

        public override void Update(GameTime gameTime)
        {
            if (!Enabled)
                return;

            var movement = Owner.GetComponent<MovementComponent>();
            var target = Target;
            if (movement == null || target == null)
            {
                movement?.Stop();
                return;
            }

            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
                return;

            _attackCooldownTimer = MathF.Max(0f, _attackCooldownTimer - dt);

            var targetHealth = target.GetComponent<HealthComponent>();
            if (targetHealth != null && !targetHealth.IsAlive)
            {
                movement.Stop();
                return;
            }

            var ownerCollider = Owner.GetComponent<ColliderComponent>();
            var targetCollider = target.GetComponent<ColliderComponent>();
            var ownerCenter = Owner.Transform.Position + (ownerCollider?.Size ?? Vector2.Zero) * 0.5f;
            var targetCenter = target.Transform.Position + (targetCollider?.Size ?? Vector2.Zero) * 0.5f;
            var toTarget = targetCenter - ownerCenter;
            var distance = toTarget.Length();

            if (distance > 0.01f)
            {
                toTarget /= distance;
            }

            var inRange = distance <= AttackRange;

            if (!inRange)
            {
                movement.MaxSpeed = MoveSpeed;
                movement.Velocity = toTarget * MoveSpeed;
                return;
            }

            movement.Stop();

            if (_attackCooldownTimer > 0f)
                return;

            var targetHealthComponent = targetHealth ?? target.GetComponent<HealthComponent>();
            if (targetHealthComponent != null && targetHealthComponent.IsAlive)
            {
                targetHealthComponent.ApplyDamage(AttackDamage);
                _attackCooldownTimer = AttackCooldownSeconds;
            }
        }

        public void ForceCooldown(float seconds)
        {
            _attackCooldownTimer = MathF.Max(_attackCooldownTimer, seconds);
        }
    }
}

