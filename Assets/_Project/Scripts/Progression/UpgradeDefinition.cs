using System.Collections.Generic;
using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Which player stat an upgrade touches. Flat stats add directly; percent stats add a
    /// percentage (e.g. 12 means +12%). The stat kind decides how <see cref="StatModifier.Value"/>
    /// is interpreted, so designers cannot mix the two up.
    /// </summary>
    public enum UpgradeStat
    {
        DamageFlat,
        DamagePercent,
        FireRatePercent,
        MoveSpeedPercent,
        ProjectileSpeedPercent,
        MaxHealthFlat,
        ExtraProjectiles,
    }

    /// <summary>One additive modifier inside an upgrade, applied on top of everything before it.</summary>
    [System.Serializable]
    public struct StatModifier
    {
        [SerializeField] private UpgradeStat stat;
        [SerializeField] private float value;

        public StatModifier(UpgradeStat stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }

        public UpgradeStat Stat => stat;
        public float Value => value;
    }

    /// <summary>
    /// Authoring-time definition of a level-up choice. Immutable data only: the running totals live
    /// on the player's runtime stats, so one asset can be offered many times and stack.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Upgrade", fileName = "Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "upgrade";
        [SerializeField] private string displayName = "Upgrade";
        [SerializeField, TextArea] private string description = "";

        [Tooltip("Optional icon shown on the level-up card.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Tint applied to the card icon.")]
        [SerializeField] private Color color = Color.white;

        [Header("Effect")]
        [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Color => color;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;
    }
}
