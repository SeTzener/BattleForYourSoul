using UnityEngine;

namespace BattleForYourSoul.World
{
    /// <summary>
    /// Derives a renderer's sorting order from its world Y instead of hard-coding a value per sprite.
    /// Lower objects on screen draw in front of higher ones, which is what a 2:1 isometric room needs.
    ///
    /// It operates on any <see cref="Renderer"/> (SpriteRenderer or a TextMesh's MeshRenderer) so props,
    /// characters and door labels all follow the same rule.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class IsometricSpriteSorter : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("Sorting-order steps per world unit. Higher values make depth changes finer.")]
        [SerializeField, Min(1)] private int unitsPerOrderStep = 100;

        [Tooltip("Small offset for overlays that must draw just in front of the object they belong to.")]
        [SerializeField] private int orderOffset;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * unitsPerOrderStep) + orderOffset;
        }
    }
}
