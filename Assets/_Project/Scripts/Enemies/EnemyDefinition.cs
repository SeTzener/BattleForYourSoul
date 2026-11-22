using UnityEngine;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// Authoring-time definition of an enemy type. Immutable data only: per-instance state (health,
    /// velocity) lives on the spawned <see cref="Enemy"/>, so many instances share one definition.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleForYourSoul/Enemy", fileName = "Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Grunt";
        [SerializeField] private Sprite sprite;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 3f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.2f;

        [Header("Contact Damage")]
        [SerializeField, Min(0f)] private float contactDamage = 6f;
        [SerializeField, Min(0.05f)] private float contactDamageInterval = 0.85f;

        [Header("Reward")]
        [Tooltip("Base XP at monster level 1. Scaled up by the monster level (see MonsterScaling).")]
        [SerializeField, Min(0f)] private float xpReward = 1f;

        [Header("Body")]
        [SerializeField, Min(0.05f)] private float colliderRadius = 0.28f;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public float ContactDamageInterval => contactDamageInterval;
        public float XpReward => xpReward;
        public float ColliderRadius => colliderRadius;
    }
}
