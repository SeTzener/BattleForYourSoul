using System;
using UnityEngine;

namespace BattleForYourSoul.VFX
{
    /// <summary>
    /// A pooled one-shot attack effect. Gameplay places it at the hit point; it scales, sweeps and
    /// fades for its lifetime, then returns itself to the pool. It owns no gameplay state and never
    /// looks up who fired it, so any weapon can reuse the same prefab.
    ///
    /// All values come from the firing <see cref="Weapons.WeaponActionDefinition"/>, so the actual
    /// motion is authored data rather than a hard-coded animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackVfx : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("How much larger the sprite gets over its lifetime.")]
        [SerializeField, Min(1f)] private float growth = 1.35f;

        private Action<AttackVfx> _release;
        private Vector3 _unitScale;
        private Color _color;
        private float _baseAngle;
        private float _sweepDegrees;
        private float _size;
        private float _elapsed;
        private float _duration;
        private bool _active;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _unitScale = transform.localScale;
        }

        /// <summary>Injected by the owning pool so the effect can return itself.</summary>
        public void SetReleaseCallback(Action<AttackVfx> release)
        {
            _release = release;
        }

        /// <summary>Starts one effect. Called immediately after the pool hands this instance out.</summary>
        public void Play(
            Vector2 position,
            Vector2 direction,
            Sprite sprite,
            Color tint,
            float size,
            float duration,
            float sweepDegrees)
        {
            transform.position = new Vector3(position.x, position.y, 0f);

            _baseAngle = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;
            _sweepDegrees = sweepDegrees;
            _color = tint;
            _size = Mathf.Max(0.01f, size);
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _active = true;

            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }

            Apply(0f);
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            Apply(t);

            if (t >= 1f)
            {
                Release();
            }
        }

        private void Apply(float t)
        {
            float scale = _size * Mathf.Lerp(1f, growth, t);
            transform.localScale = _unitScale * scale;

            float angle = _baseAngle + _sweepDegrees * (t - 0.5f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (spriteRenderer != null)
            {
                Color c = _color;
                c.a *= 1f - t;
                spriteRenderer.color = c;
            }
        }

        private void Release()
        {
            _active = false;
            _release?.Invoke(this);
        }

        private void OnDisable()
        {
            _active = false;
        }
    }
}
