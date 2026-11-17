using System;
using UnityEngine;
using BattleForYourSoul.Infrastructure;

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
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private EnemyRegistry registry;
        [SerializeField] private Transform target;

        [Header("Spawning")]
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
        private float _cooldown;
        private int _kills;

        /// <summary>Total enemies killed this run.</summary>
        public int KillCount => _kills;

        /// <summary>Raised with the new total whenever an enemy dies.</summary>
        public event Action<int> KillCountChanged;

        private void Awake()
        {
            _pool = new PrefabPool<Enemy>(
                enemyPrefab,
                transform,
                prewarmCount,
                maxPoolSize,
                enemy => enemy.SetReleaseCallback(ReleaseEnemy));

            _cooldown = spawnInterval;
        }

        private void Update()
        {
            if (target == null || registry == null || definition == null)
            {
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            _cooldown = spawnInterval;

            if (registry.Count >= maxAlive)
            {
                return;
            }

            SpawnOne();
        }

        private void SpawnOne()
        {
            Enemy enemy = _pool.Get();
            enemy.transform.position = PickSpawnPosition();
            enemy.Initialize(definition, target, registry);
        }

        private Vector2 PickSpawnPosition()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            Vector2 position = (Vector2)target.position + offset;

            position.x = Mathf.Clamp(position.x, -spawnBounds.x, spawnBounds.x);
            position.y = Mathf.Clamp(position.y, -spawnBounds.y, spawnBounds.y);
            return position;
        }

        private void ReleaseEnemy(Enemy enemy)
        {
            _kills++;
            KillCountChanged?.Invoke(_kills);
            _pool.Release(enemy);
        }
    }
}
