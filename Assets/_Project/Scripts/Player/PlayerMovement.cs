using System;
using UnityEngine;
using BattleForYourSoul.Core;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Turns a normalised input direction into world-space movement. Owns physics and collision
    /// only; it never touches the sprite or the camera.
    ///
    /// Input is read in <c>Update</c> and applied in <c>FixedUpdate</c> so physics stays in step
    /// with the fixed timestep. Dashes and attack steps temporarily own the body; this component
    /// simply gets out of their way instead of blending its velocity with theirs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Header("Definition")]
        [Tooltip("Optional definition asset. When set, its movement values override the fields below.")]
        [SerializeField] private PlayerDefinition definition;

        [Header("Movement")]
        [Tooltip("World units per second at full input.")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        [Tooltip("How quickly the body reaches target velocity (units/s^2). 0 = instant.")]
        [SerializeField, Min(0f)] private float acceleration = 60f;

        [Tooltip("How quickly the body stops when there is no input (units/s^2).")]
        [SerializeField, Min(0f)] private float deceleration = 80f;

        private Rigidbody2D _body;
        private IPlayerInput _input;
        private AttackMovement _attackMove;
        private Vector2 _desiredVelocity;
        private float _speedMultiplier = 1f;
        private Vector2 _dashVelocity;
        private bool _dashing;

        /// <summary>Raised after a teleport so transient state (such as a dash) can be cancelled.</summary>
        public event Action Teleported;

        /// <summary>Last non-zero input direction in screen space. Presentation reads this.</summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        /// <summary>Current world-space velocity, for UI/VFX that need it.</summary>
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        /// <summary>The authored speed, before any upgrade multiplier.</summary>
        public float BaseMoveSpeed => moveSpeed;

        /// <summary>Whether a dash is currently driving this body.</summary>
        public bool IsDashing => _dashing;

        /// <summary>Applies an upgrade speed multiplier. Pushed by the player's stats.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// Moves the player to a stage entrance and clears any motion left over from the previous
        /// room, so walking through a door never carries momentum or an unfinished attack step into
        /// the next stage.
        /// </summary>
        public void TeleportTo(Vector2 position)
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }

            if (_attackMove == null)
            {
                _attackMove = GetComponent<AttackMovement>();
            }

            _attackMove?.Cancel();

            if (_body != null)
            {
                _body.position = position;
                _body.linearVelocity = Vector2.zero;
            }

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            _desiredVelocity = Vector2.zero;
            _dashing = false;
            Teleported?.Invoke();
        }

        /// <summary>
        /// Takes over velocity for the dash. The caller owns the timer and calls <see cref="EndDash"/>
        /// when it expires, so dash duration is authored in one place.
        /// </summary>
        public void BeginDash(Vector2 direction, float speed)
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.down;
            }

            _dashVelocity = direction.normalized * Mathf.Max(0f, speed);
            _dashing = true;

            if (_body != null)
            {
                _body.linearVelocity = _dashVelocity;
            }
        }

        /// <summary>Returns velocity control to normal movement.</summary>
        public void EndDash()
        {
            _dashing = false;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _input = GetComponent<IPlayerInput>();
            _attackMove = GetComponent<AttackMovement>();

            if (definition != null)
            {
                moveSpeed = definition.MoveSpeed;
                acceleration = definition.Acceleration;
                deceleration = definition.Deceleration;
            }
        }

        private void Update()
        {
            Vector2 screenDirection = _input != null ? _input.Move : Vector2.zero;
            if (screenDirection.sqrMagnitude > 1f)
            {
                screenDirection.Normalize();
            }

            Vector2 worldDirection = IsometricUtility.ScreenDirectionToWorld(screenDirection);
            _desiredVelocity = worldDirection * (moveSpeed * _speedMultiplier);

            if (screenDirection.sqrMagnitude > 0.0001f)
            {
                Facing = screenDirection;
            }
        }

        private void FixedUpdate()
        {
            // A dash overrides normal movement for its whole duration; otherwise acceleration
            // would immediately bleed away the dash velocity.
            if (_dashing)
            {
                if (_body != null)
                {
                    _body.linearVelocity = _dashVelocity;
                }

                return;
            }

            // AttackMovement drives position itself. Do not fight it with input velocity.
            if (_attackMove != null && _attackMove.IsActive)
            {
                return;
            }

            if (_desiredVelocity.sqrMagnitude > 0.0001f && acceleration > 0f)
            {
                _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, _desiredVelocity, acceleration * Time.fixedDeltaTime);
            }
            else if (_desiredVelocity.sqrMagnitude <= 0.0001f && deceleration > 0f)
            {
                _body.linearVelocity = Vector2.MoveTowards(_body.linearVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                _body.linearVelocity = _desiredVelocity;
            }
        }
    }
}
