using System;
using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Tracks hit points for the owning entity and raises events on damage/death.
    /// </summary>
    public sealed class HealthComponent : ComponentBase
    {
        private float _currentHealth;
        private bool _isAlive;

        public float MaxHealth { get; set; } = 100f;

        public float CurrentHealth => _currentHealth;

        public bool IsAlive => _isAlive;

        public event Action<HealthComponent, float>? Damaged;

        public event Action<HealthComponent>? Died;

        public override void Initialize()
        {
            Reset();
        }

        public void Reset(float? newMaxHealth = null)
        {
            if (newMaxHealth.HasValue && newMaxHealth.Value > 0f)
            {
                MaxHealth = newMaxHealth.Value;
            }

            _currentHealth = MathF.Max(1f, MaxHealth);
            _isAlive = true;
            Enabled = true;
        }

        public void ApplyDamage(float amount)
        {
            if (!_isAlive || amount <= 0f)
                return;

            _currentHealth = MathF.Max(0f, _currentHealth - amount);
            Damaged?.Invoke(this, amount);

            if (_currentHealth <= 0f)
            {
                _isAlive = false;
                Enabled = false;
                Died?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            if (!_isAlive || amount <= 0f)
                return;

            _currentHealth = MathF.Min(MaxHealth, _currentHealth + amount);
        }
    }
}

