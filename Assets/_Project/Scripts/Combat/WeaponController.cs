using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Core;
using BattleForYourSoul.Infrastructure;
using BattleForYourSoul.Player;
using BattleForYourSoul.VFX;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Executes the currently equipped weapon's two actions. Attacks are triggered by
    /// <see cref="IWeaponActions"/> presses, never by auto-fire, and the facing direction comes from
    /// the player's movement component. Melee and line attacks use non-allocating physics queries;
    /// projectile attacks use the pooled <see cref="Projectile"/> prefab.
    ///
    /// The definition can be swapped while Samsara is active without losing upgrade modifiers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponController : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private WeaponDefinition definition;

        [Header("Wiring")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private WeaponInputReader weaponInput;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private LayerMask enemyMask;

        [Header("Hit VFX")]
        [SerializeField] private AttackVfx attackVfxPrefab;
        [SerializeField, Min(0)] private int vfxPrewarmCount = 8;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 24;
        [SerializeField, Min(1)] private int maxPoolSize = 256;

        private readonly List<Collider2D> _hits = new List<Collider2D>(32);
        private readonly List<Collider2D> _impactHits = new List<Collider2D>(32);
        private readonly HashSet<IDamageable> _damaged = new HashSet<IDamageable>();
        private ContactFilter2D _enemyFilter;

        private PrefabPool<Projectile> _pool;
        private Transform _poolRoot;
        private PrefabPool<AttackVfx> _vfxPool;
        private Transform _vfxRoot;
        private WeaponModifiers _modifiers = WeaponModifiers.Default;
        // Shared by both actions: while one attack is cooling down, the other cannot fire either.
        private float _attackCooldown;

        /// <summary>True while either attack is still on cooldown. Used to gate the touch buttons.</summary>
        public bool IsAttackOnCooldown => _attackCooldown > 0f;

        private void Awake()
        {
            _enemyFilter.useLayerMask = true;
            _enemyFilter.layerMask = enemyMask;

            _poolRoot = new GameObject("Projectiles").transform;
            _poolRoot.SetParent(transform, false);
            _pool = new PrefabPool<Projectile>(
                projectilePrefab,
                _poolRoot,
                prewarmCount,
                maxPoolSize,
                projectile => projectile.SetReleaseCallback(ReleaseProjectile));

            if (attackVfxPrefab != null)
            {
                // Effects stay where the hit happened, so this root lives in world space rather than
                // under the player; parenting it would drag every slash along with movement.
                _vfxRoot = new GameObject("Attack Vfx").transform;
                _vfxPool = new PrefabPool<AttackVfx>(
                    attackVfxPrefab,
                    _vfxRoot,
                    vfxPrewarmCount,
                    maxPoolSize,
                    vfx => vfx.SetReleaseCallback(ReleaseVfx));
            }

            if (movement == null)
            {
                movement = GetComponent<PlayerMovement>();
            }

            if (weaponInput == null)
            {
                weaponInput = GetComponent<WeaponInputReader>();
            }
        }

        private void OnEnable()
        {
            if (weaponInput != null)
            {
                weaponInput.PrimaryPressed += TryUsePrimary;
                weaponInput.SecondaryPressed += TryUseSpecial;
            }
        }

        private void OnDisable()
        {
            if (weaponInput != null)
            {
                weaponInput.PrimaryPressed -= TryUsePrimary;
                weaponInput.SecondaryPressed -= TryUseSpecial;
            }
        }

        private void OnDestroy()
        {
            if (_poolRoot != null)
            {
                Destroy(_poolRoot.gameObject);
            }

            if (_vfxRoot != null)
            {
                Destroy(_vfxRoot.gameObject);
            }
        }

        /// <summary>
        /// Swaps the active weapon. Upgrade totals live on this component, so changing the
        /// definition during Samsara keeps every already-purchased modifier.
        /// </summary>
        public void SetDefinition(WeaponDefinition value)
        {
            definition = value;
            _attackCooldown = 0f;
        }

        public void SetModifiers(WeaponModifiers modifiers)
        {
            _modifiers = modifiers;
        }

        private void Update()
        {
            if (definition == null)
            {
                return;
            }

            _attackCooldown -= Time.deltaTime;
        }

        public void TryUsePrimary()
        {
            TryUse(definition != null ? definition.PrimaryAction : null);
        }

        public void TryUseSpecial()
        {
            TryUse(definition != null ? definition.SpecialAction : null);
        }

        private void TryUse(WeaponActionDefinition action)
        {
            if (action == null || _attackCooldown > 0f)
            {
                return;
            }

            Execute(action);
            _attackCooldown = action.Cooldown / Mathf.Max(0.01f, _modifiers.FireRateMultiplier);
        }

        private void Execute(WeaponActionDefinition action)
        {
            Vector2 facing = movement != null ? movement.Facing : Vector2.right;
            Vector2 direction = IsometricUtility.ScreenDirectionToWorld(facing);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();

            Vector2 origin = transform.position;
            float range = action.Range + _modifiers.RangeFlat;
            float damage = (action.Damage + _modifiers.DamageFlat) * _modifiers.DamageMultiplier;
            Vector2 center = origin + direction * (range * 0.5f);

            switch (action.Shape)
            {
                case WeaponAttackShape.Projectile:
                    FireProjectiles(action, origin, direction, damage);
                    break;
                case WeaponAttackShape.Line:
                    HitLine(center, direction, range, action.LineWidth, damage);
                    PlayHitVfx(action, center, direction);
                    break;
                default:
                    HitArea(center, direction, range * 0.5f + 0.2f, action.ArcDegrees, damage);
                    PlayHitVfx(action, center, direction);
                    break;
            }
        }

        private void PlayHitVfx(WeaponActionDefinition action, Vector2 center, Vector2 direction)
        {
            if (_vfxPool == null || action.HitVfxSprite == null)
            {
                return;
            }

            AttackVfx vfx = _vfxPool.Get();
            vfx.Play(
                center,
                direction,
                action.HitVfxSprite,
                action.HitVfxColor,
                action.HitVfxSize,
                action.HitVfxDuration,
                action.HitVfxSweep);
        }

        private void ReleaseVfx(AttackVfx vfx)
        {
            _vfxPool.Release(vfx);
        }

        private void FireProjectiles(WeaponActionDefinition action, Vector2 origin, Vector2 direction, float damage)
        {
            if (_pool == null || action.ProjectileSprite == null)
            {
                return;
            }

            int count = Mathf.Max(1, action.ProjectilesPerShot + Mathf.RoundToInt(_modifiers.ExtraProjectiles));
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float speed = action.ProjectileSpeed * _modifiers.ProjectileSpeedMultiplier;

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1
                    ? Mathf.Lerp(-action.SpreadDegrees * 0.5f, action.SpreadDegrees * 0.5f, i / (float)(count - 1))
                    : 0f;
                float radians = (baseAngle + offset) * Mathf.Deg2Rad;
                var shotDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                Projectile projectile = _pool.Get();
                projectile.SetSprite(action.ProjectileSprite);
                projectile.Launch(
                    origin,
                    shotDirection,
                    speed,
                    damage,
                    action.ProjectileLifetime,
                    gameObject,
                    Mathf.RoundToInt(_modifiers.RicochetBounces),
                    _modifiers.ImpactRadius,
                    enemyMask);
            }
        }

        private void HitLine(Vector2 center, Vector2 direction, float range, float width, float damage)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = Physics2D.OverlapBox(center, new Vector2(range, width), angle, _enemyFilter, _hits);
            ApplyMeleeHits(count, center, direction, 360f, damage);
        }

        private void HitArea(Vector2 center, Vector2 direction, float radius, float arcDegrees, float damage)
        {
            int count = Physics2D.OverlapCircle(center, radius, _enemyFilter, _hits);
            ApplyMeleeHits(count, center, direction, arcDegrees, damage);
        }

        private void ApplyMeleeHits(int count, Vector2 center, Vector2 direction, float arcDegrees, float damage)
        {
            _damaged.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null || !hit.TryGetComponent(out IDamageable damageable) || !damageable.IsAlive)
                {
                    continue;
                }

                if (arcDegrees < 359f)
                {
                    Vector2 offset = (Vector2)hit.transform.position - center;
                    if (offset.sqrMagnitude > 0.0001f && Vector2.Angle(direction, offset) > arcDegrees * 0.5f)
                    {
                        continue;
                    }
                }

                if (!_damaged.Add(damageable))
                {
                    continue;
                }

                Vector2 hitPoint = hit.ClosestPoint(center);
                damageable.TakeDamage(new DamageInfo(damage, hitPoint, direction, gameObject));
                ApplyImpact(hitPoint, damage, damageable);
            }
        }

        private void ApplyImpact(Vector2 point, float damage, IDamageable primaryTarget)
        {
            if (_modifiers.ImpactRadius <= 0f)
            {
                return;
            }

            int count = Physics2D.OverlapCircle(point, _modifiers.ImpactRadius, _enemyFilter, _impactHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _impactHits[i];
                if (hit == null || !hit.TryGetComponent(out IDamageable damageable) || damageable == primaryTarget)
                {
                    continue;
                }

                if (!damageable.IsAlive || !_damaged.Add(damageable))
                {
                    continue;
                }

                damageable.TakeDamage(new DamageInfo(damage, hit.transform.position, Vector2.zero, gameObject));
            }
        }

        private void ReleaseProjectile(Projectile projectile)
        {
            _pool.Release(projectile);
        }
    }
}
