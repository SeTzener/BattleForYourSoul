using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.UI
{
    /// <summary>
    /// One level-up card. It only knows how to display an <see cref="UpgradeDefinition"/> and report
    /// clicks; the <see cref="LevelUpController"/> decides which upgrade it shows and what a pick
    /// does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text descriptionLabel;

        private UnityAction _handler;

        /// <summary>Shows an upgrade and routes clicks to <paramref name="onClick"/>.</summary>
        public void Bind(UpgradeDefinition upgrade, UnityAction onClick)
        {
            gameObject.SetActive(true);

            if (upgrade != null)
            {
                if (icon != null)
                {
                    icon.sprite = upgrade.Icon;
                    icon.color = upgrade.Color;
                    icon.enabled = upgrade.Icon != null;
                }

                if (titleLabel != null)
                {
                    titleLabel.text = upgrade.DisplayName;
                }

                if (descriptionLabel != null)
                {
                    descriptionLabel.text = upgrade.Description;
                }
            }

            if (button == null)
            {
                return;
            }

            if (_handler != null)
            {
                button.onClick.RemoveListener(_handler);
            }

            _handler = onClick;
            if (_handler != null)
            {
                button.onClick.AddListener(_handler);
            }
        }

        /// <summary>Detaches the click handler and hides the card.</summary>
        public void Clear()
        {
            if (button != null && _handler != null)
            {
                button.onClick.RemoveListener(_handler);
            }

            _handler = null;
            gameObject.SetActive(false);
        }
    }
}
