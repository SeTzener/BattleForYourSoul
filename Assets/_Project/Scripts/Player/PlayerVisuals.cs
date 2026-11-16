using UnityEngine;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Presentation only: orients the sprite from the movement facing. It never moves the body, so
    /// physics and visuals stay independent (important once animation and VFX arrive).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerDefinition definition;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private void Reset()
        {
            movement = GetComponent<PlayerMovement>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            if (movement == null)
            {
                movement = GetComponent<PlayerMovement>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (definition != null && definition.Sprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = definition.Sprite;
            }
        }

        private void LateUpdate()
        {
            if (movement == null || spriteRenderer == null)
            {
                return;
            }

            Vector2 facing = movement.Facing;
            if (facing.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // Single-sprite placeholder: mirror horizontally when moving left. A directional sprite
            // set will replace this without touching movement.
            if (Mathf.Abs(facing.x) > 0.01f)
            {
                spriteRenderer.flipX = facing.x < 0f;
            }
        }
    }
}
