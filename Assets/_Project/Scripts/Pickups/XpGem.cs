using System;
using UnityEngine;

namespace BattleForYourSoul.Pickups
{
    /// <summary>
    /// A pooled XP pickup. It sits where it dropped until the player comes within its magnet radius,
    /// then flies to them and reports collection. It never grants XP itself: the drop controller
    /// owns that, so pickups stay dumb and reusable.
    ///
    /// Collection is a distance check rather than a physics trigger, so gems need no collider or
    /// rigidbody and cost nothing to the physics world.
    /// </summary>
    public sealed class XpGem : MonoBehaviour
    {
        [Tooltip("Distance at which the gem is considered collected.")]
        [SerializeField, Min(0.05f)] private float collectRadius = 0.35f;

        [Tooltip("Seconds before an uncollected gem gives up and returns to the pool.")]
        [SerializeField, Min(1f)] private float lifetime = 60f;

        private Transform _target;
        private Action<XpGem> _release;
        private Action<XpGem> _onCollected;
        private float _xp;
        private float _magnetRadius;
        private float _moveSpeed;
        private float _remainingLife;
        private bool _active;

        /// <summary>XP this gem is worth.</summary>
        public float XpValue => _xp;

        /// <summary>Injected by the owning pool so the gem can return itself.</summary>
        public void SetReleaseCallback(Action<XpGem> release)
        {
            _release = release;
        }

        /// <summary>Arms the gem. Called right after the pool hands it out.</summary>
        public void Initialize(Transform target, float xp, float magnetRadius, float moveSpeed, Action<XpGem> onCollected)
        {
            _target = target;
            _xp = xp;
            _magnetRadius = magnetRadius;
            _moveSpeed = moveSpeed;
            _onCollected = onCollected;
            _remainingLife = lifetime;
            _active = true;
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _remainingLife -= Time.deltaTime;
            if (_remainingLife <= 0f)
            {
                _active = false;
                _release?.Invoke(this);
                return;
            }

            if (_target == null)
            {
                return;
            }

            Vector2 position = transform.position;
            Vector2 toTarget = (Vector2)_target.position - position;
            float sqrDistance = toTarget.sqrMagnitude;

            if (sqrDistance <= collectRadius * collectRadius)
            {
                _active = false;
                _onCollected?.Invoke(this);
                _release?.Invoke(this);
                return;
            }

            if (sqrDistance <= _magnetRadius * _magnetRadius)
            {
                transform.position = Vector2.MoveTowards(position, _target.position, _moveSpeed * Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            _active = false;
        }
    }
}
