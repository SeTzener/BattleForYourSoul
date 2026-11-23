using System;
using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Weapons
{
    /// <summary>
    /// Run-scoped weapon choice. During Samsara any pickup can be tried and swapped: selecting a new
    /// one reappears the previous prop at its spawn point. Walking through the exit calls
    /// <see cref="Lock"/>, after which the choice is final for the run.
    ///
    /// This component owns the selection state only; the builder wires the controller and pickups,
    /// so nothing here searches the scene or lives in static state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponSelection : MonoBehaviour
    {
        [Header("Offerings")]
        [SerializeField] private WeaponDefinition[] weapons;

        [Header("Wiring")]
        [SerializeField] private WeaponController controller;
        [SerializeField] private GameObject pickupsRoot;

        private WeaponPickup _selectedPickup;
        private bool _locked;

        public WeaponDefinition SelectedWeapon { get; private set; }
        public bool IsLocked => _locked;
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;

        /// <summary>Raised with the newly equipped weapon each time the player swaps.</summary>
        public event Action<WeaponDefinition> SelectionChanged;

        /// <summary>Raised once when the exit commits the current weapon for the run.</summary>
        public event Action<WeaponDefinition> WeaponLocked;

        /// <summary>Equips a pickup for trial. Returns false after the exit has locked the choice.</summary>
        public bool TrySelect(WeaponPickup pickup)
        {
            if (_locked || pickup == null || pickup.Weapon == null)
            {
                return false;
            }

            if (_selectedPickup == pickup)
            {
                return true;
            }

            if (_selectedPickup != null)
            {
                _selectedPickup.SetAvailable(true);
            }

            _selectedPickup = pickup;
            SelectedWeapon = pickup.Weapon;
            pickup.SetAvailable(false);

            if (controller != null)
            {
                controller.SetDefinition(SelectedWeapon);
            }

            SelectionChanged?.Invoke(SelectedWeapon);
            return true;
        }

        /// <summary>
        /// Shows the physical props only in Samsara. The selection and its availability states survive
        /// being hidden, so returning to the room would restore exactly what the player left behind.
        /// </summary>
        public void SetPickupsVisible(bool visible)
        {
            if (pickupsRoot != null)
            {
                pickupsRoot.SetActive(visible);
            }
        }

        /// <summary>Commits the current weapon. Does nothing if the player never picked one.</summary>
        public void Lock()
        {
            if (_locked || SelectedWeapon == null)
            {
                return;
            }

            _locked = true;
            WeaponLocked?.Invoke(SelectedWeapon);
        }
    }
}
