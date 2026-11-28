using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.Progression
{
    /// <summary>One enemy placed at a fixed position when a stage begins.</summary>
    [System.Serializable]
    public struct EnemyPlacement
    {
        [SerializeField] private EnemyDefinition enemy;
        [SerializeField] private Vector2 position;

        public EnemyDefinition Enemy => enemy;
        public Vector2 Position => position;
    }

    /// <summary>
    /// Authoring-time definition of a single stage: the monster level its enemies fight at and the
    /// exact enemies waiting on the map when the stage starts.
    ///
    /// Immutable data only. The runtime stage index lives on <see cref="LevelDirector"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Stage", fileName = "Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Stage";

        [Tooltip("Monster level enemies spawn at during this stage.")]
        [SerializeField, Min(1)] private int monsterLevel = 1;

        [Header("Encounter")]
        [Tooltip("Enemies already standing on the map when this stage starts. Clear all of them to unlock the exits.")]
        [SerializeField] private EnemyPlacement[] enemyPlacements;

        [Header("Room rules")]
        [Tooltip("Marks a superpower-selection room: no enemies, pickups are active, and the exit locks the chosen superpower.")]
        [SerializeField] private bool superpowerSelectionStage;

        [Tooltip("Indices of the shared door fixtures enabled in this room. Empty means all doors.")]
        [SerializeField] private int[] activeDoorIndices;

        public string DisplayName => displayName;
        public int MonsterLevel => monsterLevel;
        public IReadOnlyList<EnemyPlacement> EnemyPlacements => enemyPlacements;
        public bool IsSuperpowerSelectionStage => superpowerSelectionStage;
        public IReadOnlyList<int> ActiveDoorIndices => activeDoorIndices;
    }
}
