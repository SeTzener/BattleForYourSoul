using System;
using UnityEngine;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Owns the current monster level (the stage) and raises <see cref="LevelChanged"/> when it
    /// advances. The monster level is deliberately independent of the player's level: it follows how
    /// far into the run the player is, so an early stage always faces the same monsters.
    ///
    /// The advance rule today is a kill quota per stage. That is a stand-in for real rooms/portals;
    /// swapping it should only touch this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDirector : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private MonsterScaling scaling;

        [Header("Stage")]
        [SerializeField, Min(1)] private int startLevel = 1;

        [Tooltip("Kills required to clear a stage and raise the monster level.")]
        [SerializeField, Min(1)] private int killsToAdvance = 12;

        private int _level;
        private int _killsThisLevel;

        /// <summary>Current monster level.</summary>
        public int CurrentLevel => _level;

        /// <summary>Scaling applied to monsters at the current level.</summary>
        public MonsterScaling Scaling => scaling;

        /// <summary>Kills still needed to clear the current stage.</summary>
        public int KillsRemaining => Mathf.Max(0, killsToAdvance - _killsThisLevel);

        /// <summary>Raised with the new monster level whenever it advances.</summary>
        public event Action<int> LevelChanged;

        private void Awake()
        {
            _level = Mathf.Max(1, startLevel);
        }

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.EnemyDied += HandleEnemyDied;
            }
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.EnemyDied -= HandleEnemyDied;
            }
        }

        /// <summary>Advances the monster level immediately, regardless of the quota.</summary>
        public void Advance()
        {
            _killsThisLevel = 0;
            _level++;
            LevelChanged?.Invoke(_level);
        }

        private void HandleEnemyDied(Enemy _)
        {
            _killsThisLevel++;
            if (_killsThisLevel >= killsToAdvance)
            {
                Advance();
            }
        }
    }
}
