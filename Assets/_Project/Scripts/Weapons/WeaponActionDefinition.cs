using UnityEngine;

namespace BattleForYourSoul.Weapons
{
    /// <summary>How one weapon action delivers its damage.</summary>
    public enum WeaponAttackShape
    {
        MeleeArc,
        Line,
        Projectile,
    }

    /// <summary>
    /// One authored attack of a weapon (its primary or special action). Immutable data: all
    /// cooldowns and reusable buffers live on <see cref="Combat.WeaponController"/>, so several
    /// actors can share the same definition safely.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Weapon Action", fileName = "WeaponAction")]
    public sealed class WeaponActionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Attack";

        [Header("Attack")]
        [SerializeField] private WeaponAttackShape shape = WeaponAttackShape.MeleeArc;

        [Tooltip("Seconds before this action can be used again, before fire-rate upgrades.")]
        [SerializeField, Min(0.05f)] private float cooldown = 0.4f;

        [SerializeField, Min(0f)] private float damage = 1f;

        [Tooltip("Reach in world units for melee/line attacks, or targeting radius for projectiles.")]
        [SerializeField, Min(0.1f)] private float range = 1.5f;

        [Tooltip("Total arc in degrees for a melee attack.")]
        [SerializeField, Range(10f, 360f)] private float arcDegrees = 120f;

        [Tooltip("Width of a line attack in world units.")]
        [SerializeField, Min(0.05f)] private float lineWidth = 0.4f;

        [Header("Projectile")]
        [SerializeField, Min(1)] private int projectilesPerShot = 1;

        [Tooltip("Total arc in degrees spread across the projectiles of one shot.")]
        [SerializeField, Range(0f, 180f)] private float spreadDegrees = 0f;

        [SerializeField, Min(0.1f)] private float projectileSpeed = 14f;

        [SerializeField, Min(0.01f)] private float projectileLifetime = 1.1f;

        [SerializeField] private Sprite projectileSprite;

        [Header("Melee / line hit VFX")]
        [Tooltip("One-shot sprite shown at the hit point. Projectile actions ignore this.")]
        [SerializeField] private Sprite hitVfxSprite;

        [SerializeField] private Color hitVfxColor = Color.white;

        [Tooltip("World-space size of the effect sprite.")]
        [SerializeField, Min(0.1f)] private float hitVfxSize = 1.4f;

        [SerializeField, Min(0.01f)] private float hitVfxDuration = 0.22f;

        [Tooltip("Degrees swept across the effect lifetime. Give a slash motion; leave a thrust straight.")]
        [SerializeField] private float hitVfxSweep;

        public string DisplayName => displayName;
        public WeaponAttackShape Shape => shape;
        public float Cooldown => cooldown;
        public float Damage => damage;
        public float Range => range;
        public float ArcDegrees => arcDegrees;
        public float LineWidth => lineWidth;
        public int ProjectilesPerShot => projectilesPerShot;
        public float SpreadDegrees => spreadDegrees;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public Sprite ProjectileSprite => projectileSprite;
        public Sprite HitVfxSprite => hitVfxSprite;
        public Color HitVfxColor => hitVfxColor;
        public float HitVfxSize => hitVfxSize;
        public float HitVfxDuration => hitVfxDuration;
        public float HitVfxSweep => hitVfxSweep;
    }
}
