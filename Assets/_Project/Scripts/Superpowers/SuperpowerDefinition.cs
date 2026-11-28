using UnityEngine;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// Authoring-time definition of a superpower. Immutable data only: cooldowns, modifiers and reusable
    /// buffers live on <see cref="SuperpowerController"/>, so two actors can share one definition safely.
    /// The two actions are separate assets so a designer can retune an attack without touching the
    /// superpower's identity or its pickups.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Superpower", fileName = "Superpower")]
    public sealed class SuperpowerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "superpower";
        [SerializeField] private string displayName = "Superpower";
        [SerializeField, TextArea] private string description = "";

        [Tooltip("Sprite used by the physical pickup and the selection label.")]
        [SerializeField] private Sprite pickupSprite;

        [Header("Actions")]
        [Tooltip("Fired by the primary attack input.")]
        [SerializeField] private SuperpowerActionDefinition primaryAction;

        [Tooltip("Fired by the secondary attack input.")]
        [SerializeField] private SuperpowerActionDefinition specialAction;

        [Header("Inherent stats")]
        [Tooltip("Defense points granted while this superpower is equipped. 0 for everyone except Superstrength.")]
        [SerializeField, Min(0f)] private float defense;

        [Tooltip("Knee of the diminishing-returns defense curve. Smaller = defense scales faster.")]
        [SerializeField, Min(1f)] private float defenseKnee = 50f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite PickupSprite => pickupSprite;
        public SuperpowerActionDefinition PrimaryAction => primaryAction;
        public SuperpowerActionDefinition SpecialAction => specialAction;
        public float Defense => defense;
        public float DefenseKnee => defenseKnee;
    }
}
