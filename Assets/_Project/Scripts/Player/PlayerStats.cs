using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Progression;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Runtime aggregation of a player's upgrade modifiers. Level-up choices are applied here, and
    /// the resulting values are pushed into the combat and movement components, so no system has to
    /// poll or search for stats.
    ///
    /// Base values come from the <see cref="PlayerDefinition"/> asset when one is assigned,
    /// otherwise they are captured from the components on Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Definition")]
        [Tooltip("Optional definition asset. When set, its base stats are used instead of the components'.")]
        [SerializeField] private PlayerDefinition definition;

        [Header("Wiring")]
        [SerializeField] private Health health;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private PlayerMovement movement;

        private float _baseMaxHealth;
        private float _maxHealthBonus;
        private float _damageFlat;
        private float _damageMultiplier = 1f;
        private float _fireRateMultiplier = 1f;
        private float _moveSpeedMultiplier = 1f;
        private float _projectileSpeedMultiplier = 1f;
        private float _rangeBonus;
        private float _ricochetBounces;
        private float _impactRadius;
        private int _extraProjectiles;

        private void Awake()
        {
            if (definition != null)
            {
                _baseMaxHealth = definition.MaxHealth;
            }
            else if (health != null)
            {
                _baseMaxHealth = health.Max;
            }

            Push();
        }

        /// <summary>Applies every modifier on an upgrade and pushes the new totals out.</summary>
        public void Apply(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            var modifiers = upgrade.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                Accumulate(modifiers[i]);
            }

            Push();
        }

        private void Accumulate(StatModifier modifier)
        {
            switch (modifier.Stat)
            {
                case UpgradeStat.DamageFlat:
                    _damageFlat += modifier.Value;
                    break;
                case UpgradeStat.DamagePercent:
                    _damageMultiplier *= 1f + modifier.Value * 0.01f;
                    break;
                case UpgradeStat.FireRatePercent:
                    _fireRateMultiplier *= 1f + modifier.Value * 0.01f;
                    break;
                case UpgradeStat.MoveSpeedPercent:
                    _moveSpeedMultiplier *= 1f + modifier.Value * 0.01f;
                    break;
                case UpgradeStat.ProjectileSpeedPercent:
                    _projectileSpeedMultiplier *= 1f + modifier.Value * 0.01f;
                    break;
                case UpgradeStat.MaxHealthFlat:
                    _maxHealthBonus += modifier.Value;
                    break;
                case UpgradeStat.ExtraProjectiles:
                    _extraProjectiles += Mathf.RoundToInt(modifier.Value);
                    break;
                case UpgradeStat.RangeFlat:
                    _rangeBonus += modifier.Value;
                    break;
                case UpgradeStat.RicochetBounces:
                    _ricochetBounces += Mathf.RoundToInt(modifier.Value);
                    break;
                case UpgradeStat.ImpactRadius:
                    _impactRadius += modifier.Value;
                    break;
            }
        }

        private void Push()
        {
            if (health != null)
            {
                // Heal by the increase so a max-health upgrade feels immediate.
                health.SetMax(_baseMaxHealth + _maxHealthBonus, healDifference: true);
            }

            if (weapon != null)
            {
                var modifiers = new WeaponModifiers
                {
                    DamageFlat = _damageFlat,
                    DamageMultiplier = _damageMultiplier,
                    FireRateMultiplier = _fireRateMultiplier,
                    ExtraProjectiles = _extraProjectiles,
                    ProjectileSpeedMultiplier = _projectileSpeedMultiplier,
                    RangeFlat = _rangeBonus,
                    RicochetBounces = _ricochetBounces,
                    ImpactRadius = _impactRadius,
                };
                weapon.SetModifiers(modifiers);
            }

            if (movement != null)
            {
                movement.SetSpeedMultiplier(_moveSpeedMultiplier);
            }
        }
    }
}
