using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// A pooled projectile: travels in a straight line, damages what it hits, and can optionally
    /// bounce to new targets or leave an impact burst. The extra effects default to zero, so the
    /// basic bolt behaves exactly like the original placeholder.
    ///
    /// It owns no spawning logic. The pool hands it out and gives it a release callback, so a
    /// projectile never needs to know who fired it or where it came from.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [Tooltip("Radius used to nudge the projectile away from its muzzle when it spawns.")]
        [SerializeField, Min(0f)] private float spawnRadius = 0.2f;

        [Tooltip("Renderer the firing weapon's projectile sprite is applied to.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private readonly List<Collider2D> _impactHits = new List<Collider2D>(16);
        private readonly HashSet<Collider2D> _hitColliders = new HashSet<Collider2D>();
        private ContactFilter2D _impactFilter;

        private Rigidbody2D _body;
        private Action<Projectile> _release;
        private GameObject _owner;
        private float _damage;
        private float _remainingLife;
        private float _impactRadius;
        private int _bounces;
        private bool _active;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        /// <summary>Injected by the owning pool so the projectile can return itself.</summary>
        public void SetReleaseCallback(Action<Projectile> release)
        {
            _release = release;
        }

        /// <summary>Applies the firing weapon's projectile sprite. Called before <see cref="Launch"/>.</summary>
        public void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        public void Launch(Vector2 origin, Vector2 direction, float speed, float damage, float lifetime, GameObject owner)
        {
            Launch(origin, direction, speed, damage, lifetime, owner, 0, 0f, default);
        }

        /// <summary>
        /// Arms the projectile. Called immediately after the pool hands it out, so all per-shot
        /// state is reset here (important for reuse).
        /// </summary>
        public void Launch(
            Vector2 origin,
            Vector2 direction,
            float speed,
            float damage,
            float lifetime,
            GameObject owner,
            int bounces,
            float impactRadius,
            LayerMask impactMask)
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
            _bounces = Mathf.Max(0, bounces);
            _impactRadius = Mathf.Max(0f, impactRadius);
            _impactFilter.useLayerMask = true;
            _impactFilter.layerMask = impactMask;
            _hitColliders.Clear();
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
            if (!_active || other.isTrigger || _hitColliders.Contains(other))
            {
                return;
            }

            // Never collide with whoever fired us (the muzzle overlaps its own collider on spawn).
            if (_owner != null && other.gameObject == _owner)
            {
                return;
            }

            _hitColliders.Add(other);

            if (other.TryGetComponent(out IDamageable damageable) && damageable.IsAlive)
            {
                Vector2 direction = _body != null ? _body.linearVelocity.normalized : Vector2.up;
                damageable.TakeDamage(new DamageInfo(_damage, transform.position, direction, _owner));
                ApplyImpact(transform.position, damageable);
            }

            if (_bounces > 0 && TryBounce(other))
            {
                _bounces--;
                return;
            }

            Release();
        }

        private bool TryBounce(Collider2D hit)
        {
            if (_body == null)
            {
                return false;
            }

            Vector2 velocity = _body.linearVelocity;
            if (velocity.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector2 surfaceNormal = (Vector2)transform.position - hit.ClosestPoint(transform.position);
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                surfaceNormal = -velocity.normalized;
            }

            surfaceNormal.Normalize();
            Vector2 reflected = Vector2.Reflect(velocity.normalized, surfaceNormal).normalized;
            if (reflected.sqrMagnitude < 0.0001f)
            {
                reflected = -velocity.normalized;
            }

            _body.linearVelocity = reflected * velocity.magnitude;
            transform.position = (Vector2)transform.position + reflected * (spawnRadius + 0.05f);
            return true;
        }

        private void ApplyImpact(Vector2 point, IDamageable primaryTarget)
        {
            if (_impactRadius <= 0f)
            {
                return;
            }

            int count = Physics2D.OverlapCircle(point, _impactRadius, _impactFilter, _impactHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _impactHits[i];
                if (hit == null || _hitColliders.Contains(hit) || !hit.TryGetComponent(out IDamageable damageable))
                {
                    continue;
                }

                if (damageable == primaryTarget || !damageable.IsAlive)
                {
                    continue;
                }

                _hitColliders.Add(hit);
                damageable.TakeDamage(new DamageInfo(_damage, hit.transform.position, Vector2.zero, _owner));
            }
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
