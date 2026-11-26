using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Superpowers;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Scene-scoped record of upgrades picked this run. There is no static state: the director clears
    /// it when a new Samsara stage starts, because nothing is specified to survive that room yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunUpgradeState : MonoBehaviour
    {
        private readonly Dictionary<string, int> _taken = new Dictionary<string, int>();
        private readonly HashSet<string> _takenIds = new HashSet<string>();
        private readonly HashSet<string> _exclusiveGroups = new HashSet<string>();
        private readonly List<UpgradeDefinition> _all = new List<UpgradeDefinition>();

        public IReadOnlyCollection<string> TakenIds => _takenIds;
        public IReadOnlyList<UpgradeDefinition> Taken => _all;

        public int Count(UpgradeDefinition upgrade)
        {
            return upgrade != null && _taken.TryGetValue(upgrade.Id, out int count) ? count : 0;
        }

        public bool CanOffer(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return false;
            }

            if (upgrade.MaxStacks > 0 && Count(upgrade) >= upgrade.MaxStacks)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(upgrade.ExclusiveGroup) && _exclusiveGroups.Contains(upgrade.ExclusiveGroup))
            {
                return false;
            }

            return upgrade.RequiresMetBy(_takenIds);
        }

        /// <summary>
        /// Records a pick and returns false when it is no longer legal (stack cap, exclusive group or
        /// prerequisite). Re-checking here keeps direct callers from bypassing the offer filters.
        /// </summary>
        public bool MarkTaken(UpgradeDefinition upgrade)
        {
            if (!CanOffer(upgrade))
            {
                return false;
            }

            _taken[upgrade.Id] = Count(upgrade) + 1;
            _takenIds.Add(upgrade.Id);
            _all.Add(upgrade);

            if (!string.IsNullOrEmpty(upgrade.ExclusiveGroup))
            {
                _exclusiveGroups.Add(upgrade.ExclusiveGroup);
            }

            return true;
        }

        public SuperpowerActionDefinition GetPrimaryOverride(SuperpowerDefinition superpower)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                UpgradeDefinition upgrade = _all[i];
                if (upgrade.PrimaryOverride != null && upgrade.AppliesTo(superpower))
                {
                    return upgrade.PrimaryOverride;
                }
            }

            return null;
        }

        public SuperpowerActionDefinition GetSpecialOverride(SuperpowerDefinition superpower)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                UpgradeDefinition upgrade = _all[i];
                if (upgrade.SpecialOverride != null && upgrade.AppliesTo(superpower))
                {
                    return upgrade.SpecialOverride;
                }
            }

            return null;
        }

        public void Clear()
        {
            _taken.Clear();
            _takenIds.Clear();
            _exclusiveGroups.Clear();
            _all.Clear();
        }
    }
}
