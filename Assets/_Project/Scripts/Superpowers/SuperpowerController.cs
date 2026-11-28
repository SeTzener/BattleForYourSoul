using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Core;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Infrastructure;
using BattleForYourSoul.Player;
using BattleForYourSoul.VFX;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// Executes the currently equipped superpower's two actions. Attacks are triggered by
    /// <see cref="ISuperpowerActions"/> presses, never by auto-fire, and the facing direction comes
    /// from the player's movement component. Melee and line attacks use non-allocating physics
    /// queries; projectile attacks use the pooled <see cref="Projectile"/> prefab.
    ///
    /// Cooldowns are authored to match the placeholder animations. Multi-hit actions roll damage,
    /// crit and on-hit effects separately for every frame, and clear their hit set between frames so
    /// the same foe can be hit more than once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuperpowerController : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private SuperpowerDefinition definition;

        [Header("Wiring")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private SuperpowerInputReader superpowerInput;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private AttackMovement attackMove;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private SuperpowerPosePlayer posePlayer;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private Health health;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private EnemyRegistry enemyRegistry;

        [Header("Hit VFX")]
        [SerializeField] private AttackVfx attackVfxPrefab;
        [SerializeField, Min(0)] private int vfxPrewarmCount = 8;

        [Header("Pool")]
        [SerializeField, Min(0)] private int prewarmCount = 24;
        [SerializeField, Min(1)] private int maxPoolSize = 256;

        private readonly List<Collider2D> _hits = new List<Collider2D>(32);
        private readonly List<Collider2D> _impactHits = new List<Collider2D>(32);
        private readonly HashSet<IDamageable> _damaged = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _persistentHits = new HashSet<IDamageable>();
        private ContactFilter2D _enemyFilter;

        private PrefabPool<Projectile> _pool;
        private Transform _poolRoot;
        private PrefabPool<AttackVfx> _vfxPool;
        private Transform _vfxRoot;
        private SuperpowerModifiers _modifiers = SuperpowerModifiers.Default;
        private Coroutine _attackRoutine;
        private SuperpowerActionDefinition _primaryAction;
        private SuperpowerActionDefinition _specialAction;
        private bool _attackStepActive;
        private bool _sweeping;

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

            if (superpowerInput == null)
            {
                superpowerInput = GetComponent<SuperpowerInputReader>();
            }

            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }

            if (posePlayer == null)
            {
                posePlayer = GetComponent<SuperpowerPosePlayer>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (attackMove == null)
            {
                attackMove = GetComponent<AttackMovement>();
            }

            // A stage teleport cancels the body's attack step; cancel the attack too so it cannot
            // apply hits in the new room or leave its immunity/pass-through leases open.
            if (movement != null)
            {
                movement.Teleported += CancelAttack;
            }

            RefreshActions(definition);
        }

        private void OnEnable()
        {
            if (superpowerInput != null)
            {
                superpowerInput.PrimaryPressed += TryUsePrimary;
                superpowerInput.SecondaryPressed += TryUseSpecial;
            }
        }

        private void OnDisable()
        {
            if (superpowerInput != null)
            {
                superpowerInput.PrimaryPressed -= TryUsePrimary;
                superpowerInput.SecondaryPressed -= TryUseSpecial;
            }

            CancelAttack();
        }

        private void OnDestroy()
        {
            if (movement != null)
            {
                movement.Teleported -= CancelAttack;
            }

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
        /// Swaps the active superpower. Upgrade totals live on this component, so changing the
        /// definition during Samsara keeps every already-purchased modifier for that room. Action
        /// replacements from upgrades are re-evaluated here.
        /// </summary>
        public void SetDefinition(SuperpowerDefinition value)
        {
            definition = value;
            _attackCooldown = 0f;
            CancelAttack();
            RefreshActions(value);
        }

        /// <summary>Rebuilds the effective actions from the definition plus purchased overrides.</summary>
        public void RefreshActions(SuperpowerDefinition value)
        {
            if (value == null)
            {
                _primaryAction = null;
                _specialAction = null;
                return;
            }

            _primaryAction = _modifiers.PrimaryOverride != null ? _modifiers.PrimaryOverride : value.PrimaryAction;
            _specialAction = _modifiers.SpecialOverride != null ? _modifiers.SpecialOverride : value.SpecialAction;
        }

        public void SetModifiers(SuperpowerModifiers modifiers)
        {
            _modifiers = modifiers;
            RefreshActions(definition);
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
            TryUse(_primaryAction);
        }

        public void TryUseSpecial()
        {
            TryUse(_specialAction);
        }

        private void TryUse(SuperpowerActionDefinition action)
        {
            if (action == null || _attackCooldown > 0f || Time.timeScale <= 0f)
            {
                return;
            }

            // One attack at a time. Cancelling restores the previous pose and releases every lease
            // before the next attack captures its own direction and body step.
            CancelAttack();
            _attackCooldown = action.Cooldown / Mathf.Max(0.01f, _modifiers.FireRateMultiplier);
            _attackRoutine = StartCoroutine(AttackRoutine(action));
        }

        private void CancelAttack()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            FinishAttackStep();

            if (attackMove != null)
            {
                attackMove.Cancel();
            }

            if (posePlayer != null)
            {
                posePlayer.Stop();
            }
        }

        /// <summary>
        /// The whole attack lifecycle. Nothing here writes the player's transform: the body is
        /// moved only by <see cref="AttackMovement"/>, and sprites only by <see cref="SuperpowerPosePlayer"/>.
        /// </summary>
        private IEnumerator AttackRoutine(SuperpowerActionDefinition action)
        {
            Vector2 direction = ResolveDirection();
            AttackAnimation animation = action.Animation;

            bool stepped = attackMove != null && animation.LungeDistance > 0f &&
                           attackMove.Begin(direction, animation.LungeDistance, animation.LungeDuration);
            if (stepped)
            {
                BeginAttackStep();
            }

            if (posePlayer != null)
            {
                posePlayer.Play(animation, direction);
            }

            bool sweeping = stepped && action.SweepHitsWhileLunging;
            if (sweeping)
            {
                _persistentHits.Clear();
            }

            try
            {
                if (action.HitDelay > 0f)
                {
                    yield return new WaitForSeconds(action.HitDelay);
                }

                if (sweeping)
                {
                    yield return SweepHits(action, direction, animation.LungeDuration);
                }
                else
                {
                    yield return FixedHits(action, direction);
                }
            }
            finally
            {
                FinishAttackStep();
                _attackRoutine = null;
            }
        }

        private IEnumerator SweepHits(SuperpowerActionDefinition action, Vector2 direction, float stepDuration)
        {
            _sweeping = true;
            float deadline = Mathf.Max(0.01f, stepDuration) + 0.25f;
            float elapsed = 0f;

            while (attackMove != null && attackMove.IsActive && elapsed <= deadline)
            {
                if (PerformHit(action, direction, false, out bool critical))
                {
                    Vector2 center = (Vector2)transform.position +
                                     direction * ((action.Range + _modifiers.RangeFlat) * 0.5f);
                    PlayHitVfx(action, center, direction, critical);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator FixedHits(SuperpowerActionDefinition action, Vector2 direction)
        {
            for (int hit = 0; hit < action.HitCount; hit++)
            {
                PerformHit(action, direction, true, out _);
                if (hit < action.HitCount - 1 && action.HitInterval > 0f)
                {
                    yield return new WaitForSeconds(action.HitInterval);
                }
            }
        }

        private void BeginAttackStep()
        {
            if (_attackStepActive)
            {
                return;
            }

            _attackStepActive = true;
            if (health != null)
            {
                health.AddDamageImmunity(this);
            }

            if (bodyCollider != null && enemyRegistry != null)
            {
                enemyRegistry.SuppressEnemyCollisions(this, bodyCollider);
            }
        }

        private void FinishAttackStep()
        {
            _sweeping = false;
            _persistentHits.Clear();

            if (!_attackStepActive)
            {
                return;
            }

            _attackStepActive = false;
            if (health != null)
            {
                health.RemoveDamageImmunity(this);
            }

            if (bodyCollider != null && enemyRegistry != null)
            {
                enemyRegistry.RestoreEnemyCollisions(this, bodyCollider);
            }
        }

        private Vector2 ResolveDirection()
        {
            Vector2 facing = movement != null ? movement.Facing : Vector2.right;
            Vector2 direction = IsometricUtility.ScreenDirectionToWorld(facing);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            return direction.normalized;
        }

        /// <summary>
        /// Runs one damage frame. Returns true when a melee target was freshly hit, so a sweeping
        /// attack can show its VFX only on contact instead of once per frame along the path.
        /// </summary>
        private bool PerformHit(SuperpowerActionDefinition action, Vector2 direction, bool playVfx, out bool critical)
        {
            critical = false;
            float damage = playerStats != null
                ? playerStats.RollDamage(action.Damage, out critical)
                : (action.Damage + _modifiers.DamageFlat) * _modifiers.DamageMultiplier;
            bool crit = playerStats != null && critical;

            HitEffects effects = action.OnHit;
            if (playerStats != null && playerStats.RollBonusStun())
            {
                effects.StunDuration = Mathf.Max(effects.StunDuration, 0.25f);
            }

            Vector2 origin = transform.position;
            float range = action.Range + _modifiers.RangeFlat;
            Vector2 center = origin + direction * (range * 0.5f);

            switch (action.Shape)
            {
                case SuperpowerAttackShape.Projectile:
                    FireProjectiles(action, origin, direction, damage, effects, crit);
                    return false;
                case SuperpowerAttackShape.Line:
                    int lineHits = HitLine(origin, center, direction, range, action.LineWidth, damage, effects, crit);
                    if (playVfx)
                    {
                        PlayHitVfx(action, center, direction, crit);
                    }

                    return lineHits > 0;
                default:
                    int areaHits = HitArea(origin, center, direction, range + 0.2f, action.ArcDegrees, damage, effects, crit);
                    if (playVfx)
                    {
                        PlayHitVfx(action, center, direction, crit);
                    }

                    return areaHits > 0;
            }
        }

        private void PlayHitVfx(SuperpowerActionDefinition action, Vector2 center, Vector2 direction, bool critical)
        {
            if (_vfxPool == null || action.HitVfxSprite == null)
            {
                return;
            }

            Color color = critical ? Color.Lerp(action.HitVfxColor, Color.yellow, 0.65f) : action.HitVfxColor;
            AttackVfx vfx = _vfxPool.Get();
            vfx.Play(
                center,
                direction,
                action.HitVfxSprite,
                color,
                critical ? action.HitVfxSize * 1.2f : action.HitVfxSize,
                action.HitVfxDuration,
                action.HitVfxSweep);
        }

        private void ReleaseVfx(AttackVfx vfx)
        {
            _vfxPool.Release(vfx);
        }

        private void FireProjectiles(
            SuperpowerActionDefinition action,
            Vector2 origin,
            Vector2 direction,
            float damage,
            in HitEffects effects,
            bool critical)
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
                    enemyMask,
                    effects,
                    critical);
            }
        }

        private int HitLine(Vector2 origin, Vector2 center, Vector2 direction, float range, float width, float damage, in HitEffects effects, bool critical)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = Physics2D.OverlapBox(center, new Vector2(range, width), angle, _enemyFilter, _hits);
            return ApplyMeleeHits(count, origin, center, direction, 360f, damage, effects, critical);
        }

        private int HitArea(Vector2 origin, Vector2 center, Vector2 direction, float radius, float arcDegrees, float damage, in HitEffects effects, bool critical)
        {
            // Query from the attacker so the authored range and cone share the same origin.
            int count = Physics2D.OverlapCircle(origin, radius, _enemyFilter, _hits);
            return ApplyMeleeHits(count, origin, center, direction, arcDegrees, damage, effects, critical);
        }

        private int ApplyMeleeHits(int count, Vector2 origin, Vector2 center, Vector2 direction, float arcDegrees, float damage, in HitEffects effects, bool critical)
        {
            _damaged.Clear();
            int freshHits = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null || !hit.TryGetComponent(out IDamageable damageable) || !damageable.IsAlive)
                {
                    continue;
                }

                if (arcDegrees < 359f)
                {
                    // Measure the cone from the attacker, not the query centre; otherwise a foe
                    // standing right next to the player falls behind the centre and gets skipped.
                    Vector2 offset = (Vector2)hit.transform.position - origin;
                    if (offset.sqrMagnitude > 0.0001f && Vector2.Angle(direction, offset) > arcDegrees * 0.5f)
                    {
                        continue;
                    }
                }

                if (!_damaged.Add(damageable) || (_sweeping && !_persistentHits.Add(damageable)))
                {
                    continue;
                }

                freshHits++;
                float amount = damage;
                Enemy enemy = null;
                if (hit.TryGetComponent(out enemy) && enemy.Rank == EnemyRank.Boss && effects.GuardBreak)
                {
                    // EXPERIMENT: bosses cannot be knocked back, so Guard Break pays in damage.
                    amount *= 1.25f;
                }

                // Knockback is radial: away from the attacker, not merely along the facing arrow.
                // Keep the authored facing direction for arc filtering and VFX only.
                Vector2 hitDirection = (Vector2)hit.transform.position - origin;
                if (hitDirection.sqrMagnitude < 0.0001f)
                {
                    hitDirection = direction;
                }

                hitDirection.Normalize();
                Vector2 hitPoint = hit.ClosestPoint(center);
                damageable.TakeDamage(new DamageInfo(amount, hitPoint, hitDirection, gameObject, effects, critical));
                enemy?.ApplyHitEffects(effects, hitDirection);
                ApplyImpact(hitPoint, damage, damageable, effects, critical);
            }

            return freshHits;
        }

        /// <summary>Dash damage entry point. Damage is already scaled by the Super Charge multiplier.</summary>
        public void ApplyDashHit(IDamageable target, Vector2 origin, float amount, in HitEffects effects)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            bool critical = false;
            float damage = playerStats != null
                ? playerStats.RollDamage(amount, out critical)
                : amount;
            bool crit = playerStats != null && critical;

            // IDamageable is implemented by Health, which shares its GameObject with Enemy. Derive
            // the enemy from the damageable component so dash hits get the same control effects as
            // melee hits without forcing the interface to expose enemy-only concepts.
            Enemy enemy = null;
            var componentTarget = target as Component;
            if (componentTarget != null)
            {
                enemy = componentTarget.GetComponent<Enemy>();
            }

            if (enemy != null && enemy.Rank == EnemyRank.Boss && effects.GuardBreak)
            {
                damage *= 1.25f;
            }

            Vector2 direction = componentTarget != null
                ? (Vector2)componentTarget.transform.position - origin
                : Vector2.right;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            target.TakeDamage(new DamageInfo(damage, origin, direction.normalized, gameObject, effects, crit));
            enemy?.ApplyHitEffects(effects, direction.normalized);
        }

        private void ApplyImpact(Vector2 point, float damage, IDamageable primaryTarget, in HitEffects effects, bool critical)
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

                if (!damageable.IsAlive || !_damaged.Add(damageable) ||
                    (_sweeping && !_persistentHits.Add(damageable)))
                {
                    continue;
                }

                Vector2 hitDirection = (Vector2)hit.transform.position - point;
                if (hitDirection.sqrMagnitude < 0.0001f)
                {
                    hitDirection = Vector2.up;
                }

                hitDirection.Normalize();

                float amount = damage;
                Enemy enemy = null;
                if (hit.TryGetComponent(out enemy) && enemy.Rank == EnemyRank.Boss && effects.GuardBreak)
                {
                    amount *= 1.25f;
                }

                damageable.TakeDamage(new DamageInfo(amount, point, hitDirection, gameObject, effects, critical));
                enemy?.ApplyHitEffects(effects, hitDirection);
            }
        }

        private void ReleaseProjectile(Projectile projectile)
        {
            _pool.Release(projectile);
        }
    }
}
