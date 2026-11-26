using System.Collections.Generic;
using UnityEngine;

namespace BattleForYourSoul.Enemies
{
    /// <summary>
    /// Scene-scoped index of live enemies, so superpowers can find a target without searching the scene
    /// each frame.
    ///
    /// Deliberately a plain scene component rather than a global singleton: each Game scene owns
    /// exactly one, and it disappears with the scene. Enemies register and unregister themselves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRegistry : MonoBehaviour
    {
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly Dictionary<object, List<Collider2D>> _suppressedByOwner = new Dictionary<object, List<Collider2D>>();
        private readonly Dictionary<Collider2D, int> _suppressionCounts = new Dictionary<Collider2D, int>();

        /// <summary>Number of currently live enemies.</summary>
        public int Count => _enemies.Count;

        /// <summary>Live enemies, for systems that need to touch every body without a scene search.</summary>
        public IReadOnlyList<Enemy> Enemies => _enemies;

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
        /// Temporarily lets a collider pass through every live enemy. Calls are owner-keyed and
        /// counted, so a dash and a moving attack can overlap without one restoring collisions
        /// while the other still needs pass-through. Environment colliders are never touched.
        /// </summary>
        public void SuppressEnemyCollisions(object owner, Collider2D playerCollider)
        {
            if (owner == null || playerCollider == null || _suppressedByOwner.ContainsKey(owner))
            {
                return;
            }

            var suppressed = new List<Collider2D>(_enemies.Count);
            for (int i = 0; i < _enemies.Count; i++)
            {
                Collider2D enemyCollider = _enemies[i] != null ? _enemies[i].BodyCollider : null;
                if (enemyCollider == null || !enemyCollider.enabled)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
                suppressed.Add(enemyCollider);
                _suppressionCounts.TryGetValue(enemyCollider, out int count);
                _suppressionCounts[enemyCollider] = count + 1;
            }

            _suppressedByOwner[owner] = suppressed;
        }

        /// <summary>Releases one owner's pass-through lease, restoring collisions only when the last owner leaves.</summary>
        public void RestoreEnemyCollisions(object owner, Collider2D playerCollider)
        {
            if (owner == null || playerCollider == null ||
                !_suppressedByOwner.TryGetValue(owner, out List<Collider2D> suppressed))
            {
                return;
            }

            _suppressedByOwner.Remove(owner);
            for (int i = 0; i < suppressed.Count; i++)
            {
                Collider2D enemyCollider = suppressed[i];
                if (enemyCollider == null || !_suppressionCounts.TryGetValue(enemyCollider, out int count))
                {
                    continue;
                }

                if (count <= 1)
                {
                    _suppressionCounts.Remove(enemyCollider);
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);
                }
                else
                {
                    _suppressionCounts[enemyCollider] = count - 1;
                }
            }
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
