using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BattleForYourSoul.Weapons
{
    /// <summary>
    /// Translates the Input System's two attack actions into framework-agnostic press events. This is
    /// the only place in weapon gameplay that knows about input devices; the weapon controller only
    /// sees <see cref="IWeaponActions"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponInputReader : MonoBehaviour, IWeaponActions
    {
        [Header("Input")]
        [Tooltip("Optional override. When empty, the project-wide Input System actions are used.")]
        [SerializeField] private InputActionAsset actionsOverride;

        [SerializeField] private string primaryActionPath = "Player/PrimaryAttack";
        [SerializeField] private string secondaryActionPath = "Player/SecondaryAttack";

        private InputAction _primary;
        private InputAction _secondary;

        public event Action PrimaryPressed;
        public event Action SecondaryPressed;

        private void Awake()
        {
            InputActionAsset asset = actionsOverride != null ? actionsOverride : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogError("[WeaponInputReader] No Input System actions asset available.", this);
                return;
            }

            _primary = asset.FindAction(primaryActionPath);
            _secondary = asset.FindAction(secondaryActionPath);

            if (_primary == null)
            {
                Debug.LogError($"[WeaponInputReader] Action '{primaryActionPath}' not found in '{asset.name}'.", this);
            }

            if (_secondary == null)
            {
                Debug.LogError($"[WeaponInputReader] Action '{secondaryActionPath}' not found in '{asset.name}'.", this);
            }
        }

        private void OnEnable()
        {
            _primary?.Enable();
            _secondary?.Enable();
        }

        private void OnDisable()
        {
            _primary?.Disable();
            _secondary?.Disable();
        }

        private void Update()
        {
            if (_primary != null && _primary.WasPressedThisFrame())
            {
                PrimaryPressed?.Invoke();
            }

            if (_secondary != null && _secondary.WasPressedThisFrame())
            {
                SecondaryPressed?.Invoke();
            }
        }
    }
}
