namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Anything that can receive damage. Kept as an interface so superpowers and projectiles do not
    /// need to know whether the target is the player, an enemy or a destructible prop.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>True while this target can still take damage.</summary>
        bool IsAlive { get; }

        /// <summary>Applies a single hit. Implementations ignore hits once dead.</summary>
        void TakeDamage(in DamageInfo damage);
    }
}
