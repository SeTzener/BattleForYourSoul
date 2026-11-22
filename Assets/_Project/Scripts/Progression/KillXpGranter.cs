using UnityEngine;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.Progression
{
    /// <summary>
    /// Turns a kill into XP. It listens to <see cref="EnemySpawner.EnemyDied"/> and banks the enemy's
    /// reward, which already reflects the monster's level. This is the only place kills become XP;
    /// nothing is picked up off the floor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillXpGranter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Experience experience;

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.EnemyDied += HandleEnemyDied;
            }
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.EnemyDied -= HandleEnemyDied;
            }
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            if (enemy == null || experience == null)
            {
                return;
            }

            float reward = enemy.XpReward;
            if (reward > 0f)
            {
                experience.AddXp(reward);
            }
        }
    }
}
