using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Reads the Input System "Player/Move" and "Player/Dash" actions and exposes them as
    /// framework-agnostic player intent. This is the only gameplay component that touches the Input
    /// System; keyboard, gamepad and the mobile virtual gamepad all arrive here already merged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [Header("Input")]
        [Tooltip("Optional override. When empty, the project-wide Input System actions are used.")]
        [SerializeField] private InputActionAsset actionsOverride;

        [Tooltip("Action path within the actions asset.")]
        [SerializeField] private string moveActionPath = "Player/Move";

        [Tooltip("Button action used for the dash.")]
        [SerializeField] private string dashActionPath = "Player/Dash";

        private InputAction _moveAction;
        private InputAction _dashAction;

        /// <inheritdoc />
        public Vector2 Move { get; private set; }

        /// <inheritdoc />
        public bool DashHeld => _dashAction != null && _dashAction.IsPressed();

        public event Action DashPressed;
        public event Action DashReleased;

        private void Awake()
        {
            InputActionAsset asset = actionsOverride != null ? actionsOverride : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogError("[PlayerInputReader] No Input System actions asset available.", this);
                return;
            }

            _moveAction = asset.FindAction(moveActionPath);
            if (_moveAction == null)
            {
                Debug.LogError($"[PlayerInputReader] Action '{moveActionPath}' not found in '{asset.name}'.", this);
            }

            _dashAction = asset.FindAction(dashActionPath);
            if (_dashAction == null)
            {
                Debug.LogError($"[PlayerInputReader] Action '{dashActionPath}' not found in '{asset.name}'.", this);
            }
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _dashAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _dashAction?.Disable();
            Move = Vector2.zero;
        }

        private void Update()
        {
            if (_dashAction != null)
            {
                if (_dashAction.WasPressedThisFrame())
                {
                    DashPressed?.Invoke();
                }

                if (_dashAction.WasReleasedThisFrame())
                {
                    DashReleased?.Invoke();
                }
            }

            if (_moveAction == null)
            {
                return;
            }

            Vector2 raw = _moveAction.ReadValue<Vector2>();
            // Clamp to a unit disc so diagonal movement is not faster than cardinal movement.
            Move = raw.sqrMagnitude > 1f ? raw.normalized : raw;
        }
    }
}
