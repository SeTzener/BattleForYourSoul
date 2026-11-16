using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// The set of upgrades a level-up can offer. Kept in an asset so adding or removing a choice is
    /// data, not code.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Upgrade Pool", fileName = "UpgradePool")]
    public sealed class UpgradePool : ScriptableObject
    {
        [Tooltip("Upgrades that can be offered. Null entries are ignored.")]
        [SerializeField] private UpgradeDefinition[] upgrades;

        public int Count => upgrades != null ? upgrades.Length : 0;

        public UpgradeDefinition Get(int index)
        {
            if (upgrades == null || index < 0 || index >= upgrades.Length)
            {
                return null;
            }

            return upgrades[index];
        }
    }
}
