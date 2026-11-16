using UnityEngine;

namespace BattleForYourSoul.Core
{
    /// <summary>
    /// Single home for the isometric maths so no other system hard-codes it.
    ///
    /// This project uses Unity's 2D isometric <c>Grid</c> with an axis-aligned orthographic camera.
    /// Because the camera is axis-aligned, world space already reads as screen space, so a
    /// screen-space input direction maps to a world direction without rotation. The isometric
    /// transform is therefore needed for two things: placing things on the diamond grid, and
    /// depth sorting. Both live here.
    /// </summary>
    public static class IsometricUtility
    {
        /// <summary>Classic 2:1 isometric ratio (tile height / tile width).</summary>
        public const float DefaultRatio = 0.5f;

        /// <summary>Cell (grid) coordinates to world position for a 2:1 isometric layout.</summary>
        public static Vector2 CellToWorld(Vector2 cell, float ratio = DefaultRatio)
        {
            return new Vector2((cell.x - cell.y) * 0.5f, (cell.x + cell.y) * 0.5f * ratio);
        }

        /// <summary>World position to cell (grid) coordinates. Inverse of <see cref="CellToWorld"/>.</summary>
        public static Vector2 WorldToCell(Vector2 world, float ratio = DefaultRatio)
        {
            float xMinusY = world.x * 2f;
            float xPlusY = world.y * 2f / ratio;
            return new Vector2((xMinusY + xPlusY) * 0.5f, (xPlusY - xMinusY) * 0.5f);
        }

        /// <summary>
        /// Screen-space (input) direction to world-space direction. The isometric camera is
        /// axis-aligned, so this is currently the identity; it exists so the mapping has one home
        /// if the camera is ever rotated.
        /// </summary>
        public static Vector2 ScreenDirectionToWorld(Vector2 screenDirection)
        {
            return screenDirection;
        }

        /// <summary>World-space direction to screen-space direction. Inverse of <see cref="ScreenDirectionToWorld"/>.</summary>
        public static Vector2 WorldDirectionToScreen(Vector2 worldDirection)
        {
            return worldDirection;
        }

        /// <summary>
        /// Depth key used for isometric sorting. Larger world Y is further "north" (behind), so
        /// sorting ascending draws the nearest sprites last.
        /// </summary>
        public static float DepthKey(Vector2 worldPosition)
        {
            return worldPosition.y;
        }
    }
}
