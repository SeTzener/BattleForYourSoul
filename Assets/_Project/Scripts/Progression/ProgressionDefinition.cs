using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Authoring-time level curve. Keeps "how much XP is the next level" out of code so the pace of
    /// a run is tuned in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Progression", fileName = "Progression")]
    public sealed class ProgressionDefinition : ScriptableObject
    {
        [Tooltip("Level the player starts a run at.")]
        [SerializeField, Min(1)] private int startingLevel = 1;

        [Tooltip("XP required to go from the starting level to the next one.")]
        [SerializeField, Min(1f)] private float baseXp = 5f;

        [Tooltip("Each level costs this multiple of the previous one.")]
        [SerializeField, Range(1f, 3f)] private float growth = 1.32f;

        public int StartingLevel => startingLevel;

        /// <summary>XP required to advance from <paramref name="level"/> to the next one.</summary>
        public float XpRequiredFor(int level)
        {
            int steps = Mathf.Max(0, level - startingLevel);
            return Mathf.Max(1f, Mathf.Round(baseXp * Mathf.Pow(growth, steps)));
        }
    }
}
