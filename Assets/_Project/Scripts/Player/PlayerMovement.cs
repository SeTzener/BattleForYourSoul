using UnityEngine;
using BattleForYourSoul.Core;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Turns a normalised input direction into world-space movement. Owns physics and collision
    /// only; it never touches the sprite or the camera.
    ///
    /// Input is read in <c>Update</c> and applied in <c>FixedUpdate</c> so physics stays in step
    /// with the fixed timestep.
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
        private Vector2 _desiredVelocity;
        private float _speedMultiplier = 1f;

        /// <summary>Last non-zero input direction in screen space. Presentation reads this.</summary>
        public Vector2 Facing { get; private set; } = Vector2.down;

        /// <summary>Current world-space velocity, for UI/VFX that need it.</summary>
        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        /// <summary>The authored speed, before any upgrade multiplier.</summary>
        public float BaseMoveSpeed => moveSpeed;

        /// <summary>Applies an upgrade speed multiplier. Pushed by the player's stats.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// Moves the player to a stage entrance and clears any motion left over from the previous
        /// room, so walking through a door never carries momentum into the next stage.
        /// </summary>
        public void TeleportTo(Vector2 position)
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }

            if (_body != null)
            {
                _body.position = position;
                _body.linearVelocity = Vector2.zero;
            }

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            _desiredVelocity = Vector2.zero;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _input = GetComponent<IPlayerInput>();

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
