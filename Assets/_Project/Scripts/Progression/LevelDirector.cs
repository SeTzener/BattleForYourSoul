using System;
using UnityEngine;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Walks a run through the stages in a <see cref="StageProgression"/> asset. Each stage sets the
    /// monster level, the kill quota that clears it, and the spawner's pacing/roster, so the whole
    /// difficulty curve is authored as data.
    ///
    /// The monster level is deliberately independent of the player's level: it follows how far into
    /// the run the player is, so an early stage always faces the same monsters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDirector : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private MonsterScaling scaling;
        [SerializeField] private StageProgression progression;

        private int _stageIndex;
        private int _killsThisStage;

        /// <summary>Monster level enemies spawn at during the current stage.</summary>
        public int CurrentLevel => progression != null ? progression.GetMonsterLevel(_stageIndex) : Mathf.Max(1, 1 + _stageIndex);

        /// <summary>1-based stage number for display; keeps counting past the last authored stage.</summary>
        public int StageNumber => _stageIndex + 1;

        /// <summary>The stage definition currently in play.</summary>
        public StageDefinition CurrentStage => progression != null ? progression.GetStage(_stageIndex) : null;

        /// <summary>Scaling applied to monsters at the current level.</summary>
        public MonsterScaling Scaling => scaling;

        /// <summary>Kills still needed to clear the current stage.</summary>
        public int KillsRemaining
        {
            get
            {
                StageDefinition stage = CurrentStage;
                return stage != null ? Mathf.Max(0, stage.KillsToAdvance - _killsThisStage) : 0;
            }
        }

        /// <summary>Raised with the new stage number whenever the run advances.</summary>
        public event Action<int> LevelChanged;

        private void Awake()
        {
            ApplyStage();
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

        /// <summary>Advances to the next stage immediately, regardless of the quota.</summary>
        public void Advance()
        {
            _killsThisStage = 0;
            _stageIndex++;
            ApplyStage();
            LevelChanged?.Invoke(StageNumber);
        }

        private void ApplyStage()
        {
            StageDefinition stage = CurrentStage;
            if (stage != null && spawner != null)
            {
                spawner.Configure(stage);
            }
        }

        private void HandleEnemyDied(Enemy _)
        {
            _killsThisStage++;

            StageDefinition stage = CurrentStage;
            int quota = stage != null ? stage.KillsToAdvance : int.MaxValue;
            if (_killsThisStage >= quota)
            {
                Advance();
            }
        }
    }
}
