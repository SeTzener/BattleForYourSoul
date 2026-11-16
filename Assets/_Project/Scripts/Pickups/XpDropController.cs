using UnityEngine;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Infrastructure;
using BattleForYourSoul.Player;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.Pickups
{
    /// <summary>
    /// Drops XP gems where enemies die and banks the XP when they are collected.
    ///
    /// It listens to <see cref="EnemySpawner.EnemyDied"/> rather than searching the scene, owns its
    /// own gem pool, and is the single place that turns a kill into XP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XpDropController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private XpGem gemPrefab;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Experience experience;
        [SerializeField] private Transform target;
        [SerializeField] private PlayerStats playerStats;

        [Header("Drop")]
        [Tooltip("Base radius at which a dropped gem starts flying to the player.")]
        [SerializeField, Min(0.1f)] private float magnetRadius = 2.2f;

        [Tooltip("How fast a magnetised gem homes in (world units per second).")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 8f;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 48;
        [SerializeField, Min(1)] private int maxPoolSize = 512;

        private PrefabPool<XpGem> _pool;

        private void Awake()
        {
            _pool = new PrefabPool<XpGem>(gemPrefab, transform, prewarmCount, maxPoolSize, gem => gem.SetReleaseCallback(ReleaseGem));
        }

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.EnemyDied += HandleEnemyDied;
            }
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.EnemyDied -= HandleEnemyDied;
            }
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            float xp = enemy.XpReward;
            if (xp <= 0f)
            {
                return;
            }

            XpGem gem = _pool.Get();
            gem.transform.position = enemy.transform.position;

            float radius = playerStats != null ? magnetRadius * playerStats.PickupRadiusMultiplier : magnetRadius;
            gem.Initialize(target, xp, radius, moveSpeed, HandleCollected);
        }

        private void HandleCollected(XpGem gem)
        {
            if (experience != null)
            {
                experience.AddXp(gem.XpValue);
            }
        }

        private void ReleaseGem(XpGem gem)
        {
            _pool.Release(gem);
        }
    }
}
