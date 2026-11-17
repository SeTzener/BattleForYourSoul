using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Shows the game-over panel and freezes the run when the player dies, and wires the panel's
    /// buttons. Listens to <see cref="Health.Died"/> so no one has to poll "is the player dead".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Health playerHealth;

        [Header("Flow")]
        [SerializeField] private string menuSceneName = "MainMenu";

        private void Awake()
        {
            // A previous run may have frozen time before reloading.
            Time.timeScale = 1f;

            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(GoToMenu);
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }
        }

        private void HandlePlayerDied(Health _)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }

            Time.timeScale = 0f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
