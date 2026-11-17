using System.Collections.Generic;
using UnityEngine;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// Scene-scoped index of live enemies, so weapons can find a target without searching the scene
    /// each frame.
    ///
    /// Deliberately a plain scene component rather than a global singleton: each Game scene owns
    /// exactly one, and it disappears with the scene. Enemies register and unregister themselves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRegistry : MonoBehaviour
    {
        private readonly List<Enemy> _enemies = new List<Enemy>();

        /// <summary>Number of currently live enemies.</summary>
        public int Count => _enemies.Count;

        public void Register(Enemy enemy)
        {
            if (enemy == null || _enemies.Contains(enemy))
            {
                return;
            }

            _enemies.Add(enemy);
        }

        public void Unregister(Enemy enemy)
        {
            _enemies.Remove(enemy);
        }

        /// <summary>
        /// Finds the closest live enemy within <paramref name="maxRange"/> of
        /// <paramref name="origin"/>. Allocation-free; iterates the backing list directly.
        /// </summary>
        public bool TryGetNearest(Vector2 origin, float maxRange, out Transform target)
        {
            target = null;
            float bestSqr = maxRange * maxRange;
            bool found = false;

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float sqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    target = enemy.transform;
                    found = true;
                }
            }

            return found;
        }
    }
}
