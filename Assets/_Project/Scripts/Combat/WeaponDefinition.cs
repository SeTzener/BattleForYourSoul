using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Authoring-time definition of a weapon. Immutable data only: all per-shot runtime state lives
    /// on <see cref="WeaponController"/>, so two actors can share one definition safely.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Weapon", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Firing")]
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0.01f)] private float fireInterval = 0.32f;

        [Tooltip("Projectiles fired per shot.")]
        [SerializeField, Min(1)] private int projectilesPerShot = 1;

        [Tooltip("Total arc in degrees spread across the projectiles of one shot.")]
        [SerializeField, Range(0f, 180f)] private float spreadDegrees = 0f;

        [Header("Targeting")]
        [Tooltip("Only enemies within this radius (world units) are targeted.")]
        [SerializeField, Min(0.1f)] private float targetRange = 7f;

        [Header("Projectile")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 14f;

        [Tooltip("Seconds before a projectile expires on its own.")]
        [SerializeField, Min(0.01f)] private float projectileLifetime = 1.1f;

        [SerializeField, Min(0f)] private float damage = 1f;

        public float FireInterval => fireInterval;
        public int ProjectilesPerShot => projectilesPerShot;
        public float SpreadDegrees => spreadDegrees;
        public float TargetRange => targetRange;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float Damage => damage;
    }
}
