using UnityEngine;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Keeps UI inside the display's safe area. On iOS in landscape the notch / Dynamic Island and
    /// the rounded corners cover part of the screen, so edge-anchored HUD elements must be inset.
    /// On displays without cutouts Screen.safeArea covers the whole screen and nothing moves.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // Orientation changes and cutouts can appear/disappear at runtime, so re-check cheaply.
            Apply();
        }

        private void Apply()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (screenSize == appliedScreenSize && safeArea == appliedSafeArea)
                return;

            appliedScreenSize = screenSize;
            appliedSafeArea = safeArea;

            if (screenSize.x <= 0 || screenSize.y <= 0)
                return;

            // Normalized anchors map the safe rectangle onto the canvas root rect, which is what
            // CanvasScaler uses for ScaleWithScreenSize.
            rectTransform.anchorMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
            rectTransform.anchorMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
