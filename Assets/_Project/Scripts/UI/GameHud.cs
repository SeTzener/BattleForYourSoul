using UnityEngine;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Minimal run HUD: a player health bar and a kill counter. It only reflects state pushed by
    /// <see cref="Health"/> and <see cref="EnemySpawner"/> events; it never polls or searches.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text killLabel;
        [SerializeField] private Health playerHealth;
        [SerializeField] private EnemySpawner spawner;

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
    }
}
