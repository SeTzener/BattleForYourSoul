using System;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Progression;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// A pooled enemy: waits at its post until the target comes close, then walks straight at it and
    /// deals contact damage. It is configured from an
    /// <see cref="EnemyDefinition"/> every time it is taken from the pool, so one prefab can back
    /// many enemy types.
    ///
    /// Movement, health and contact damage are separate components; this class only coordinates them
    /// and reports death back to its pool.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public sealed class Enemy : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private ContactDamage contactDamage;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private CircleCollider2D bodyCollider;

        private Rigidbody2D _body;
        private Transform _target;
        private EnemyRegistry _registry;
        private Action<Enemy> _release;
        private float _moveSpeed;
        private float _xpReward;
        private int _monsterLevel;
        private float _aggroRadius;
        private float _aggroDelay;
        private bool _aggroed;
        private bool _active;

        /// <summary>XP this enemy is worth when killed, already scaled by its monster level.</summary>
        public float XpReward => _xpReward;

        /// <summary>Body collider, so movement effects can ignore it without searching the scene.</summary>
        public Collider2D BodyCollider => bodyCollider;

        /// <summary>Monster level this enemy was spawned at.</summary>
        public int MonsterLevel => _monsterLevel;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (contactDamage == null)
            {
                contactDamage = GetComponent<ContactDamage>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<CircleCollider2D>();
            }

            health.Died += HandleDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        /// <summary>Injected by the spawning pool so the enemy can return itself on death.</summary>
        public void SetReleaseCallback(Action<Enemy> release)
        {
            _release = release;
        }

        /// <summary>Resets per-spawn state. Called right after the pool hands this enemy out.</summary>
        public void Initialize(EnemyDefinition definition, int level, MonsterScaling scaling, Transform target, EnemyRegistry registry)
        {
            _target = target;
            _registry = registry;
            _monsterLevel = Mathf.Max(1, level);
            _aggroRadius = definition != null ? definition.AggroRadius : 0f;
            // Ignore the spawn frame so a stage-entry teleport cannot accidentally aggro guards.
            _aggroDelay = 0.25f;
            _aggroed = false;
            _active = true;

            if (definition != null)
            {
                float healthMultiplier = scaling != null ? scaling.HealthMultiplier(_monsterLevel) : 1f;
                float damageMultiplier = scaling != null ? scaling.DamageMultiplier(_monsterLevel) : 1f;
                float speedMultiplier = scaling != null ? scaling.SpeedMultiplier(_monsterLevel) : 1f;
                float xpMultiplier = scaling != null ? scaling.XpMultiplier(_monsterLevel) : 1f;

                _moveSpeed = definition.MoveSpeed * speedMultiplier;
                _xpReward = definition.XpReward * xpMultiplier;

                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = definition.Sprite;
                    spriteRenderer.flipX = false;
                }

                if (bodyCollider != null)
                {
                    bodyCollider.radius = definition.ColliderRadius;
                }

                health.Configure(definition.MaxHealth * healthMultiplier);

                if (contactDamage != null)
                {
                    contactDamage.Configure(1 << LayerMask.NameToLayer("Player"), definition.ContactDamage * damageMultiplier, definition.ContactDamageInterval);
                }
            }
            else
            {
                _moveSpeed = 0f;
                _xpReward = 0f;
            }

            registry?.Register(this);
        }

        private void FixedUpdate()
        {
            if (!_active || _target == null || _body == null)
            {
                return;
            }

            if (_aggroDelay > 0f)
            {
                _aggroDelay -= Time.fixedDeltaTime;
                _body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 toTarget = (Vector2)_target.position - _body.position;
            float distanceSqr = toTarget.sqrMagnitude;

            // Stationed enemies wait until the player comes close, then commit to the chase.
            if (!_aggroed)
            {
                if (distanceSqr > _aggroRadius * _aggroRadius)
                {
                    _body.linearVelocity = Vector2.zero;
                    return;
                }

                _aggroed = true;
            }

            if (distanceSqr < 0.0001f)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 direction = toTarget.normalized;
            _body.linearVelocity = direction * _moveSpeed;

            if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            {
                spriteRenderer.flipX = direction.x < 0f;
            }
        }

        /// <summary>
        /// Stops and unregisters an enemy without treating it as a kill. Used when a stage is reset
        /// while enemies are still alive; the caller returns it to the pool.
        /// </summary>
        public void Despawn()
        {
            _active = false;
            _aggroed = false;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }

            _registry?.Unregister(this);
        }

        private void HandleDied(Health _)
        {
            Despawn();
            _release?.Invoke(this);
        }
    }
}
