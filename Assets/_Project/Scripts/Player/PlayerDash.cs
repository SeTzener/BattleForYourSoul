using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Core;
using BattleForYourSoul.Enemies;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Run-scoped dash state: charges, cooldown and the brief pass-through window.
    ///
    /// Gameplay ownership is split on purpose: this component decides <em>when</em> a dash happens
    /// and handles enemies/immunity, <see cref="PlayerMovement"/> owns the body velocity while it
    /// lasts. Input arrives through <see cref="IPlayerInput"/>, so nothing here touches a device.
    /// Instant charges granted by upgrades are always ready and refill after each dash; the normal
    /// charge still recharges on its cooldown.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerDash : MonoBehaviour
    {
        private const int BaseCharges = 1;

        [Header("Definition")]
        [Tooltip("Optional definition asset. When set, its dash values override the fields below.")]
        [SerializeField] private PlayerDefinition definition;

        [Header("Dash")]
        [SerializeField, Min(0.1f)] private float dashSpeed = 14f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.18f;
        [SerializeField, Min(0f)] private float dashCooldown = 1.2f;
        [SerializeField, Min(0f)] private float invulnerabilityGrace = 0.08f;

        [Header("Wiring")]
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private EnemyRegistry enemyRegistry;
        [SerializeField] private Health health;

        private readonly List<Collider2D> _ignoredColliders = new List<Collider2D>(8);

        private IPlayerInput _input;
        private PlayerMovement _movement;
        private int _cooldownCharges = BaseCharges;
        private int _grantedInstantCharges;
        private int _instantAvailable;
        private float _cooldownRemaining;
        private float _dashRemaining;
        private float _graceRemaining;
        private bool _dashing;

        public bool IsDashing => _dashing;

        private void Awake()
        {
            if (definition != null)
            {
                dashSpeed = definition.DashSpeed;
                dashDuration = definition.DashDuration;
                dashCooldown = definition.DashCooldown;
                invulnerabilityGrace = definition.DashGrace;
            }

            _movement = GetComponent<PlayerMovement>();
            _input = GetComponent<IPlayerInput>();

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            _movement.Teleported += Cancel;
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.DashPressed += TryDash;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.DashPressed -= TryDash;
            }

            Cancel();
        }

        private void OnDestroy()
        {
            if (_movement != null)
            {
                _movement.Teleported -= Cancel;
            }
        }

        private void Update()
        {
            if (_cooldownCharges < BaseCharges && _cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
                if (_cooldownRemaining <= 0f)
                {
                    _cooldownCharges++;
                    _cooldownRemaining = _cooldownCharges < BaseCharges ? dashCooldown : 0f;
                }
            }

            if (_dashing)
            {
                _dashRemaining -= Time.deltaTime;
                if (_dashRemaining <= 0f)
                {
                    EndDash();
                }
            }
            else if (_graceRemaining > 0f)
            {
                _graceRemaining -= Time.deltaTime;
                if (_graceRemaining <= 0f && health != null)
                {
                    health.SetDamageImmune(false);
                }
            }
        }

        /// <summary>
        /// Pushed by <see cref="PlayerStats"/> when an upgrade is taken. Instant charges are always
        /// ready, so they allow another dash even while the normal charge is still recharging.
        /// </summary>
        public void SetInstantCharges(int value)
        {
            _grantedInstantCharges = Mathf.Max(0, value);
            _instantAvailable = _grantedInstantCharges;
        }

        private void TryDash()
        {
            // A modal (level-up/game over) freezes time; do not start a dash that cannot progress.
            if (_dashing || Time.timeScale <= 0f || _movement == null)
            {
                return;
            }

            bool usedCooldownCharge = false;
            if (_cooldownCharges > 0)
            {
                _cooldownCharges--;
                if (_cooldownCharges == 0)
                {
                    _cooldownRemaining = dashCooldown;
                }

                usedCooldownCharge = true;
            }
            else if (_instantAvailable <= 0)
            {
                return;
            }

            // Prefer the normal charge so its cooldown starts as early as possible. Instant charges
            // are not consumed permanently; they refill when the dash ends.
            if (!usedCooldownCharge)
            {
                _instantAvailable--;
            }

            Vector2 facing = IsometricUtility.ScreenDirectionToWorld(_movement.Facing);
            _dashing = true;
            _dashRemaining = Mathf.Max(0.01f, dashDuration);
            _movement.BeginDash(facing, dashSpeed);

            IgnoreEnemyCollisions();
            if (health != null)
            {
                health.SetDamageImmune(true);
            }
        }

        private void EndDash()
        {
            _dashing = false;
            _dashRemaining = 0f;
            _movement.EndDash();
            RestoreCollisions();

            // Instant charges refill after every dash, so one granted charge means one free dash
            // is always available between normal cooldown recharges.
            _instantAvailable = _grantedInstantCharges;

            if (invulnerabilityGrace <= 0f)
            {
                _graceRemaining = 0f;
                if (health != null)
                {
                    health.SetDamageImmune(false);
                }
            }
            else
            {
                _graceRemaining = invulnerabilityGrace;
            }
        }

        private void IgnoreEnemyCollisions()
        {
            if (bodyCollider == null || enemyRegistry == null)
            {
                return;
            }

            IReadOnlyList<Enemy> enemies = enemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                Collider2D collider = enemy.BodyCollider;
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(bodyCollider, collider, true);
                _ignoredColliders.Add(collider);
            }
        }

        private void RestoreCollisions()
        {
            if (bodyCollider == null)
            {
                _ignoredColliders.Clear();
                return;
            }

            for (int i = 0; i < _ignoredColliders.Count; i++)
            {
                Collider2D collider = _ignoredColliders[i];
                if (collider != null)
                {
                    Physics2D.IgnoreCollision(bodyCollider, collider, false);
                }
            }

            _ignoredColliders.Clear();
        }

        /// <summary>Clears dash, cooldown and pass-through state, e.g. on a stage teleport.</summary>
        private void Cancel()
        {
            if (_dashing)
            {
                _dashing = false;
                _dashRemaining = 0f;
                if (_movement != null)
                {
                    _movement.EndDash();
                }
            }

            RestoreCollisions();
            _cooldownCharges = BaseCharges;
            _cooldownRemaining = 0f;
            _instantAvailable = _grantedInstantCharges;
            _graceRemaining = 0f;

            if (health != null)
            {
                health.SetDamageImmune(false);
            }
        }
    }
}
