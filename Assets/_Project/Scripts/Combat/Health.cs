using System;
using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Runtime hit points for any actor. This is per-instance state and never lives in a definition
    /// asset. Other systems react through <see cref="Changed"/> and <see cref="Died"/> instead of
    /// polling, so nothing has to search the scene to keep a health bar in sync.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Tooltip("Hit points at full health. Overwritten by a definition when one drives this actor.")]
        [SerializeField, Min(1f)] private float maxHealth = 10f;

        private float _current;
        private bool _initialized;

        public float Max => maxHealth;

        // Before Awake has run, report full health so a component that reads this during its own
        // OnEnable (whose order against our Awake is undefined) never sees a spurious zero.
        public float Current => _initialized ? _current : maxHealth;

        public float Normalized => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        public bool IsAlive => Current > 0f;

        /// <summary>Raised after any change to <see cref="Current"/>.</summary>
        public event Action<Health> Changed;

        /// <summary>Raised exactly once, when health first reaches zero.</summary>
        public event Action<Health> Died;

        private void Awake()
        {
            _current = maxHealth;
            _initialized = true;
        }

        /// <summary>
        /// Sets the maximum and (by default) refills. Used when a pooled actor is reconfigured from
        /// a definition on spawn.
        /// </summary>
        public void Configure(float maximum, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, maximum);
            if (refill)
            {
                _current = maxHealth;
            }
            else
            {
                _current = Mathf.Min(Current, maxHealth);
            }

            _initialized = true;
            Changed?.Invoke(this);
        }

        /// <inheritdoc />
        public void TakeDamage(in DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0f)
            {
                return;
            }

            _current = Mathf.Max(0f, Current - damage.Amount);
            _initialized = true;
            Changed?.Invoke(this);

            if (_current <= 0f)
            {
                Died?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _current = Mathf.Min(maxHealth, Current + amount);
            _initialized = true;
            Changed?.Invoke(this);
        }

        /// <summary>
        /// Changes the maximum without refilling. When <paramref name="healDifference"/> is set and the
        /// maximum grows, current health grows by the same amount, so a max-health upgrade is felt at
        /// once. Used by upgrades that raise the cap mid-run.
        /// </summary>
        public void SetMax(float maximum, bool healDifference)
        {
            float previousMax = maxHealth;
            maxHealth = Mathf.Max(1f, maximum);

            float current = _initialized ? _current : previousMax;
            if (healDifference && maxHealth > previousMax)
            {
                current += maxHealth - previousMax;
            }

            _current = Mathf.Clamp(current, 0f, maxHealth);
            _initialized = true;
            Changed?.Invoke(this);
        }

        /// <summary>Restores full health and re-arms death handling. Used on pooled respawn.</summary>
        public void ResetToFull()
        {
            _current = maxHealth;
            _initialized = true;
            Changed?.Invoke(this);
        }
    }
}
