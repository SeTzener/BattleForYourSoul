using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Presentation only. The sprite must live on a child so the visual hop (pose player) and the
    /// Hulk / Man of Steel growth never move or resize the physics body. Never write to the root
    /// transform from this component.
    ///
    /// TODO: retune the growth factor and tint when the final character sprites arrive.
    /// </summary>
    public sealed class PlayerVisuals : MonoBehaviour
    {
        private const float MaxGrowth = 0.25f;
        private const float MaxTint = 0.8f;

        private static readonly Color HulkTint = new Color32(96, 216, 104, 255);
        private static readonly Color SteelTint = new Color32(196, 208, 224, 255);

        [SerializeField] private PlayerDefinition definition;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private Health health;

        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Reset()
        {
            movement = GetComponent<PlayerMovement>();
            spriteRenderer = FindVisualRenderer();
        }

        private SpriteRenderer FindVisualRenderer()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].transform != transform)
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private void Awake()
        {
            if (movement == null)
            {
                movement = GetComponent<PlayerMovement>();
            }

            if (spriteRenderer == null || spriteRenderer.transform == transform)
            {
                spriteRenderer = FindVisualRenderer();
            }

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (definition != null && definition.Sprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = definition.Sprite;
            }

            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
                _baseScale = spriteRenderer.transform.localScale;
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (movement != null)
            {
                Vector2 facing = movement.Facing;
                if (facing.sqrMagnitude > 0.0001f && Mathf.Abs(facing.x) > 0.01f)
                {
                    // Single-sprite placeholder: mirror horizontally when moving left. A directional
                    // sprite set will replace this without touching movement.
                    spriteRenderer.flipX = facing.x < 0f;
                }
            }

            bool hulk = stats != null && stats.CurrentModifiers.Hulk;
            bool steel = !hulk && stats != null && stats.CurrentModifiers.ManOfSteel;
            float missingHealth = health != null ? 1f - health.Normalized : 0f;

            if (!hulk && !steel)
            {
                spriteRenderer.color = _baseColor;
                spriteRenderer.transform.localScale = _baseScale;
                return;
            }

            float factor = Mathf.Clamp01(missingHealth);
            spriteRenderer.transform.localScale = _baseScale * (1f + MaxGrowth * factor);
            spriteRenderer.color = Color.Lerp(
                _baseColor,
                hulk ? HulkTint : SteelTint,
                factor * MaxTint);
        }
    }
}
