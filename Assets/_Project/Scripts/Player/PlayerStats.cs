using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Runtime aggregation of a player's upgrade modifiers. Level-up choices are applied here, and
    /// the resulting values are pushed into the combat and movement components, so no system has to
    /// poll or search for stats.
    ///
    /// Base values are captured from the components on Awake; nothing is read from a definition
    /// asset at runtime beyond the chosen upgrades themselves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
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
        private int _extraProjectiles;
        private float _pickupRadiusMultiplier = 1f;

        /// <summary>Multiplier applied to pickup magnet radii. Read when a pickup is spawned.</summary>
        public float PickupRadiusMultiplier => _pickupRadiusMultiplier;

        private void Awake()
        {
            if (health != null)
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
                case UpgradeStat.PickupRadiusPercent:
                    _pickupRadiusMultiplier *= 1f + modifier.Value * 0.01f;
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
                weapon.SetModifiers(_damageFlat, _damageMultiplier, _fireRateMultiplier, _extraProjectiles, _projectileSpeedMultiplier);
            }

            if (movement != null)
            {
                movement.SetSpeedMultiplier(_moveSpeedMultiplier);
            }
        }
    }
}
