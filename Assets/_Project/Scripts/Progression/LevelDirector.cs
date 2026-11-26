using System;
using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Core;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Player;
using BattleForYourSoul.UI;
using BattleForYourSoul.Superpowers;
using BattleForYourSoul.World;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Walks a run through the stages in a <see cref="StageProgression"/> asset. Each stage defines
    /// the monster level and the enemies already standing in the room. The director places them,
    /// watches for the room to be cleared, unlocks the exits, and advances only when the player
    /// physically walks into one.
    ///
    /// The door also carries a reward category. That category is queued as a filtered upgrade choice
    /// when the *following* stage is cleared, so the exit the player picks shapes the next reward.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDirector : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private MonsterScaling scaling;
        [SerializeField] private StageProgression progression;
        [SerializeField] private GameSettings settings;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private LevelUpController levelUpController;
        [SerializeField] private Experience experience;
        [SerializeField] private SuperpowerSelection superpowerSelection;
        [SerializeField] private RunUpgradeState runUpgrades;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private StageDoor[] doors;

        private int _stageIndex;
        private bool _cleared;
        private UpgradeCategoryDefinition _pendingCategory;

        /// <summary>Monster level enemies fight at during the current stage.</summary>
        public int CurrentLevel => progression != null ? progression.GetMonsterLevel(_stageIndex) : Mathf.Max(1, 1 + _stageIndex);

        /// <summary>1-based stage number for display; keeps counting past the last authored stage.</summary>
        public int StageNumber => _stageIndex + 1;

        /// <summary>The stage definition currently in play.</summary>
        public StageDefinition CurrentStage => progression != null ? progression.GetStage(_stageIndex) : null;

        /// <summary>Scaling applied to monsters at the current level.</summary>
        public MonsterScaling Scaling => scaling;

        /// <summary>Enemies still alive in the current room.</summary>
        public int EnemiesRemaining => spawner != null ? spawner.Remaining : 0;

        /// <summary>Whether every enemy is dead and the exits are unlocked.</summary>
        public bool IsCleared => _cleared;

        /// <summary>Category whose filtered choice is queued when this stage is cleared.</summary>
        public UpgradeCategoryDefinition PendingRewardCategory => _pendingCategory;

        /// <summary>Whether the current room is Samsara, the superpower-selection room.</summary>
        public bool IsSuperpowerSelectionStage => CurrentStage != null && CurrentStage.IsSuperpowerSelectionStage;

        /// <summary>Raised with the new stage number whenever the run advances.</summary>
        public event Action<int> LevelChanged;

        /// <summary>Raised with the new live-enemy count.</summary>
        public event Action<int> RemainingChanged;

        /// <summary>Raised with true when the room is cleared and false when a new stage starts.</summary>
        public event Action<bool> ClearedChanged;

        /// <summary>Raised when the door-picked reward category changes.</summary>
        public event Action<UpgradeCategoryDefinition> PendingCategoryChanged;

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.RemainingChanged += HandleRemainingChanged;
                spawner.Cleared += HandleCleared;
            }

            if (superpowerSelection != null)
            {
                superpowerSelection.SelectionChanged += HandleSuperpowerSelected;
            }
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.RemainingChanged -= HandleRemainingChanged;
                spawner.Cleared -= HandleCleared;
            }

            if (superpowerSelection != null)
            {
                superpowerSelection.SelectionChanged -= HandleSuperpowerSelected;
            }
        }

        private void Start()
        {
            // Move the player before placing enemies so stationed guards measure aggro from the
            // entrance, not from wherever the generated scene happened to put the player.
            ResetPlayerPosition();
            ApplyStage();

            // Initial state for listeners that subscribed before this component started.
            LevelChanged?.Invoke(StageNumber);
            HandleRemainingChanged(EnemiesRemaining);
            ClearedChanged?.Invoke(_cleared);
            PendingCategoryChanged?.Invoke(_pendingCategory);
        }

        /// <summary>
        /// Called by a door trigger once the room is clear. In Samsara the exit also commits the
        /// superpower choice for the rest of the run, and stays locked until one has been equipped.
        /// </summary>
        public void SelectExit(StageDoor door)
        {
            if (!_cleared || door == null)
            {
                return;
            }

            bool lockingSuperpower = IsSuperpowerSelectionStage && superpowerSelection != null;
            if (lockingSuperpower && superpowerSelection.SelectedSuperpower == null)
            {
                return;
            }

            if (lockingSuperpower)
            {
                superpowerSelection.Lock();
            }

            _pendingCategory = door.Category;
            PendingCategoryChanged?.Invoke(_pendingCategory);
            Advance();
        }

        /// <summary>Advances to the next stage, regardless of whether the room was cleared.</summary>
        public void Advance()
        {
            _stageIndex++;
            ResetPlayerPosition();
            ApplyStage();
            LevelChanged?.Invoke(StageNumber);
        }

        private void ApplyStage()
        {
            _cleared = false;

            // Samsara initialises a fresh run with base stats, base health/XP, an empty upgrade record
            // and no queued reward. It is only the opening stage, but keeping this reset here makes a
            // designer-authored return to the selection room deterministic too. Clear the selection
            // first so PlayerStats sees a null definition for every subsequent Push.
            if (IsSuperpowerSelectionStage)
            {
                if (_pendingCategory != null)
                {
                    _pendingCategory = null;
                    PendingCategoryChanged?.Invoke(null);
                }

                levelUpController?.ResetForNewRun();
                experience?.ResetForNewRun();
                superpowerSelection?.Reset();
                runUpgrades?.Clear();
                playerStats?.ResetUpgrades();
            }

            ApplyDoorAvailability();
            SetDoorsUnlocked(false);
            if (superpowerSelection != null)
            {
                superpowerSelection.SetPickupsVisible(IsSuperpowerSelectionStage);
            }

            ClearedChanged?.Invoke(false);

            if (spawner != null)
            {
                spawner.Configure(CurrentStage, CurrentLevel, scaling);
            }

            // An empty room is already clear; the spawner cannot raise Cleared in that case.
            if (EnemiesRemaining <= 0)
            {
                HandleCleared();
            }
        }

        private void HandleRemainingChanged(int remaining)
        {
            RemainingChanged?.Invoke(remaining);
        }

        private void HandleCleared()
        {
            if (_cleared)
            {
                return;
            }

            _cleared = true;
            UpdateDoorUnlocks();
            ClearedChanged?.Invoke(true);

            if (!IsSuperpowerSelectionStage && _pendingCategory != null && levelUpController != null)
            {
                levelUpController.QueueCategoryReward(_pendingCategory);
            }
        }

        private void HandleSuperpowerSelected(SuperpowerDefinition _)
        {
            if (_cleared)
            {
                UpdateDoorUnlocks();
            }
        }

        /// <summary>
        /// An empty Samsara room is clear immediately, but its exit only opens once the player has
        /// equipped a superpower. Every other room opens its enabled exits as soon as it is cleared.
        /// </summary>
        private void UpdateDoorUnlocks()
        {
            bool hasSuperpower = superpowerSelection == null || superpowerSelection.SelectedSuperpower != null;
            SetDoorsUnlocked(_cleared && (!IsSuperpowerSelectionStage || hasSuperpower));
        }

        private void ApplyDoorAvailability()
        {
            if (doors == null)
            {
                return;
            }

            IReadOnlyList<int> active = CurrentStage != null ? CurrentStage.ActiveDoorIndices : null;
            bool allDoors = active == null || active.Count == 0;

            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null)
                {
                    continue;
                }

                bool enabled = allDoors;
                if (!allDoors)
                {
                    for (int j = 0; j < active.Count; j++)
                    {
                        if (active[j] == i)
                        {
                            enabled = true;
                            break;
                        }
                    }
                }

                doors[i].gameObject.SetActive(enabled);
            }
        }

        private void SetDoorsUnlocked(bool unlocked)
        {
            if (doors == null)
            {
                return;
            }

            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] != null)
                {
                    doors[i].SetUnlocked(unlocked);
                }
            }
        }

        private void ResetPlayerPosition()
        {
            Vector2 entrance = settings != null ? settings.RoomEntrance : new Vector2(0f, -3f);
            if (playerMovement != null)
            {
                playerMovement.TeleportTo(entrance);
            }

            if (cameraFollow != null)
            {
                cameraFollow.SnapToTarget();
            }
        }
    }
}
