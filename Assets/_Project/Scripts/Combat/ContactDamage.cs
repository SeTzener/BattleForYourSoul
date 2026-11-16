using UnityEngine;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Deals damage to anything on <c>targetMask</c> that stays in contact with this collider,
    /// throttled by an interval so a single touch does not drain a health bar instantly.
    ///
    /// Used by enemies for contact damage. The mask is injected so the component does not hard-code
    /// "the player".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ContactDamage : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask;
        [SerializeField, Min(0f)] private float damage = 5f;
        [SerializeField, Min(0.05f)] private float interval = 0.5f;

        private float _nextDamageTime;

        public void Configure(LayerMask mask, float damageAmount, float damageInterval)
        {
            targetMask = mask;
            damage = Mathf.Max(0f, damageAmount);
            interval = Mathf.Max(0.05f, damageInterval);
            _nextDamageTime = 0f;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (Time.time < _nextDamageTime)
            {
                return;
            }

            if (((1 << collision.gameObject.layer) & targetMask) == 0)
            {
                return;
            }

            if (!collision.gameObject.TryGetComponent(out IDamageable damageable) || !damageable.IsAlive)
            {
                return;
            }

            Vector2 direction = ((Vector2)collision.transform.position - (Vector2)transform.position);
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;

            damageable.TakeDamage(new DamageInfo(damage, transform.position, direction, gameObject));
            _nextDamageTime = Time.time + interval;
        }
    }
}
