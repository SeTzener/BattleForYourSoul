using UnityEngine;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Shows the on-screen controls only on touch-first platforms. The controls are authored inactive
    /// so they never create a virtual gamepad in desktop Play sessions; this component enables them
    /// on Android/iOS before gameplay input readers wake up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileControlsVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject controlsRoot;

        /// <summary>Whether the authored controls are currently active.</summary>
        public bool IsVisible => controlsRoot != null && controlsRoot.activeSelf;

        private void Awake()
        {
            if (controlsRoot == null)
            {
                Debug.LogError("[MobileControlsVisibility] No controls root assigned.", this);
                return;
            }

            controlsRoot.SetActive(Application.isMobilePlatform);
        }
    }
}
