using UnityEngine;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Progression;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Minimal run HUD: health, XP, stage, kills and a room objective. It only reflects state pushed
    /// by <see cref="Health"/>, <see cref="Experience"/>, <see cref="EnemySpawner"/> and
    /// <see cref="LevelDirector"/> events; it never polls or searches.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text killLabel;
        [SerializeField] private Health playerHealth;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Experience experience;
        [SerializeField] private Image xpFill;
        [SerializeField] private Text levelLabel;
        [SerializeField] private LevelDirector levelDirector;
        [SerializeField] private Text stageLabel;
        [SerializeField] private Text objectiveLabel;
        [SerializeField] private Text weaponLabel;
        [SerializeField] private WeaponSelection weaponSelection;

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed += HandleHealthChanged;
                HandleHealthChanged(playerHealth);
            }

            if (spawner != null)
            {
                spawner.KillCountChanged += HandleKillsChanged;
                HandleKillsChanged(spawner.KillCount);
            }

            if (experience != null)
            {
                experience.Changed += HandleExperienceChanged;
                HandleExperienceChanged(experience);
            }

            if (levelDirector != null)
            {
                levelDirector.LevelChanged += HandleStageChanged;
                levelDirector.RemainingChanged += HandleObjectiveChanged;
                levelDirector.ClearedChanged += HandleObjectiveChanged;
                levelDirector.PendingCategoryChanged += HandleObjectiveChanged;

                HandleStageChanged(levelDirector.StageNumber);
                HandleObjectiveChanged();
            }

            if (weaponSelection != null)
            {
                weaponSelection.SelectionChanged += HandleWeaponChanged;
                weaponSelection.WeaponLocked += HandleWeaponChanged;
                HandleWeaponChanged(weaponSelection.SelectedWeapon);
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed -= HandleHealthChanged;
            }

            if (spawner != null)
            {
                spawner.KillCountChanged -= HandleKillsChanged;
            }

            if (experience != null)
            {
                experience.Changed -= HandleExperienceChanged;
            }

            if (levelDirector != null)
            {
                levelDirector.LevelChanged -= HandleStageChanged;
                levelDirector.RemainingChanged -= HandleObjectiveChanged;
                levelDirector.ClearedChanged -= HandleObjectiveChanged;
                levelDirector.PendingCategoryChanged -= HandleObjectiveChanged;
            }

            if (weaponSelection != null)
            {
                weaponSelection.SelectionChanged -= HandleWeaponChanged;
                weaponSelection.WeaponLocked -= HandleWeaponChanged;
            }
        }

        private void HandleHealthChanged(Health health)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = health.Normalized;
            }
        }

        private void HandleKillsChanged(int kills)
        {
            if (killLabel != null)
            {
                killLabel.text = $"Kills: {kills}";
            }
        }

        private void HandleExperienceChanged(Experience value)
        {
            if (xpFill != null)
            {
                xpFill.fillAmount = value.Normalized;
            }

            if (levelLabel != null)
            {
                levelLabel.text = $"Lv {value.Level}";
            }
        }

        private void HandleStageChanged(int stage)
        {
            if (stageLabel == null)
            {
                return;
            }

            StageDefinition current = levelDirector != null ? levelDirector.CurrentStage : null;
            stageLabel.text = current != null ? current.DisplayName : $"Stage {stage}";
        }

        private void HandleWeaponChanged(WeaponDefinition weapon)
        {
            if (weaponLabel == null)
            {
                return;
            }

            string name = weapon != null ? weapon.DisplayName : "none";
            string suffix = weaponSelection != null && weaponSelection.IsLocked ? " (locked)" : "";
            weaponLabel.text = $"Weapon: {name}{suffix}";
            HandleObjectiveChanged();
        }

        private void HandleObjectiveChanged()
        {
            if (objectiveLabel == null || levelDirector == null)
            {
                return;
            }

            UpgradeCategoryDefinition pending = levelDirector.PendingRewardCategory;
            string reward = pending != null ? pending.DisplayName : "none";

            if (levelDirector.IsWeaponSelectionStage)
            {
                bool hasWeapon = weaponSelection != null && weaponSelection.SelectedWeapon != null;
                objectiveLabel.text = !hasWeapon
                    ? "Pick up a weapon to begin - clear reward: " + reward
                    : $"Take the exit to lock {weaponSelection.SelectedWeapon.DisplayName} - clear reward: {reward}";
                return;
            }

            objectiveLabel.text = levelDirector.IsCleared
                ? $"Exits open - choose a door (clear reward: {reward})"
                : $"Enemies: {levelDirector.EnemiesRemaining} - clear reward: {reward}";
        }

        private void HandleObjectiveChanged(int _)
        {
            HandleObjectiveChanged();
        }

        private void HandleObjectiveChanged(bool _)
        {
            HandleObjectiveChanged();
        }

        private void HandleObjectiveChanged(UpgradeCategoryDefinition _)
        {
            HandleObjectiveChanged();
        }
    }
}
