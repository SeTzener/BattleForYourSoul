using System;
using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// A pooled enemy: walks straight at a target and deals contact damage. It is configured from an
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

        private Rigidbody2D _body;
        private Transform _target;
        private EnemyRegistry _registry;
        private Action<Enemy> _release;
        private float _moveSpeed;
        private bool _active;

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
        public void Initialize(EnemyDefinition definition, Transform target, EnemyRegistry registry)
        {
            _target = target;
            _registry = registry;
            _active = true;

            if (definition != null)
            {
                _moveSpeed = definition.MoveSpeed;

                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = definition.Sprite;
                    spriteRenderer.flipX = false;
                }

                health.Configure(definition.MaxHealth);

                if (contactDamage != null)
                {
                    contactDamage.Configure(1 << LayerMask.NameToLayer("Player"), definition.ContactDamage, definition.ContactDamageInterval);
                }
            }
            else
            {
                _moveSpeed = 0f;
            }

            registry?.Register(this);
        }

        private void FixedUpdate()
        {
            if (!_active || _target == null || _body == null)
            {
                return;
            }

            Vector2 toTarget = (Vector2)_target.position - _body.position;
            if (toTarget.sqrMagnitude < 0.0001f)
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

        private void HandleDied(Health _)
        {
            _active = false;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }

            _registry?.Unregister(this);
            _release?.Invoke(this);
        }
    }
}
