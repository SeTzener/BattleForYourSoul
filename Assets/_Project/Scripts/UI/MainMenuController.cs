using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Main menu behaviour. Kept free of scene-wiring details so the button can be hooked up in the
    /// Inspector, and so a touch-friendly UI can reuse it later.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Game";

        public void PlayGame()
        {
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogWarning("[MainMenuController] No game scene configured.", this);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
