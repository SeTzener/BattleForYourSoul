using UnityEngine;
using BattleForYourSoul.Combat;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Authoring-time definition of the player character: base stats, body and starting weapon.
    ///
    /// Immutable data only. Runtime state (current health, upgrade bonuses) lives on the scene
    /// components, so many values can share one definition and retuning the player is an asset edit
    /// rather than a code change.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Player", fileName = "Player")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Player";
        [SerializeField] private Sprite sprite;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float acceleration = 60f;
        [SerializeField, Min(0f)] private float deceleration = 80f;

        [Header("Dash")]
        [SerializeField, Min(0.1f)] private float dashSpeed = 14f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.18f;
        [SerializeField, Min(0f)] private float dashCooldown = 1.2f;

        [Tooltip("Brief damage immunity after the dash ends, so an enemy the player lands on cannot hit instantly.")]
        [SerializeField, Min(0f)] private float dashGrace = 0.08f;

        [Header("Body")]
        [SerializeField] private Vector2 colliderSize = new Vector2(0.45f, 0.5f);
        [SerializeField] private Vector2 colliderOffset = new Vector2(0f, 0.25f);

        [Header("Loadout")]
        [Tooltip("Weapon the player starts the run with.")]
        [SerializeField] private WeaponDefinition startingWeapon;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public float DashGrace => dashGrace;
        public Vector2 ColliderSize => colliderSize;
        public Vector2 ColliderOffset => colliderOffset;
        public WeaponDefinition StartingWeapon => startingWeapon;
    }
}
