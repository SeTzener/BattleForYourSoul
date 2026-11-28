using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Superpowers;

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

        // Appended so existing authored upgrades keep their enum values.
        RangeFlat,
        RicochetBounces,
        ImpactRadius,

        // Adds a dash charge that is always ready, independent of the normal cooldown.
        InstantDashCharges,

        // Superstrength stats. Percentages are whole numbers (20 = +20%).
        Strength,
        CriticalChancePercent,
        CriticalDamagePercent,
        BonusStunChancePercent,
        DefenseFlat,

        // Charge / Super Charge values. ChargeDamage is a damage amount, SuperCharge is seconds.
        ChargeDamage,
        ChargeKnockback,
        SuperCharge,
    }

    /// <summary>Where an upgrade may be offered. Level-up and stage-clear rewards never mix.</summary>
    public enum UpgradeSource
    {
        LevelUp = 0,
        StageClear = 1,
    }

    /// <summary>Non-stat effects of a unique superpower upgrade.</summary>
    [Flags]
    public enum SuperpowerUpgradeBehavior
    {
        None = 0,
        Charge = 1 << 0,
        SuperCharge = 1 << 1,
        PowerUp = 1 << 2,
        Hulk = 1 << 3,
        ManOfSteel = 1 << 4,
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

        [Header("Offer source")]
        [Tooltip("Level-up cards and stage-clear rewards are separate pools and never mix.")]
        [SerializeField] private UpgradeSource source = UpgradeSource.LevelUp;

        [Header("Category")]
        [Tooltip("Optional stage-clear grouping. Level-ups ignore this field.")]
        [SerializeField] private UpgradeCategoryDefinition category;

        [Header("Superpower")]
        [Tooltip("Empty = available to every superpower. Otherwise only offered while one of these superpowers is equipped.")]
        [SerializeField] private SuperpowerDefinition[] superpowerFilter;

        [Header("Availability")]
        [Tooltip("0 = unlimited stacking. 1 = unique per run.")]
        [SerializeField, Min(0)] private int maxStacks;

        [Tooltip("Upgrades in the same non-empty group are mutually exclusive.")]
        [SerializeField] private string exclusiveGroup = "";

        [Tooltip("These upgrades must be taken before this one can be offered.")]
        [SerializeField] private UpgradeDefinition[] requires;

        [Header("Effect")]
        [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();

        [SerializeField] private SuperpowerUpgradeBehavior behavior;

        [Tooltip("Replaces the primary action while this superpower is equipped.")]
        [SerializeField] private SuperpowerActionDefinition primaryOverride;

        [Tooltip("Replaces the special action while this superpower is equipped.")]
        [SerializeField] private SuperpowerActionDefinition specialOverride;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Color => color;
        public UpgradeSource Source => source;
        public UpgradeCategoryDefinition Category => category;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;
        public int MaxStacks => Mathf.Max(0, maxStacks);
        public string ExclusiveGroup => exclusiveGroup;
        public SuperpowerUpgradeBehavior Behavior => behavior;
        public SuperpowerActionDefinition PrimaryOverride => primaryOverride;
        public SuperpowerActionDefinition SpecialOverride => specialOverride;

        public bool AppliesTo(SuperpowerDefinition superpower)
        {
            if (superpowerFilter == null || superpowerFilter.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < superpowerFilter.Length; i++)
            {
                if (superpowerFilter[i] == superpower)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RequiresMetBy(IReadOnlyCollection<string> takenIds)
        {
            if (requires == null)
            {
                return true;
            }

            for (int i = 0; i < requires.Length; i++)
            {
                if (requires[i] != null && !takenIds.Contains(requires[i].Id))
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasOverrideFor(SuperpowerDefinition superpower)
        {
            if (superpowerFilter == null || superpowerFilter.Length == 0)
            {
                return primaryOverride != null || specialOverride != null;
            }

            for (int i = 0; i < superpowerFilter.Length; i++)
            {
                if (superpowerFilter[i] == superpower)
                {
                    return primaryOverride != null || specialOverride != null;
                }
            }

            return false;
        }
    }
}
