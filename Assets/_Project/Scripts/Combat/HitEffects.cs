using System;
using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Optional on-hit effects carried by every hit. Defaults are inert, so existing attacks stay
    /// unchanged until an action or upgrade opts into them.
    ///
    /// Knockback always travels along the hit direction (away from the attacker). Root, stun and
    /// contact-damage suppression are kept separate: a root stops movement, a stun also suppresses
    /// contact damage, and suppression alone can disable contact damage on any rank (including a
    /// boss that ignores all control effects).
    /// </summary>
    [Serializable]
    public struct HitEffects
    {
        [Tooltip("Seconds a Foe is rooted. 0 = no root.")]
        [Min(0f)] public float RootDuration;

        [Tooltip("Seconds a Special Foe is rooted as well. Foes are always affected by RootDuration.")]
        public bool RootAffectsSpecialFoes;

        [Tooltip("Seconds a target is stunned (no movement and no contact damage). Bosses ignore this.")]
        [Min(0f)] public float StunDuration;

        [Tooltip("Knockback impulse in world units per second. 0 = no knockback.")]
        [Min(0f)] public float KnockbackForce;

        [Tooltip("How long the knockback overrides enemy AI.")]
        [Min(0f)] public float KnockbackDuration;

        [Tooltip("Knockback also affects Special Foes. Bosses are never knocked back.")]
        public bool KnockbackAffectsSpecialFoes;

        [Tooltip("EXPERIMENT: bosses take extra damage instead of being knocked back.")]
        public bool GuardBreak;

        [Tooltip("Seconds a hit enemy cannot deal contact damage, even while being knocked back. " +
                 "Applies to every rank, including bosses. 0 = no suppression.")]
        [Min(0f)] public float ContactDamageDisabledDuration;

        public static HitEffects None => default;
    }
}
