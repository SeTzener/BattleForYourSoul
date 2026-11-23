namespace BattleForYourSoul.Weapons
{
    /// <summary>
    /// Runtime totals pushed from <see cref="Player.PlayerStats"/> into the active weapon. Kept as a
    /// value type so swapping weapons only changes the definition and never loses accumulated
    /// upgrades. Effects that are not purchased stay at zero, so placeholders are inert until an
    /// upgrade opts into them.
    /// </summary>
    public struct WeaponModifiers
    {
        public float DamageFlat;
        public float DamageMultiplier;
        public float FireRateMultiplier;
        public float ExtraProjectiles;
        public float ProjectileSpeedMultiplier;
        public float RangeFlat;
        public float RicochetBounces;
        public float ImpactRadius;

        public static WeaponModifiers Default => new WeaponModifiers
        {
            DamageMultiplier = 1f,
            FireRateMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
        };
    }
}
