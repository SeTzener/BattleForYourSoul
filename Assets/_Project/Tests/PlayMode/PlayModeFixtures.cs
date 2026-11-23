using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Player;

namespace BattleForYourSoul.Tests.PlayMode
{
    /// <summary>
    /// Minimal object and asset factory for mechanics tests. Objects start inactive so every
    /// serialized field can be set before Awake runs, then one Activate call starts the component
    /// with the intended configuration. Nothing here loads the generated game scene.
    /// </summary>
    internal sealed class PlayModeFixtures : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        public GameObject NewObject(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            _objects.Add(go);
            return go;
        }

        public void Activate(GameObject go)
        {
            go.SetActive(true);
        }

        public Canvas NewCanvas()
        {
            GameObject go = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(go);
            return go.GetComponent<Canvas>();
        }

        public Image NewImage(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _objects.Add(go);
            return go.GetComponent<Image>();
        }

        public T CreateAsset<T>(params (string Name, object Value)[] fields) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            foreach ((string name, object value) in fields)
            {
                SetField(asset, name, value);
            }

            return asset;
        }

        /// <summary>
        /// Builds a player with only the components a mechanics test needs. Everything is configured
        /// before activation so Awake sees the intended values.
        /// </summary>
        public TestPlayer CreatePlayer(
            bool withWeapon,
            float maxHealth = 100f,
            float dashCooldown = 5f,
            float dashDuration = 0.05f,
            float dashGrace = 0.1f)
        {
            GameObject go = NewObject("Player");
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            Collider2D collider = go.AddComponent<CircleCollider2D>();

            Health health = go.AddComponent<Health>();
            SetField(health, "maxHealth", maxHealth);

            PlayerMovement movement = go.AddComponent<PlayerMovement>();
            TestPlayerInput input = go.AddComponent<TestPlayerInput>();

            PlayerDash dash = go.AddComponent<PlayerDash>();
            SetField(dash, "bodyCollider", collider);
            SetField(dash, "health", health);
            SetField(dash, "dashCooldown", dashCooldown);
            SetField(dash, "dashDuration", dashDuration);
            SetField(dash, "invulnerabilityGrace", dashGrace);

            WeaponController weapon = null;
            if (withWeapon)
            {
                // The pool needs a non-null prefab, but tests never fire a shot, so prewarm stays zero.
                GameObject prefab = NewObject("ProjectilePrefab");
                prefab.AddComponent<Rigidbody2D>();
                Projectile projectile = prefab.AddComponent<Projectile>();

                weapon = go.AddComponent<WeaponController>();
                SetField(weapon, "projectilePrefab", projectile);
                SetField(weapon, "prewarmCount", 0);
                SetField(weapon, "maxPoolSize", 4);
                SetField(weapon, "movement", movement);
            }

            PlayerStats stats = go.AddComponent<PlayerStats>();
            SetField(stats, "health", health);
            SetField(stats, "movement", movement);
            SetField(stats, "dash", dash);
            if (weapon != null)
            {
                SetField(stats, "weapon", weapon);
            }

            Activate(go);
            return new TestPlayer(go, health, weapon, movement, dash, stats, input);
        }

        public static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Fields);
            if (field == null)
            {
                throw new InvalidOperationException($"Field {name} was not found on {target.GetType().Name}.");
            }

            field.SetValue(target, value);
        }

        public static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, Fields);
            if (field == null)
            {
                throw new InvalidOperationException($"Field {name} was not found on {target.GetType().Name}.");
            }

            return (T)field.GetValue(target);
        }

        public void Dispose()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
                }
            }

            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_assets[i]);
                }
            }

            _objects.Clear();
            _assets.Clear();
        }
    }

    internal sealed class TestPlayer
    {
        public TestPlayer(
            GameObject gameObject,
            Health health,
            WeaponController weapon,
            PlayerMovement movement,
            PlayerDash dash,
            PlayerStats stats,
            TestPlayerInput input)
        {
            GameObject = gameObject;
            Health = health;
            Weapon = weapon;
            Movement = movement;
            Dash = dash;
            Stats = stats;
            Input = input;
        }

        public GameObject GameObject { get; }
        public Health Health { get; }
        public WeaponController Weapon { get; }
        public PlayerMovement Movement { get; }
        public PlayerDash Dash { get; }
        public PlayerStats Stats { get; }
        public TestPlayerInput Input { get; }
    }

    /// <summary>A gameplay input source the tests can trigger without touching the Input System.</summary>
    internal sealed class TestPlayerInput : MonoBehaviour, IPlayerInput
    {
        public Vector2 Move { get; set; }

        public event Action DashPressed;

        public void PressDash()
        {
            DashPressed?.Invoke();
        }
    }
}
