using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Reads the Input System "Player/Move" action and exposes it as a normalised vector.
    ///
    /// This is the only gameplay component that touches the Input System. By default it uses the
    /// project-wide actions asset; assign <see cref="actionsOverride"/> to point at a different
    /// asset (useful for tests or a rebinding screen). Keyboard, gamepad and joystick bindings all
    /// arrive here already merged by the Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [Header("Input")]
        [Tooltip("Optional override. When empty, the project-wide Input System actions are used.")]
        [SerializeField] private InputActionAsset actionsOverride;

        [Tooltip("Action path within the actions asset.")]
        [SerializeField] private string moveActionPath = "Player/Move";

        private InputAction _moveAction;

        /// <inheritdoc />
        public Vector2 Move { get; private set; }

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
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            Move = Vector2.zero;
        }

        private void Update()
        {
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
