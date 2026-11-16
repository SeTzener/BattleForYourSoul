using System;
using UnityEngine;
using BattleForYourSoul.Infrastructure;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// Spawns enemies around the target on a timer, capped by a live-enemy limit, using a pool so
    /// the fight does not churn garbage.
    ///
    /// It owns the pool and is the single place that counts kills, which the HUD subscribes to.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Enemy enemyPrefab;
        [Tooltip("Fallback enemy used when the current stage has no roster.")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private EnemyRegistry registry;
        [SerializeField] private LevelDirector levelDirector;
        [SerializeField] private Transform target;

        [Header("Spawning (fallbacks when no stage is configured)")]
        [Tooltip("Seconds between spawns.")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 1.2f;

        [Tooltip("Never keep more than this many enemies alive at once.")]
        [SerializeField, Min(1)] private int maxAlive = 30;

        [Tooltip("Enemies spawn on a ring of this radius around the target.")]
        [SerializeField, Min(0.1f)] private float spawnRadius = 7f;

        [Tooltip("Half-extents the spawn position is clamped to, keeping spawns inside the arena.")]
        [SerializeField] private Vector2 spawnBounds = new Vector2(7.5f, 3.8f);

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 32;
        [SerializeField, Min(1)] private int maxPoolSize = 128;

        private PrefabPool<Enemy> _pool;
        private StageDefinition _stage;
        private float _cooldown;
        private int _kills;
        private float _activeSpawnInterval;
        private int _activeMaxAlive;
        private float _activeSpawnRadius;
        private Vector2 _activeSpawnBounds;

        /// <summary>Total enemies killed this run.</summary>
        public int KillCount => _kills;

        /// <summary>Raised with the new total whenever an enemy dies.</summary>
        public event Action<int> KillCountChanged;

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

            ApplyStage();
        }

        /// <summary>
        /// Applies a stage's pacing and roster. Called by the level director on start and on every
        /// stage change; without a stage the serialized fallbacks are used.
        /// </summary>
        public void Configure(StageDefinition stage)
        {
            _stage = stage;
            ApplyStage();
        }

        private void ApplyStage()
        {
            _activeSpawnInterval = _stage != null ? _stage.SpawnInterval : spawnInterval;
            _activeMaxAlive = _stage != null ? _stage.MaxAlive : maxAlive;
            _activeSpawnRadius = _stage != null ? _stage.SpawnRadius : spawnRadius;
            _activeSpawnBounds = _stage != null ? _stage.SpawnBounds : spawnBounds;
            _cooldown = _activeSpawnInterval;
        }

        private void Update()
        {
            if (target == null || registry == null)
            {
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            _cooldown = _activeSpawnInterval;

            if (registry.Count >= _activeMaxAlive)
            {
                return;
            }

            SpawnOne();
        }

        private void SpawnOne()
        {
            EnemyDefinition spawnDefinition = PickDefinition();
            if (spawnDefinition == null)
            {
                return;
            }

            Enemy enemy = _pool.Get();
            enemy.transform.position = PickSpawnPosition();

            int level = levelDirector != null ? levelDirector.CurrentLevel : 1;
            MonsterScaling scaling = levelDirector != null ? levelDirector.Scaling : null;
            enemy.Initialize(spawnDefinition, level, scaling, target, registry);
        }

        /// <summary>Picks a definition from the stage roster by weight, or the fallback definition.</summary>
        private EnemyDefinition PickDefinition()
        {
            var roster = _stage != null ? _stage.Roster : null;
            if (roster == null || roster.Count == 0)
            {
                return definition;
            }

            float total = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Enemy != null)
                {
                    total += Mathf.Max(0f, roster[i].Weight);
                }
            }

            if (total <= 0f)
            {
                return definition;
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < roster.Count; i++)
            {
                EnemySpawnEntry entry = roster[i];
                if (entry.Enemy == null)
                {
                    continue;
                }

                roll -= Mathf.Max(0f, entry.Weight);
                if (roll <= 0f)
                {
                    return entry.Enemy;
                }
            }

            return definition;
        }

        private Vector2 PickSpawnPosition()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _activeSpawnRadius;
            Vector2 position = (Vector2)target.position + offset;

            position.x = Mathf.Clamp(position.x, -_activeSpawnBounds.x, _activeSpawnBounds.x);
            position.y = Mathf.Clamp(position.y, -_activeSpawnBounds.y, _activeSpawnBounds.y);
            return position;
        }

        private void ReleaseEnemy(Enemy enemy)
        {
            _kills++;
            KillCountChanged?.Invoke(_kills);
            EnemyDied?.Invoke(enemy);
            _pool.Release(enemy);
        }
    }
}
