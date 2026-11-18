using UnityEngine;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Minimal run HUD: a player health bar, an XP bar with the current level, a stage indicator and
    /// a kill counter. It only reflects state pushed by <see cref="Health"/>, <see cref="Experience"/>,
    /// <see cref="LevelDirector"/> and <see cref="EnemySpawner"/> events; it never polls or searches.
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
                HandleStageChanged(levelDirector.CurrentLevel);
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
            if (stageLabel != null)
            {
                stageLabel.text = $"Stage {stage}";
            }
        }
    }
}
