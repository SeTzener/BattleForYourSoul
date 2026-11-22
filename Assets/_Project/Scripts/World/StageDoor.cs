using UnityEngine;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.World
{
    /// <summary>
    /// One exit at the far edge of the room. Doors are always visible but stay locked until the
    /// encounter is cleared; unlocking swaps the art to the category they offer and shows its label.
    ///
    /// Walking an unlocked door into the player trigger selects that category and advances the run.
    /// It holds no run state itself - the <see cref="LevelDirector"/> owns that.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageDoor : MonoBehaviour
    {
        [Header("Offer")]
        [SerializeField] private UpgradeCategoryDefinition category;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMesh label;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite openSprite;

        [Header("Wiring")]
        [SerializeField] private LevelDirector levelDirector;

        private bool _unlocked;
        private bool _used;

        public UpgradeCategoryDefinition Category => category;

        public void SetUnlocked(bool unlocked)
        {
            _unlocked = unlocked;
            _used = false;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = unlocked ? openSprite : lockedSprite;
                spriteRenderer.color = unlocked && category != null
                    ? category.Color
                    : new Color32(118, 116, 128, 255);
            }

            if (label != null)
            {
                label.gameObject.SetActive(unlocked);
                if (unlocked && category != null)
                {
                    label.text = category.DisplayName.ToUpperInvariant();
                    label.color = category.Color;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_unlocked || _used || levelDirector == null || !other.CompareTag("Player"))
            {
                return;
            }

            _used = true;
            levelDirector.SelectExit(this);
        }
    }
}
