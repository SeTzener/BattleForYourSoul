using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Turns a monster level into stat and XP multipliers. This is the single place that says how
    /// much stronger (and more rewarding) a stage's monsters are, so tuning the difficulty curve is
    /// one asset, not scattered constants.
    ///
    /// Multipliers are expressed as "growth per level above 1": level 1 is always x1, and each level
    /// past it adds the configured fraction of the base value.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Monster Scaling", fileName = "MonsterScaling")]
    public sealed class MonsterScaling : ScriptableObject
    {
        [Header("Enemy stats (growth per level above 1)")]
        [Tooltip("Fraction of base health added per monster level.")]
        [SerializeField, Min(0f)] private float healthGrowthPerLevel = 0.4f;

        [Tooltip("Fraction of base contact damage added per monster level.")]
        [SerializeField, Min(0f)] private float damageGrowthPerLevel = 0.25f;

        [Tooltip("Fraction of base move speed added per monster level.")]
        [SerializeField, Min(0f)] private float speedGrowthPerLevel = 0.03f;

        [Header("Reward")]
        [Tooltip("Fraction of base XP added per monster level.")]
        [SerializeField, Min(0f)] private float xpGrowthPerLevel = 1f;

        public float HealthMultiplier(int level) => 1f + healthGrowthPerLevel * Mathf.Max(0, level - 1);
        public float DamageMultiplier(int level) => 1f + damageGrowthPerLevel * Mathf.Max(0, level - 1);
        public float SpeedMultiplier(int level) => 1f + speedGrowthPerLevel * Mathf.Max(0, level - 1);
        public float XpMultiplier(int level) => 1f + xpGrowthPerLevel * Mathf.Max(0, level - 1);
    }
}
