using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Authoring-time grouping of upgrades. Doors advertise one category each, and clearing the
    /// following stage offers a choice drawn only from that category.
    ///
    /// Categories are assets so they can be renamed, recoloured and extended without code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Upgrade Category", fileName = "UpgradeCategory")]
    public sealed class UpgradeCategoryDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Category";
        [SerializeField] private Color color = Color.white;

        [TextArea, Tooltip("Short flavour text shown when the door is unlocked.")]
        [SerializeField] private string description;

        public string DisplayName => displayName;
        public Color Color => color;
        public string Description => description;
    }
}
