using System;
using UnityEngine;
using UnityEngine.Pool;

namespace BattleForYourSoul.Infrastructure
{
    /// <summary>
    /// A small wrapper over <see cref="ObjectPool{T}"/> for pooling prefab instances.
    ///
    /// It keeps the pool mechanics (get/release, prewarm, cap) in one place so gameplay systems
    /// only deal with "give me one" / "take it back" instead of repeating the boilerplate.
    /// Instances are parented under a caller-supplied transform so the hierarchy stays readable.
    /// </summary>
    public sealed class PrefabPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;

        /// <param name="prefab">Prefab to clone. Must not be null.</param>
        /// <param name="parent">Transform new instances are parented under.</param>
        /// <param name="prewarm">How many instances to create up front.</param>
        /// <param name="maxSize">Hard cap; releases beyond this are destroyed.</param>
        /// <param name="onCreated">Optional hook run once per created instance (e.g. wire callbacks).</param>
        public PrefabPool(T prefab, Transform parent, int prewarm, int maxSize, Action<T> onCreated = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            _pool = new ObjectPool<T>(
                createFunc: () =>
                {
                    T instance = UnityEngine.Object.Instantiate(prefab, parent);
                    instance.name = prefab.name;
                    onCreated?.Invoke(instance);
                    return instance;
                },
                actionOnGet: instance => instance.gameObject.SetActive(true),
                actionOnRelease: instance => instance.gameObject.SetActive(false),
                actionOnDestroy: instance => UnityEngine.Object.Destroy(instance.gameObject),
                collectionCheck: true,
                defaultCapacity: Mathf.Max(1, prewarm),
                maxSize: Mathf.Max(1, maxSize));

            Prewarm(prewarm);
        }

        public T Get()
        {
            return _pool.Get();
        }

        public void Release(T instance)
        {
            if (instance != null)
            {
                _pool.Release(instance);
            }
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private void Prewarm(int count)
        {
            if (count <= 0)
            {
                return;
            }

            // Grow the pool first, then return everything so the pool's default capacity is filled.
            var created = new T[count];
            for (int i = 0; i < count; i++)
            {
                created[i] = _pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                _pool.Release(created[i]);
            }
        }
    }
}
