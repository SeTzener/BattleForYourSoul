using UnityEngine;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Infrastructure;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Auto-firing weapon. Every <see cref="WeaponDefinition.FireInterval"/> it asks the registry for
    /// the nearest enemy in range and fires a spread of pooled projectiles at it.
    ///
    /// It never searches the scene: the target source is a serialized reference, and projectiles
    /// come from a pool created in <c>Awake</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponController : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private WeaponDefinition definition;

        [Header("Wiring")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private EnemyRegistry registry;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 24;
        [SerializeField, Min(1)] private int maxPoolSize = 256;

        private PrefabPool<Projectile> _pool;
        private Transform _poolRoot;
        private float _cooldown;

        private float _damageFlat;
        private float _damageMultiplier = 1f;
        private float _fireRateMultiplier = 1f;
        private float _projectileSpeedMultiplier = 1f;
        private int _extraProjectiles;

        private void Awake()
        {
            _poolRoot = new GameObject("Projectiles").transform;
            _poolRoot.SetParent(transform, false);
            _pool = new PrefabPool<Projectile>(
                projectilePrefab,
                _poolRoot,
                prewarmCount,
                maxPoolSize,
                projectile => projectile.SetReleaseCallback(ReleaseProjectile));

            // Stagger the first shot slightly so many weapons do not all fire on the same frame.
            _cooldown = Random.Range(0f, 0.1f);
        }

        private void OnDestroy()
        {
            if (_poolRoot != null)
            {
                Destroy(_poolRoot.gameObject);
            }
        }

        /// <summary>
        /// Applies upgrade modifiers. Pushed by the owner's stats so the weapon itself stays unaware
        /// of how upgrades are chosen. All values are clamped to sane minimums.
        /// </summary>
        public void SetModifiers(float damageFlat, float damageMultiplier, float fireRateMultiplier, int extraProjectiles, float projectileSpeedMultiplier)
        {
            _damageFlat = damageFlat;
            _damageMultiplier = Mathf.Max(0f, damageMultiplier);
            _fireRateMultiplier = Mathf.Max(0.01f, fireRateMultiplier);
            _extraProjectiles = Mathf.Max(0, extraProjectiles);
            _projectileSpeedMultiplier = Mathf.Max(0.01f, projectileSpeedMultiplier);
        }

        private void Update()
        {
            if (definition == null || _pool == null || registry == null)
            {
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            if (!registry.TryGetNearest(transform.position, definition.TargetRange, out Transform target) || target == null)
            {
                return;
            }

            Fire(target.position);
            _cooldown = definition.FireInterval / _fireRateMultiplier;
        }

        private void Fire(Vector3 targetPosition)
        {
            Vector2 origin = transform.position;
            Vector2 aim = (Vector2)targetPosition - origin;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.right;
            }

            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, definition.ProjectilesPerShot + _extraProjectiles);
            float spread = definition.SpreadDegrees;
            float damage = (definition.Damage + _damageFlat) * _damageMultiplier;
            float speed = definition.ProjectileSpeed * _projectileSpeedMultiplier;

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1
                    ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(count - 1))
                    : 0f;

                float angle = (baseAngle + offset) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                Projectile projectile = _pool.Get();
                projectile.Launch(origin, direction, speed, damage, definition.ProjectileLifetime, gameObject);
            }
        }

        private void ReleaseProjectile(Projectile projectile)
        {
            _pool.Release(projectile);
        }
    }
}
