using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Immutable description of a single hit. Passed by readonly reference so combat can carry
    /// more context (knockback direction, damage source) later without allocating per hit.
    /// </summary>
    public readonly struct DamageInfo
    {
        public DamageInfo(float amount, Vector2 sourcePosition, Vector2 direction, GameObject source = null)
        {
            Amount = amount;
            SourcePosition = sourcePosition;
            Direction = direction;
            Source = source;
        }

        /// <summary>Hit points to remove.</summary>
        public float Amount { get; }

        /// <summary>World position the hit came from (used for knockback / VFX).</summary>
        public Vector2 SourcePosition { get; }

        /// <summary>Normalised direction the hit travels in, from source to target.</summary>
        public Vector2 Direction { get; }

        /// <summary>The GameObject that caused the hit, if known.</summary>
        public GameObject Source { get; }
    }
}
