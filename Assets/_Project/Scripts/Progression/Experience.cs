using System;
using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Runtime XP and level for a run. Other systems push XP in and react to <see cref="Changed"/>
    /// and <see cref="LeveledUp"/> instead of polling, so the HUD and the level-up screen stay in
    /// sync without searching the scene.
    ///
    /// Per-instance state only; the curve lives in a <see cref="ProgressionDefinition"/> asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Experience : MonoBehaviour
    {
        [SerializeField] private ProgressionDefinition progression;

        private int _level;
        private float _xp;
        private float _xpToNext;

        public int Level => _level;

        /// <summary>XP banked toward the next level.</summary>
        public float CurrentXp => _xp;

        /// <summary>XP the current level needs in total.</summary>
        public float XpForNextLevel => _xpToNext;

        /// <summary>Progress toward the next level, 0..1. Drives the XP bar.</summary>
        public float Normalized => _xpToNext > 0f ? Mathf.Clamp01(_xp / _xpToNext) : 0f;

        /// <summary>Raised once per level gained, with the new level.</summary>
        public event Action<int> LeveledUp;

        /// <summary>Raised after XP or level changes.</summary>
        public event Action<Experience> Changed;

        private void Awake()
        {
            _level = progression != null ? progression.StartingLevel : 1;
            _xpToNext = XpRequiredFor(_level);
        }

        /// <summary>Adds XP and resolves any level-ups it triggers.</summary>
        public void AddXp(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _xp += amount;

            while (_xpToNext > 0f && _xp >= _xpToNext)
            {
                _xp -= _xpToNext;
                _level++;
                _xpToNext = XpRequiredFor(_level);
                LeveledUp?.Invoke(_level);
            }

            Changed?.Invoke(this);
        }

        private float XpRequiredFor(int level)
        {
            return progression != null ? progression.XpRequiredFor(level) : Mathf.Max(1f, level * 5f);
        }
    }
}
