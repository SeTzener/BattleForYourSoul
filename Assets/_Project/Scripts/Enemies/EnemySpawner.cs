using System;
using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Infrastructure;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// Places the authored enemies for the current stage and tracks how many are left. Enemies are
    /// pooled and spawned once at fixed positions; there is no timer and no replacement wave.
    ///
    /// Clearing every placement raises <see cref="Cleared"/>, which lets the level director unlock
    /// the exits. The spawner owns the pool and the live-enemy count, but no run state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Enemy enemyPrefab;
        [Tooltip("Fallback enemy used when an authored placement has no type.")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private EnemyRegistry registry;
        [SerializeField] private Transform target;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 16;
        [SerializeField, Min(1)] private int maxPoolSize = 64;

        private readonly List<Enemy> _live = new List<Enemy>();
        private PrefabPool<Enemy> _pool;
        private int _kills;
        private int _remaining;

        /// <summary>Total enemies killed this run.</summary>
        public int KillCount => _kills;

        /// <summary>Enemies still alive in the current room.</summary>
        public int Remaining => _remaining;

        /// <summary>True when the current room has no enemies left.</summary>
        public bool IsCleared => _remaining <= 0;

        /// <summary>Raised with the new total whenever an enemy dies.</summary>
        public event Action<int> KillCountChanged;

        /// <summary>Raised with the new live count whenever it changes.</summary>
        public event Action<int> RemainingChanged;

        /// <summary>Raised once when the last enemy in the room dies.</summary>
        public event Action Cleared;

        /// <summary>Raised with a dying enemy just before it returns to the pool.</summary>
        public event Action<Enemy> EnemyDied;

        private void Awake()
        {
            _pool = new PrefabPool<Enemy>(
                enemyPrefab,
                transform,
                prewarmCount,
                maxPoolSize,
                enemy => enemy.SetReleaseCallback(ReleaseEnemy));
        }

        /// <summary>
        /// Replaces the current room with the stage's authored placements. Called by the level
        /// director on start and on every stage change. Surviving enemies are returned to the pool
        /// without counting as kills.
        /// </summary>
        public void Configure(StageDefinition stage, int level, MonsterScaling scaling)
        {
            DespawnAll();

            IReadOnlyList<EnemyPlacement> placements = stage != null ? stage.EnemyPlacements : null;
            int count = placements != null ? placements.Count : 0;
            for (int i = 0; i < count; i++)
            {
                EnemyPlacement placement = placements[i];
                EnemyDefinition enemyDefinition = placement.Enemy != null ? placement.Enemy : definition;
                Spawn(enemyDefinition, placement.Position, level, scaling);
            }

            SetRemaining(_live.Count);
        }

        private void Spawn(EnemyDefinition enemyDefinition, Vector2 position, int level, MonsterScaling scaling)
        {
            if (enemyDefinition == null || target == null || registry == null)
            {
                return;
            }

            Enemy enemy = _pool.Get();
            enemy.transform.position = new Vector3(position.x, position.y, 0f);
            enemy.Initialize(enemyDefinition, Mathf.Max(1, level), scaling, target, registry);
            _live.Add(enemy);
        }

        /// <summary>Clears the room without treating any living enemy as killed.</summary>
        public void DespawnAll()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                Enemy enemy = _live[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.Despawn();
                _pool.Release(enemy);
            }

            _live.Clear();
        }

        private void SetRemaining(int value)
        {
            int next = Mathf.Max(0, value);
            if (next == _remaining)
            {
                return;
            }

            _remaining = next;
            RemainingChanged?.Invoke(_remaining);

            if (_remaining == 0)
            {
                Cleared?.Invoke();
            }
        }

        private void ReleaseEnemy(Enemy enemy)
        {
            _live.Remove(enemy);

            _kills++;
            KillCountChanged?.Invoke(_kills);
            EnemyDied?.Invoke(enemy);

            _pool.Release(enemy);
            SetRemaining(_live.Count);
        }
    }
}
