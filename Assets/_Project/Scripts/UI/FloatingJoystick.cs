using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Floating virtual stick. Touching anywhere in the left-half zone places the base at that point
    /// and feeds a normalized vector to &lt;Gamepad&gt;/leftStick, so movement reuses the existing
    /// Gamepad binding. The base and knob are hidden while idle, leaving no fixed control near the
    /// screen border.
    ///
    /// The press is read straight from Touchscreen/Mouse instead of UI pointer events: the pad must
    /// appear at the exact point the finger lands, even when no UI graphic is under it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingJoystick : OnScreenControl
    {
        [SerializeField] private RectTransform touchZone;
        [SerializeField] private RectTransform baseVisual;
        [SerializeField] private RectTransform knob;
        [SerializeField] private float movementRange = 56f;

        [InputControl(layout = "Vector2")]
        [SerializeField] private string m_ControlPath = "<Gamepad>/leftStick";

        protected override string controlPathInternal
        {
            get => m_ControlPath;
            set => m_ControlPath = value;
        }

        private int activePointerId = int.MinValue;
        private Vector2 startLocal;

        private void Update()
        {
            if (Touchscreen.current != null)
            {
                UpdateFromTouchscreen(Touchscreen.current);
                return;
            }

            if (Mouse.current != null)
                UpdateFromPointer(Mouse.current.position.ReadValue(), Mouse.current.leftButton.isPressed, -1);
        }

        private void UpdateFromTouchscreen(Touchscreen screen)
        {
            bool trackedTouchAlive = false;
            foreach (TouchControl touch in screen.touches)
            {
                if (!touch.press.isPressed)
                    continue;

                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (IsActive)
                {
                    if (id != activePointerId)
                        continue;
                    DragStick(position);
                }
                else if (IsInsideLeftHalf(position, out Vector2 local))
                {
                    StartStick(position, local, id);
                }
                else
                {
                    continue;
                }

                trackedTouchAlive = true;
                break;
            }

            if (IsActive && !trackedTouchAlive)
                ResetStick();
        }

        private void UpdateFromPointer(Vector2 screenPosition, bool pressed, int id)
        {
            if (IsActive)
            {
                if (pressed && id == activePointerId)
                    DragStick(screenPosition);
                else
                    ResetStick();
                return;
            }

            if (pressed && IsInsideLeftHalf(screenPosition, out Vector2 local))
                StartStick(screenPosition, local, id);
        }

        private bool IsActive => activePointerId != int.MinValue;

        private void StartStick(Vector2 screenPosition, Vector2 local, int id)
        {
            activePointerId = id;
            startLocal = local;

            if (baseVisual != null)
            {
                baseVisual.anchoredPosition = ClampToLeftHalf(local);
                baseVisual.gameObject.SetActive(true);
            }

            if (knob != null)
                knob.anchoredPosition = Vector2.zero;

            SendValueToControl(Vector2.zero);
        }

        private void DragStick(Vector2 screenPosition)
        {
            Vector2 current = ToLocal(screenPosition);
            Vector2 delta = Vector2.ClampMagnitude(current - startLocal, movementRange);
            if (knob != null)
                knob.anchoredPosition = delta;

            SendValueToControl(delta / movementRange);
        }

        protected override void OnDisable()
        {
            ResetStick();
            base.OnDisable();
        }

        private void ResetStick()
        {
            activePointerId = int.MinValue;
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
            if (baseVisual != null)
                baseVisual.gameObject.SetActive(false);
            SendValueToControl(Vector2.zero);
        }

        private Vector2 ToLocal(Vector2 screenPosition)
        {
            RectTransform parent = touchZone != null ? touchZone.parent as RectTransform : null;
            if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPosition, null, out Vector2 local))
                return Vector2.zero;
            return local;
        }

        private bool IsInsideLeftHalf(Vector2 screenPosition, out Vector2 local)
        {
            local = ToLocal(screenPosition);
            RectTransform parent = touchZone != null ? touchZone.parent as RectTransform : null;
            return parent != null && parent.rect.Contains(local) && local.x <= 0f;
        }

        private Vector2 ClampToLeftHalf(Vector2 local)
        {
            RectTransform parent = touchZone != null ? touchZone.parent as RectTransform : null;
            if (parent == null)
                return local;

            Rect rect = parent.rect;
            float halfWidth = baseVisual != null ? baseVisual.rect.width * 0.5f : 0f;
            float halfHeight = baseVisual != null ? baseVisual.rect.height * 0.5f : 0f;

            // The root canvas is centered on zero, so its left half ends at x = 0.
            return new Vector2(
                Mathf.Clamp(local.x, rect.xMin + halfWidth, -halfWidth),
                Mathf.Clamp(local.y, rect.yMin + halfHeight, rect.yMax - halfHeight));
        }
    }
}
