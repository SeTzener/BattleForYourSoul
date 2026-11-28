using UnityEngine;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// Runtime totals pushed from <see cref="Player.PlayerStats"/> into the active superpower. Kept as a
    /// value type so swapping superpowers only changes the definition and never loses accumulated
    /// upgrades. Effects that are not purchased stay at zero, so placeholders are inert until an
    /// upgrade opts into them.
    /// </summary>
    public struct SuperpowerModifiers
    {
        public float DamageFlat;
        public float DamageMultiplier;
        public float FireRateMultiplier;
        public float ExtraProjectiles;
        public float ProjectileSpeedMultiplier;
        public float RangeFlat;
        public float RicochetBounces;
        public float ImpactRadius;

        // Superstrength stats. Percentages are stored as fractions (0.2 = 20%).
        public float Strength;
        public float CriticalChance;
        public float CriticalDamage;
        public float BonusStunChance;
        public float DefenseFlat;
        public float DefenseKnee;

        // Charge / Super Charge. Zero ChargeDamage means dashing deals no damage.
        public float ChargeDamage;
        public float ChargeKnockbackForce;
        public float ChargeKnockbackDuration;
        public float SuperChargeDuration;

        // Behaviour flags from unique upgrades.
        public bool Hulk;
        public bool ManOfSteel;

        // Action replacements from unique upgrades. Null = use the definition's authored action.
        public SuperpowerActionDefinition PrimaryOverride;
        public SuperpowerActionDefinition SpecialOverride;

        public static SuperpowerModifiers Default => new SuperpowerModifiers
        {
            DamageMultiplier = 1f,
            FireRateMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            DefenseKnee = 50f,
        };
    }
}
