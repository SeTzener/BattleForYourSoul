using System;
using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// A pooled projectile: travels in a straight line, damages the first live
    /// <see cref="IDamageable"/> it hits, and returns itself to its pool.
    ///
    /// It owns no spawning logic. The pool hands it out and gives it a release callback, so a
    /// projectile never needs to know who fired it or where it came from.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [Tooltip("Radius used to nudge the projectile away from its muzzle when it spawns.")]
        [SerializeField, Min(0f)] private float spawnRadius = 0.2f;

        private Rigidbody2D _body;
        private Action<Projectile> _release;
        private GameObject _owner;
        private float _damage;
        private float _remainingLife;
        private bool _active;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
        }

        /// <summary>Injected by the owning pool so the projectile can return itself.</summary>
        public void SetReleaseCallback(Action<Projectile> release)
        {
            _release = release;
        }

        /// <summary>
        /// Arms the projectile. Called immediately after the pool hands it out, so all per-shot
        /// state is reset here (important for reuse).
        /// </summary>
        public void Launch(Vector2 origin, Vector2 direction, float speed, float damage, float lifetime, GameObject owner)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();

            transform.position = origin + direction * spawnRadius;
            transform.rotation = Quaternion.identity;

            _owner = owner;
            _damage = damage;
            _remainingLife = lifetime;
            _active = true;

            if (_body != null)
            {
                _body.linearVelocity = direction * speed;
            }
        }

        private void FixedUpdate()
        {
            if (!_active)
            {
                return;
            }

            _remainingLife -= Time.fixedDeltaTime;
            if (_remainingLife <= 0f)
            {
                Release();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_active || other.isTrigger)
            {
                return;
            }

            // Never collide with whoever fired us (the muzzle overlaps its own collider on spawn).
            if (_owner != null && other.gameObject == _owner)
            {
                return;
            }

            if (other.TryGetComponent(out IDamageable damageable) && damageable.IsAlive)
            {
                Vector2 direction = _body != null ? _body.linearVelocity.normalized : Vector2.up;
                damageable.TakeDamage(new DamageInfo(_damage, transform.position, direction, _owner));
            }

            Release();
        }

        private void Release()
        {
            _active = false;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }

            _release?.Invoke(this);
        }

        private void OnDisable()
        {
            _active = false;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }
        }
    }
}
