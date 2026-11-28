using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Runtime hit points for any actor. This is per-instance state and never lives in a definition
    /// asset. Other systems react through <see cref="Changed"/> and <see cref="Died"/> instead of
    /// polling, so nothing has to search the scene to keep a health bar in sync.
    ///
    /// Defense uses diminishing returns: reduction = defense / (defense + knee), capped at 80%.
    /// See PROGRESSION.md for the formula and tuning notes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Tooltip("Hit points at full health. Overwritten by a definition when one drives this actor.")]
        [SerializeField, Min(1f)] private float maxHealth = 10f;

        private const float MaxReduction = 0.8f;

        private readonly HashSet<object> _immunityOwners = new HashSet<object>();

        private float _current;
        private float _defensePoints;
        private float _defenseKnee = 50f;
        private bool _initialized;

        public float Max => maxHealth;

        // Before Awake has run, report full health so a component that reads this during its own
        // OnEnable (whose order against our Awake is undefined) never sees a spurious zero.
        public float Current => _initialized ? _current : maxHealth;

        public float Normalized => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        public bool IsAlive => Current > 0f;

        /// <summary>Reduction after defense, from 0 to 0.8.</summary>
        public float DamageReduction => _defensePoints <= 0f
            ? 0f
            : Mathf.Min(MaxReduction, _defensePoints / (_defensePoints + Mathf.Max(1f, _defenseKnee)));

        /// <summary>
        /// True while any effect (dash, moving attack, …) holds an immunity lease. Leases are
        /// owner-keyed so overlapping effects cannot clear each other's protection.
        /// </summary>
        public bool IsDamageImmune => _immunityOwners.Count > 0;

        /// <summary>Raised after any change to <see cref="Current"/>.</summary>
        public event Action<Health> Changed;

        /// <summary>Raised exactly once, when health first reaches zero.</summary>
        public event Action<Health> Died;

        private void Awake()
        {
            _current = maxHealth;
            _initialized = true;
        }

        /// <summary>Sets defense points and the knee of the diminishing-returns curve.</summary>
        public void SetDefense(float points, float knee)
        {
            _defensePoints = Mathf.Max(0f, points);
            _defenseKnee = Mathf.Max(1f, knee);
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
            if (!IsAlive || IsDamageImmune || damage.Amount <= 0f)
            {
                return;
            }

            float amount = damage.Amount * (1f - DamageReduction);
            _current = Mathf.Max(0f, Current - amount);
            _initialized = true;
            Changed?.Invoke(this);

            if (_current <= 0f)
            {
                Died?.Invoke(this);
            }
        }

        /// <summary>Adds a damage-immunity lease. The same owner can add it repeatedly.</summary>
        public void AddDamageImmunity(object owner)
        {
            if (owner != null)
            {
                _immunityOwners.Add(owner);
            }
        }

        /// <summary>Releases one owner's damage-immunity lease, leaving any others intact.</summary>
        public void RemoveDamageImmunity(object owner)
        {
            if (owner != null)
            {
                _immunityOwners.Remove(owner);
            }
        }

        /// <summary>Clears every lease. Used by a full reset, not by a single effect ending.</summary>
        public void ClearDamageImmunity()
        {
            _immunityOwners.Clear();
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
            _immunityOwners.Clear();
            Changed?.Invoke(this);
        }
    }
}
