using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleForYourSoul.Core
{
    /// <summary>
    /// Entry point placed in the Bootstrap scene. Loads the first real scene, then stays out of
    /// the way. Kept deliberately tiny: no global singletons, no static state.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "MainMenu";

        private void Start()
        {
            if (string.IsNullOrEmpty(firstSceneName))
            {
                Debug.LogWarning("[GameBootstrap] No first scene configured.", this);
                return;
            }

            SceneManager.LoadScene(firstSceneName);
        }
    }
}
