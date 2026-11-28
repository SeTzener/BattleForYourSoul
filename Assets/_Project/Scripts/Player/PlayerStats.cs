using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Progression;
using BattleForYourSoul.Superpowers;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Runtime aggregation of a player's upgrade modifiers. Level-up choices are applied here, and
    /// the resulting values are pushed into the combat and movement components, so no system has to
    /// poll or search for stats.
    ///
    /// Base values come from the <see cref="PlayerDefinition"/> asset when one is assigned,
    /// otherwise they are captured from the components on Awake. Inherent stats (defense and action
    /// overrides) come from the equipped <see cref="SuperpowerDefinition"/> plus taken upgrades.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        private const float MaxCriticalChance = 1f;
        private const float MaxCriticalDamage = 0.5f;
        private const float MaxBonusStunChance = 0.5f;

        // Hulk / Man of Steel low-health scaling. Tuning lives here until playtests settle it; see
        // PROGRESSION.md. Both forms are mutually exclusive, enforced by RunUpgradeState.
        private const float HulkMaxAttackBonus = 0.5f;
        private const float SteelMaxDefenseBonus = 40f;

        [Header("Definition")]
        [Tooltip("Optional definition asset. When set, its base stats are used instead of the components'.")]
        [SerializeField] private PlayerDefinition definition;

        [Header("Wiring")]
        [SerializeField] private Health health;
        [SerializeField] private SuperpowerController superpower;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerDash dash;
        [SerializeField] private SuperpowerSelection superpowerSelection;
        [SerializeField] private RunUpgradeState runUpgrades;

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
        private int _instantDashCharges;

        private float _strength;
        private float _criticalChance;
        private float _criticalDamage;
        private float _bonusStunChance;
        private float _defenseFlat;
        private float _chargeDamage;
        private float _chargeKnockbackForce;
        private float _chargeKnockbackDuration;
        private float _superChargeDuration;
        private bool _hulk;
        private bool _manOfSteel;

        private SuperpowerDefinition _equipped;

        public SuperpowerModifiers CurrentModifiers { get; private set; } = SuperpowerModifiers.Default;

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

            if (superpowerSelection != null)
            {
                _equipped = superpowerSelection.SelectedSuperpower;
            }

            if (health != null)
            {
                health.Changed += HandleHealthChanged;
            }

            Push();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Changed -= HandleHealthChanged;
            }
        }

        /// <summary>Man of Steel's defense grows as health falls; keep the runtime defense in sync.</summary>
        private void HandleHealthChanged(Health _)
        {
            if (_manOfSteel && !_hulk)
            {
                ApplyDefense();
            }
        }

        private void OnEnable()
        {
            if (superpowerSelection != null)
            {
                superpowerSelection.SelectionChanged += HandleSuperpowerChanged;
            }
        }

        private void OnDisable()
        {
            if (superpowerSelection != null)
            {
                superpowerSelection.SelectionChanged -= HandleSuperpowerChanged;
            }
        }

        private void HandleSuperpowerChanged(SuperpowerDefinition value)
        {
            _equipped = value;
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

            var behavior = upgrade.Behavior;
            bool hulk = (behavior & SuperpowerUpgradeBehavior.Hulk) != 0;
            bool manOfSteel = (behavior & SuperpowerUpgradeBehavior.ManOfSteel) != 0;

            // Defence in depth: RunUpgradeState filters exclusive cards, but a direct/debug Apply must
            // not be able to leave both forms active at once.
            if (hulk)
            {
                _hulk = true;
                _manOfSteel = false;
            }
            else if (manOfSteel)
            {
                _manOfSteel = true;
                _hulk = false;
            }

            Push();
        }

        /// <summary>Clears every run upgrade. Called when a fresh Samsara stage begins.</summary>
        public void ResetUpgrades()
        {
            _maxHealthBonus = 0f;
            _damageFlat = 0f;
            _damageMultiplier = 1f;
            _fireRateMultiplier = 1f;
            _moveSpeedMultiplier = 1f;
            _projectileSpeedMultiplier = 1f;
            _rangeBonus = 0f;
            _ricochetBounces = 0f;
            _impactRadius = 0f;
            _extraProjectiles = 0;
            _instantDashCharges = 0;
            _strength = 0f;
            _criticalChance = 0f;
            _criticalDamage = 0f;
            _bonusStunChance = 0f;
            _defenseFlat = 0f;
            _chargeDamage = 0f;
            _chargeKnockbackForce = 0f;
            _chargeKnockbackDuration = 0f;
            _superChargeDuration = 0f;
            _hulk = false;
            _manOfSteel = false;

            // Samsara is a new run, so restore base health before Push re-applies base defense.
            if (health != null)
            {
                health.SetMax(_baseMaxHealth, healDifference: false);
                health.ResetToFull();
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
                case UpgradeStat.InstantDashCharges:
                    _instantDashCharges += Mathf.RoundToInt(modifier.Value);
                    break;
                case UpgradeStat.Strength:
                    _strength += modifier.Value;
                    break;
                case UpgradeStat.CriticalChancePercent:
                    _criticalChance = Mathf.Min(MaxCriticalChance, _criticalChance + modifier.Value * 0.01f);
                    break;
                case UpgradeStat.CriticalDamagePercent:
                    _criticalDamage = Mathf.Min(MaxCriticalDamage, _criticalDamage + modifier.Value * 0.01f);
                    break;
                case UpgradeStat.BonusStunChancePercent:
                    _bonusStunChance = Mathf.Min(MaxBonusStunChance, _bonusStunChance + modifier.Value * 0.01f);
                    break;
                case UpgradeStat.DefenseFlat:
                    _defenseFlat += modifier.Value;
                    break;
                case UpgradeStat.ChargeDamage:
                    _chargeDamage = Mathf.Max(_chargeDamage, modifier.Value);
                    break;
                case UpgradeStat.ChargeKnockback:
                    _chargeKnockbackForce = Mathf.Max(_chargeKnockbackForce, modifier.Value);
                    _chargeKnockbackDuration = _chargeKnockbackForce > 0f ? 0.22f : 0f;
                    break;
                case UpgradeStat.SuperCharge:
                    _superChargeDuration = Mathf.Max(_superChargeDuration, modifier.Value);
                    break;
            }
        }

        private void Push()
        {
            if (health != null)
            {
                // Heal by the increase so a max-health upgrade feels immediate.
                health.SetMax(_baseMaxHealth + _maxHealthBonus, healDifference: true);
                ApplyDefense();
            }

            var modifiers = new SuperpowerModifiers
            {
                DamageFlat = _damageFlat,
                DamageMultiplier = _damageMultiplier,
                FireRateMultiplier = _fireRateMultiplier,
                ExtraProjectiles = _extraProjectiles,
                ProjectileSpeedMultiplier = _projectileSpeedMultiplier,
                RangeFlat = _rangeBonus,
                RicochetBounces = _ricochetBounces,
                ImpactRadius = _impactRadius,
                Strength = _strength,
                CriticalChance = _criticalChance,
                CriticalDamage = _criticalDamage,
                BonusStunChance = _bonusStunChance,
                DefenseFlat = _defenseFlat,
                DefenseKnee = _equipped != null ? _equipped.DefenseKnee : 50f,
                ChargeDamage = _chargeDamage,
                ChargeKnockbackForce = _chargeKnockbackForce,
                ChargeKnockbackDuration = _chargeKnockbackDuration,
                SuperChargeDuration = _superChargeDuration,
                Hulk = _hulk,
                ManOfSteel = _manOfSteel,
                PrimaryOverride = runUpgrades != null ? runUpgrades.GetPrimaryOverride(_equipped) : null,
                SpecialOverride = runUpgrades != null ? runUpgrades.GetSpecialOverride(_equipped) : null,
            };
            CurrentModifiers = modifiers;

            if (superpower != null)
            {
                superpower.SetModifiers(modifiers);
                superpower.RefreshActions(_equipped);
            }

            if (movement != null)
            {
                movement.SetSpeedMultiplier(_moveSpeedMultiplier);
            }

            if (dash != null)
            {
                dash.SetInstantCharges(_instantDashCharges);
            }
        }

        /// <summary>
        /// Applies inherent defense, purchased defense and Man of Steel's low-health bonus. Kept separate
        /// from <see cref="Push"/> so health changes can refresh it without rewriting every modifier.
        /// </summary>
        private void ApplyDefense()
        {
            if (health == null)
            {
                return;
            }

            float points = (_equipped != null ? _equipped.Defense : 0f) + _defenseFlat;
            if (_manOfSteel && !_hulk)
            {
                points += SteelMaxDefenseBonus * (1f - health.Normalized);
            }

            float knee = _equipped != null ? _equipped.DefenseKnee : 50f;
            health.SetDefense(points, Mathf.Max(knee, 1f));
        }

        /// <summary>
        /// Damage formula: (base + Strength + flat) * percent multipliers, then Hulk's low-health
        /// attack bonus and finally a critical multiplier of (2 + criticalDamage). Caps are enforced
        /// here, not in the upgrade assets.
        /// </summary>
        public float RollDamage(float baseDamage, out bool critical)
        {
            float value = (baseDamage + _strength + _damageFlat) * _damageMultiplier;
            if (_hulk && health != null)
            {
                value *= 1f + HulkMaxAttackBonus * (1f - health.Normalized);
            }

            critical = _criticalChance > 0f && Random.value < _criticalChance;
            if (critical)
            {
                value *= 2f + _criticalDamage;
            }

            return value;
        }

        /// <summary>Bonus stun chance from level-ups. Bosses are filtered by the enemy itself.</summary>
        public bool RollBonusStun()
        {
            return _bonusStunChance > 0f && Random.value < _bonusStunChance;
        }
    }
}
