using System.Collections.Generic;
using UnityEngine;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Core;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Superpowers;

namespace BattleForYourSoul.Player
{
    /// <summary>
    /// Run-scoped dash state: charges, cooldown and the brief pass-through window.
    ///
    /// Gameplay ownership is split on purpose: this component decides <em>when</em> a dash happens
    /// and handles enemies/immunity, <see cref="PlayerMovement"/> owns the body velocity while it
    /// lasts. With Charge taken, a dash also damages each enemy it passes through once. With Super
    /// Charge taken, holding the button charges damage up to 2x; a tap still dashes at 1x.
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
        [SerializeField] private PlayerStats stats;
        [SerializeField] private SuperpowerController superpowers;
        [SerializeField] private SuperpowerPosePlayer posePlayer;

        private readonly HashSet<Enemy> _dashHitEnemies = new HashSet<Enemy>();

        private IPlayerInput _input;
        private PlayerMovement _movement;
        private int _cooldownCharges = BaseCharges;
        private int _grantedInstantCharges;
        private int _instantAvailable;
        private float _cooldownRemaining;
        private float _dashRemaining;
        private float _graceRemaining;
        private float _chargeElapsed;
        private float _dashDamage;
        private float _dashDamageMultiplier = 1f;
        private bool _charging;
        private bool _dashing;

        public bool IsDashing => _dashing;
        public bool IsCharging => _charging;

        /// <summary>Charge progress from 0 to 1, for a future HUD meter.</summary>
        public float ChargeProgress
        {
            get
            {
                float duration = stats != null ? stats.CurrentModifiers.SuperChargeDuration : 0f;
                return duration > 0f ? Mathf.Clamp01(_chargeElapsed / duration) : 0f;
            }
        }

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
                _input.DashPressed += HandleDashPressed;
                _input.DashReleased += HandleDashReleased;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.DashPressed -= HandleDashPressed;
                _input.DashReleased -= HandleDashReleased;
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

            if (_charging)
            {
                if (Time.timeScale <= 0f)
                {
                    _charging = false;
                    _chargeElapsed = 0f;
                }
                else if (_input != null && _input.DashHeld)
                {
                    float duration = Mathf.Max(0.01f, stats != null ? stats.CurrentModifiers.SuperChargeDuration : 0.01f);
                    _chargeElapsed = Mathf.Min(duration, _chargeElapsed + Time.deltaTime);
                }
            }

            if (_dashing)
            {
                ApplyDashHits();
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
                    health.RemoveDamageImmunity(this);
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

        private void HandleDashPressed()
        {
            if (_dashing || Time.timeScale <= 0f || _movement == null)
            {
                return;
            }

            float duration = stats != null ? stats.CurrentModifiers.SuperChargeDuration : 0f;
            if (duration > 0f)
            {
                _charging = true;
                _chargeElapsed = 0f;
                return;
            }

            TryDash(1f);
        }

        private void HandleDashReleased()
        {
            if (!_charging)
            {
                return;
            }

            _charging = false;
            float duration = stats != null ? stats.CurrentModifiers.SuperChargeDuration : 0f;
            float progress = duration > 0f ? Mathf.Clamp01(_chargeElapsed / duration) : 0f;
            TryDash(Mathf.Lerp(1f, 2f, progress));
        }

        private void TryDash(float damageMultiplier)
        {
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
            _dashDamageMultiplier = damageMultiplier;
            _dashDamage = stats != null ? stats.CurrentModifiers.ChargeDamage : 0f;
            _dashHitEnemies.Clear();
            _movement.BeginDash(facing, dashSpeed);

            IgnoreEnemyCollisions();
            if (health != null)
            {
                health.AddDamageImmunity(this);
            }

            if (posePlayer != null && definition != null)
            {
                posePlayer.Play(definition.DashAnimation, facing);
            }
        }

        private void ApplyDashHits()
        {
            if (_dashDamage <= 0f || superpowers == null || enemyRegistry == null)
            {
                return;
            }

            SuperpowerModifiers modifiers = stats != null ? stats.CurrentModifiers : SuperpowerModifiers.Default;
            var effects = new HitEffects
            {
                KnockbackForce = modifiers.ChargeKnockbackForce,
                KnockbackDuration = modifiers.ChargeKnockbackDuration,
            };

            Vector2 origin = transform.position;
            IReadOnlyList<Enemy> enemies = enemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy || _dashHitEnemies.Contains(enemy))
                {
                    continue;
                }

                float radius = 0.65f + (enemy.BodyCollider != null ? enemy.BodyCollider.bounds.extents.x : 0f);
                if (((Vector2)enemy.transform.position - origin).sqrMagnitude > radius * radius)
                {
                    continue;
                }

                _dashHitEnemies.Add(enemy);
                superpowers.ApplyDashHit(enemy.Health, origin, _dashDamage * _dashDamageMultiplier, effects);
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
                    health.RemoveDamageImmunity(this);
                }
            }
            else
            {
                _graceRemaining = invulnerabilityGrace;
            }
        }

        private void IgnoreEnemyCollisions()
        {
            if (bodyCollider != null && enemyRegistry != null)
            {
                enemyRegistry.SuppressEnemyCollisions(this, bodyCollider);
            }
        }

        private void RestoreCollisions()
        {
            if (bodyCollider != null && enemyRegistry != null)
            {
                enemyRegistry.RestoreEnemyCollisions(this, bodyCollider);
            }
        }

        /// <summary>Clears dash, charge, cooldown and pass-through state, e.g. on a stage teleport.</summary>
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

                if (posePlayer != null)
                {
                    posePlayer.Stop();
                }
            }

            _charging = false;
            _chargeElapsed = 0f;
            _dashHitEnemies.Clear();
            RestoreCollisions();
            _cooldownCharges = BaseCharges;
            _cooldownRemaining = 0f;
            _instantAvailable = _grantedInstantCharges;
            _graceRemaining = 0f;

            if (health != null)
            {
                health.RemoveDamageImmunity(this);
            }
        }
    }
}
