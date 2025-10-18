using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Integrates basic velocity and acceleration to move the owning entity.
    /// </summary>
    public sealed class MovementComponent : ComponentBase
    {
        private Vector2 _previousPosition;

        public Vector2 Velocity { get; set; }

        public Vector2 Acceleration { get; set; }

        public float MaxSpeed { get; set; } = 400f;

        public bool ClampToMaxSpeed { get; set; } = true;

        public Vector2 PreviousPosition => _previousPosition;

        public override void Initialize()
        {
            _previousPosition = Owner.Transform.Position;
        }

        public override void Update(GameTime gameTime)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
                return;

            if (Acceleration != Vector2.Zero)
                Velocity += Acceleration * dt;

            if (ClampToMaxSpeed && Velocity.LengthSquared() > MaxSpeed * MaxSpeed && Velocity != Vector2.Zero)
            {
                Velocity = Vector2.Normalize(Velocity) * MaxSpeed;
            }

            _previousPosition = Owner.Transform.Position;
            Owner.Transform.Position += Velocity * dt;
        }

        public void RevertPosition()
        {
            Owner.Transform.Position = _previousPosition;
            Velocity = Vector2.Zero;
        }

        public void Stop()
        {
            Velocity = Vector2.Zero;
            Acceleration = Vector2.Zero;
        }
    }
}

