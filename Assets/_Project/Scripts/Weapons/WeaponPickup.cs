using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Weapons
{
    /// <summary>
    /// A physical weapon lying on the Samsara floor. Walking into one equips it for trial. The
    /// selection owner hides a picked-up weapon and shows it here again when another is chosen, so
    /// the pickup's transform is always its original spawn point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponPickup : MonoBehaviour
    {
        [Header("Offer")]
        [SerializeField] private WeaponDefinition weapon;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMesh label;
        [SerializeField] private Collider2D trigger;

        [Header("Wiring")]
        [SerializeField] private WeaponSelection selection;

        private Color _baseColor = Color.white;

        public WeaponDefinition Weapon => weapon;

        private void Awake()
        {
            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
            }
        }

        /// <summary>Makes the weapon available or shows it as the currently equipped, dormant prop.</summary>
        public void SetAvailable(bool available)
        {
            if (trigger != null)
            {
                trigger.enabled = available;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = available ? _baseColor : new Color32(100, 96, 116, 96);
            }

            if (label != null)
            {
                label.gameObject.SetActive(available);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && selection != null)
            {
                selection.TrySelect(this);
            }
        }
    }
}
