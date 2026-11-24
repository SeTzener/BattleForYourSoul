using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// Disables an on-screen attack button while the weapon's shared attack cooldown is active.
    /// Both attack buttons carry this component, so using either attack blocks both until the
    /// cooldown ends. The button is also dimmed to show that the input is unavailable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class AttackButtonCooldown : MonoBehaviour
    {
        [SerializeField] private WeaponController weapon;
        [SerializeField] private OnScreenButton button;
        [SerializeField] private Image image;
        [SerializeField] private Graphic label;
        [SerializeField, Range(0f, 1f)] private float cooldownAlpha = 0.35f;

        private Color _baseImageColor;
        private Color _baseLabelColor;
        private bool _lastBlocked;

        private void Awake()
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (button == null)
            {
                button = GetComponent<OnScreenButton>();
            }

            _baseImageColor = image != null ? image.color : Color.white;
            _baseLabelColor = label != null ? label.color : Color.white;
        }

        private void Update()
        {
            if (weapon == null)
            {
                return;
            }

            bool blocked = weapon.IsAttackOnCooldown;
            if (blocked == _lastBlocked)
            {
                return;
            }

            _lastBlocked = blocked;

            if (button != null)
            {
                button.enabled = !blocked;
            }

            if (image != null)
            {
                image.color = WithAlpha(_baseImageColor, blocked ? _baseImageColor.a * cooldownAlpha : _baseImageColor.a);
            }

            if (label != null)
            {
                label.color = WithAlpha(_baseLabelColor, blocked ? _baseLabelColor.a * cooldownAlpha : _baseLabelColor.a);
            }
        }

        private void OnDisable()
        {
            // Never leave a virtual button latched off when the mobile controls are hidden.
            if (button != null && !button.enabled)
            {
                button.enabled = true;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
