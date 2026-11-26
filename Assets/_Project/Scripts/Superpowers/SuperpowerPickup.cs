using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// A physical superpower lying on the Samsara floor. Walking into one equips it for trial. The
    /// selection owner hides a picked-up superpower and shows it here again when another is chosen, so
    /// the pickup's transform is always its original spawn point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuperpowerPickup : MonoBehaviour
    {
        [Header("Offer")]
        [SerializeField] private SuperpowerDefinition superpower;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMesh label;
        [SerializeField] private Collider2D trigger;

        [Header("Wiring")]
        [SerializeField] private SuperpowerSelection selection;

        private Color _baseColor = Color.white;

        public SuperpowerDefinition Superpower => superpower;

        private void Awake()
        {
            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
            }
        }

        /// <summary>Makes the superpower available or shows it as the currently equipped, dormant prop.</summary>
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
