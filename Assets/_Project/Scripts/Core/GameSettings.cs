using UnityEngine;

namespace BattleForYourSoul.Core
{
    /// <summary>
    /// Project-wide, authoring-time tuning for pixel-art rendering and isometric layout.
    ///
    /// This is an immutable definition asset: never write runtime state into it. Systems read it
    /// so rendering assumptions (PPU, iso ratio, sorting layers) are configured in one place
    /// instead of being scattered across components.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Game Settings", fileName = "GameSettings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Pixel Art")]
        [Tooltip("Pixels Per Unit used by every pixel-art sprite in the project.")]
        [SerializeField, Min(1)] private int pixelsPerUnit = 32;

        [Tooltip("Low internal resolution the Pixel Perfect Camera targets.")]
        [SerializeField] private Vector2Int referenceResolution = new Vector2Int(320, 180);

        [Header("Isometric")]
        [Tooltip("Tile height / tile width. 0.5 is the classic 2:1 isometric look.")]
        [SerializeField, Range(0.25f, 1f)] private float isometricRatio = 0.5f;

        [Tooltip("Isometric tile sprite size in pixels (width x height).")]
        [SerializeField] private Vector2Int tileSizeInPixels = new Vector2Int(32, 16);

        [Header("Sorting")]
        [Tooltip("Sorting layer drawn behind everything else (the floor).")]
        [SerializeField] private string groundSortingLayer = "Ground";

        [Tooltip("Sorting layer for characters and props, sorted by world Y.")]
        [SerializeField] private string entitySortingLayer = "Entities";

        [Tooltip("Sorting layer drawn in front of entities (projectiles, hit effects).")]
        [SerializeField] private string vfxSortingLayer = "VFX";

        public int PixelsPerUnit => pixelsPerUnit;
        public Vector2Int ReferenceResolution => referenceResolution;
        public float IsometricRatio => isometricRatio;
        public Vector2Int TileSizeInPixels => tileSizeInPixels;
        public string GroundSortingLayer => groundSortingLayer;
        public string EntitySortingLayer => entitySortingLayer;
        public string VfxSortingLayer => vfxSortingLayer;
    }
}
