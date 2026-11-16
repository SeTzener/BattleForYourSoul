using UnityEngine;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// The ordered list of stages a run walks through, plus what happens after the last one.
    ///
    /// By default the final stage keeps its pacing while the monster level keeps climbing, so a run
    /// never runs out of challenge. Enabling <c>loopStages</c> restarts from the first stage instead.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Stage Progression", fileName = "StageProgression")]
    public sealed class StageProgression : ScriptableObject
    {
        [Tooltip("Stages in play order.")]
        [SerializeField] private StageDefinition[] stages;

        [Tooltip("Loop back to the first stage after the last one instead of escalating.")]
        [SerializeField] private bool loopStages;

        public int StageCount => stages != null ? stages.Length : 0;

        /// <summary>Stage parameters for the given run stage index (0-based).</summary>
        public StageDefinition GetStage(int index)
        {
            if (stages == null || stages.Length == 0)
            {
                return null;
            }

            if (index < 0)
            {
                index = 0;
            }

            if (index < stages.Length)
            {
                return stages[index];
            }

            return loopStages ? stages[index % stages.Length] : stages[stages.Length - 1];
        }

        /// <summary>Monster level for the given run stage index, escalating past the last stage.</summary>
        public int GetMonsterLevel(int index)
        {
            if (stages == null || stages.Length == 0)
            {
                return Mathf.Max(1, 1 + index);
            }

            if (index < 0)
            {
                index = 0;
            }

            if (index < stages.Length)
            {
                return stages[index].MonsterLevel;
            }

            if (loopStages)
            {
                return stages[index % stages.Length].MonsterLevel;
            }

            return stages[stages.Length - 1].MonsterLevel + (index - (stages.Length - 1));
        }
    }
}
