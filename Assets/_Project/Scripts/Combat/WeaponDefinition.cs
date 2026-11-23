using UnityEngine;
using BattleForYourSoul.Weapons;

namespace BattleForYourSoul.Combat
{
    /// <summary>
    /// Authoring-time definition of a weapon. Immutable data only: cooldowns, modifiers and reusable
    /// buffers live on <see cref="WeaponController"/>, so two actors can share one definition safely.
    /// The two actions are separate assets so a designer can retune an attack without touching the
    /// weapon's identity or its pickups.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Weapon", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "weapon";
        [SerializeField] private string displayName = "Weapon";
        [SerializeField, TextArea] private string description = "";

        [Tooltip("Sprite used by the physical pickup and the selection label.")]
        [SerializeField] private Sprite pickupSprite;

        [Header("Actions")]
        [Tooltip("Fired by the primary attack input.")]
        [SerializeField] private WeaponActionDefinition primaryAction;

        [Tooltip("Fired by the secondary attack input.")]
        [SerializeField] private WeaponActionDefinition specialAction;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PickupSprite => pickupSprite;
        public WeaponActionDefinition PrimaryAction => primaryAction;
        public WeaponActionDefinition SpecialAction => specialAction;
    }
}
