using UnityEngine;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// The only component allowed to move the player's body for an attack. It advances one
    /// deterministic step with <see cref="Rigidbody2D.MovePosition"/> inside <c>FixedUpdate</c>, from
    /// the captured start to an exact target along the captured direction. Input is suspended by
    /// <see cref="PlayerMovement"/> while this is active, so held movement cannot bend the step.
    ///
    /// Do not set <c>linearVelocity</c> after <c>MovePosition</c>: that overwrites the velocity the
    /// engine computed for the move and was the source of the old erratic behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class AttackMovement : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;

        private Rigidbody2D _body;
        private Vector2 _start;
        private Vector2 _target;
        private float _duration;
        private float _elapsed;
        private bool _active;
        private bool _finishing;

        /// <summary>True while this component owns the body's position.</summary>
        public bool IsActive => _active;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (movement == null)
            {
                movement = GetComponent<PlayerMovement>();
            }
        }

        /// <summary>
        /// Starts one attack step. The direction is captured once and never re-read, so turning
        /// mid-attack cannot change it. Returns false if a dash or another step already owns the body.
        /// </summary>
        public bool Begin(Vector2 direction, float distance, float duration)
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }

            if (_body == null || _active || movement != null && movement.IsDashing ||
                direction.sqrMagnitude < 0.0001f || distance <= 0f)
            {
                return false;
            }

            _start = _body.position;
            _target = _start + direction.normalized * distance;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _finishing = false;
            _active = true;
            _body.linearVelocity = Vector2.zero;
            return true;
        }

        /// <summary>Stops immediately and leaves the body at rest where it currently is.</summary>
        public void Cancel()
        {
            if (!_active && !_finishing)
            {
                return;
            }

            _active = false;
            _finishing = false;
            _elapsed = 0f;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            if (!_active || _body == null)
            {
                return;
            }

            // A dash outranks an attack step; never let two systems fight over the same position.
            if (movement != null && movement.IsDashing)
            {
                Cancel();
                return;
            }

            // Give the final MovePosition one physics step to land, then stop. Zeroing velocity in
            // the same frame as the last MovePosition would cancel that final move.
            if (_finishing)
            {
                _active = false;
                _finishing = false;
                _elapsed = 0f;
                _body.linearVelocity = Vector2.zero;
                return;
            }

            _elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            _body.MovePosition(Vector2.Lerp(_start, _target, t));

            if (t >= 1f)
            {
                _elapsed = 0f;
                _finishing = true;
            }
        }
    }
}
