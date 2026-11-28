using System;
using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>How one superpower action delivers its damage.</summary>
    public enum SuperpowerAttackShape
    {
        MeleeArc,
        Line,
        Projectile,
    }

    /// <summary>
    /// Placeholder animation data. Frames are swapped on the sprite child; the hop is a local sprite
    /// offset only, never a physics jump. LungeDistance/LungeDuration describe the small real-world
    /// forward step played alongside it, now driven by <c>AttackMovement</c>.
    /// </summary>
    [Serializable]
    public struct AttackAnimation
    {
        public Sprite[] Frames;
        [Min(0.01f)] public float FrameDuration;
        public Vector2 HopOffset;
        [Tooltip("World units of the deterministic forward attack step.")]
        [Min(0f)] public float LungeDistance;

        [Tooltip("Duration of the forward attack step in seconds.")]
        [Min(0.01f)] public float LungeDuration;
    }

    /// <summary>
    /// One authored attack of a superpower (its primary or special action). Immutable data: all
    /// cooldowns and reusable buffers live on <see cref="SuperpowerController"/>, so several actors
    /// can share the same definition safely.
    ///
    /// Cooldown equals the animation length unless an upgrade explicitly changes the action. The
    /// 3-hit Northern Star action therefore authors roughly twice the punch cooldown. A moving
    /// attack (<see cref="SweepHitsWhileLunging"/>) sweeps its hit query along the lunge path and
    /// is immune for that window; multi-hit timing is reserved for stationary attacks.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Superpower Action", fileName = "SuperpowerAction")]
    public sealed class SuperpowerActionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Attack";

        [Header("Attack")]
        [SerializeField] private SuperpowerAttackShape shape = SuperpowerAttackShape.MeleeArc;

        [Tooltip("Seconds before this action can be used again, before fire-rate upgrades.")]
        [SerializeField, Min(0.05f)] private float cooldown = 0.4f;

        [SerializeField, Min(0f)] private float damage = 1f;

        [Tooltip("Reach in world units for melee/line attacks, or targeting radius for projectiles.")]
        [SerializeField, Min(0.1f)] private float range = 1.5f;

        [Tooltip("Total arc in degrees for a melee attack.")]
        [SerializeField, Range(10f, 360f)] private float arcDegrees = 120f;

        [Tooltip("Width of a line attack in world units.")]
        [SerializeField, Min(0.05f)] private float lineWidth = 0.4f;

        [Header("Timeline")]
        [Tooltip("Number of damage frames. Each frame rolls its own damage, crit and on-hit effects.")]
        [SerializeField, Min(1)] private int hitCount = 1;

        [Tooltip("Delay before the first damage frame.")]
        [SerializeField, Min(0f)] private float hitDelay;

        [Tooltip("Spacing between damage frames of a multi-hit action.")]
        [SerializeField, Min(0f)] private float hitInterval;

        [Tooltip("Moving attack: sweep the melee query along the whole lunge, hitting each foe once, " +
                 "and grant damage immunity plus enemy pass-through for the movement window.")]
        [SerializeField] private bool sweepHitsWhileLunging;

        [Header("On hit")]
        [SerializeField] private HitEffects onHit;

        [Header("Placeholder animation")]
        [SerializeField] private AttackAnimation animation;

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
        public SuperpowerAttackShape Shape => shape;
        public float Cooldown => cooldown;
        public float Damage => damage;
        public float Range => range;
        public float ArcDegrees => arcDegrees;
        public float LineWidth => lineWidth;
        public int HitCount => Mathf.Max(1, hitCount);
        public float HitDelay => Mathf.Max(0f, hitDelay);
        public float HitInterval => Mathf.Max(0f, hitInterval);
        public bool SweepHitsWhileLunging => sweepHitsWhileLunging;
        public HitEffects OnHit => onHit;
        public AttackAnimation Animation => animation;
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
