using System;
using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// Run-scoped superpower choice. During Samsara any pickup can be tried and swapped: selecting a new
    /// one reappears the previous prop at its spawn point. Walking through the exit calls
    /// <see cref="Lock"/>, after which the choice is final for the run.
    ///
    /// This component owns the selection state only; the builder wires the controller and pickups,
    /// so nothing here searches the scene or lives in static state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuperpowerSelection : MonoBehaviour
    {
        [Header("Offerings")]
        [SerializeField] private SuperpowerDefinition[] superpowers;

        [Header("Wiring")]
        [SerializeField] private SuperpowerController controller;
        [SerializeField] private GameObject pickupsRoot;

        private SuperpowerPickup _selectedPickup;
        private bool _locked;

        public SuperpowerDefinition SelectedSuperpower { get; private set; }
        public bool IsLocked => _locked;
        public IReadOnlyList<SuperpowerDefinition> Superpowers => superpowers;

        /// <summary>Raised with the newly equipped superpower each time the player swaps.</summary>
        public event Action<SuperpowerDefinition> SelectionChanged;

        /// <summary>Raised once when the exit commits the current superpower for the run.</summary>
        public event Action<SuperpowerDefinition> SuperpowerLocked;

        /// <summary>Equips a pickup for trial. Returns false after the exit has locked the choice.</summary>
        public bool TrySelect(SuperpowerPickup pickup)
        {
            if (_locked || pickup == null || pickup.Superpower == null)
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
            SelectedSuperpower = pickup.Superpower;
            pickup.SetAvailable(false);

            if (controller != null)
            {
                controller.SetDefinition(SelectedSuperpower);
            }

            SelectionChanged?.Invoke(SelectedSuperpower);
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

        /// <summary>Commits the current superpower. Does nothing if the player never picked one.</summary>
        public void Lock()
        {
            if (_locked || SelectedSuperpower == null)
            {
                return;
            }

            _locked = true;
            SuperpowerLocked?.Invoke(SelectedSuperpower);
        }

        /// <summary>
        /// Reopens the choice for a fresh Samsara. Restores any prop that was hidden by the previous
        /// run's trial and clears the committed definition so the player must choose again.
        /// </summary>
        public void Reset()
        {
            if (_selectedPickup != null)
            {
                _selectedPickup.SetAvailable(true);
            }

            if (controller != null)
            {
                controller.SetDefinition(null);
            }

            _selectedPickup = null;
            SelectedSuperpower = null;
            _locked = false;
            SelectionChanged?.Invoke(null);
        }
    }
}
