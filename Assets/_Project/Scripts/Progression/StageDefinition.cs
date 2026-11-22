using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.Progression
{
    /// <summary>One enemy type a stage can spawn, with a relative spawn weight.</summary>
    [System.Serializable]
    public struct EnemySpawnEntry
    {
        [SerializeField] private EnemyDefinition enemy;

        [Tooltip("Relative chance this enemy is picked. 0 disables it.")]
        [SerializeField, Min(0f)] private float weight;

        public EnemyDefinition Enemy => enemy;
        public float Weight => weight;
    }

    /// <summary>
    /// Authoring-time definition of a single stage: the monster level it fights at, how many kills
    /// clear it, which enemies can spawn and how fast they appear.
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

        [Tooltip("Kills required to clear this stage.")]
        [SerializeField, Min(1)] private int killsToAdvance = 12;

        [Header("Roster")]
        [Tooltip("Enemy types that can spawn here, picked by weight. Empty falls back to the spawner default.")]
        [SerializeField] private EnemySpawnEntry[] roster;

        [Header("Spawning")]
        [Tooltip("Seconds between spawns.")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 1.2f;

        [Tooltip("Never keep more than this many enemies alive at once.")]
        [SerializeField, Min(1)] private int maxAlive = 30;

        [Tooltip("Enemies spawn on a ring of this radius around the target.")]
        [SerializeField, Min(0.1f)] private float spawnRadius = 7f;

        [Tooltip("Half-extents the spawn position is clamped to, keeping spawns inside the arena.")]
        [SerializeField] private Vector2 spawnBounds = new Vector2(7.5f, 3.8f);

        public string DisplayName => displayName;
        public int MonsterLevel => monsterLevel;
        public int KillsToAdvance => killsToAdvance;
        public IReadOnlyList<EnemySpawnEntry> Roster => roster;
        public float SpawnInterval => spawnInterval;
        public int MaxAlive => maxAlive;
        public float SpawnRadius => spawnRadius;
        public Vector2 SpawnBounds => spawnBounds;
    }
}
