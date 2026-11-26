using System.Collections;
using UnityEngine;

namespace BattleForYourSoul.Superpowers
{
    /// <summary>
    /// Presentation-only attack poses. Sprites are swapped on a dedicated <em>visual child</em> and
    /// the hop is a local offset of that child. The physics root is never written to.
    ///
    /// This is deliberate: an earlier version wrote the hop to whatever renderer it found, which was
    /// the root SpriteRenderer. Every attack then pinned the Rigidbody's transform back to the
    /// position captured on the button press, producing the shove/jitter seen with every superpower.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuperpowerPosePlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Coroutine _routine;
        private Sprite _baseSprite;
        private Vector3 _baseVisualPosition;
        private bool _hasBase;
        private bool _invalid;

        private Transform VisualRoot => spriteRenderer != null ? spriteRenderer.transform : null;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = FindVisualRenderer();
            }

            // The visual must be a child. If it is the body root, do nothing rather than moving the
            // player's physics transform; a missing pose is much safer than a broken body.
            if (spriteRenderer == null || VisualRoot == transform)
            {
                Debug.LogError(
                    "[SuperpowerPosePlayer] No dedicated visual child with a SpriteRenderer. " +
                    "Attack poses are disabled until one is wired.", this);
                _invalid = true;
                return;
            }

            _baseSprite = spriteRenderer.sprite;
            _baseVisualPosition = VisualRoot.localPosition;
            _hasBase = true;
        }

        /// <summary>Plays one pose. A new pose interrupts and cleanly restores the previous one.</summary>
        public void Play(AttackAnimation animation, Vector2 direction)
        {
            if (_invalid || spriteRenderer == null)
            {
                return;
            }

            Stop();
            _baseSprite = spriteRenderer.sprite;
            _baseVisualPosition = VisualRoot.localPosition;
            _hasBase = true;
            _routine = StartCoroutine(PlayRoutine(animation, direction));
        }

        /// <summary>Restores the resting sprite and visual offset immediately.</summary>
        public void Stop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            Restore();
        }

        private IEnumerator PlayRoutine(AttackAnimation animation, Vector2 direction)
        {
            Sprite[] frames = animation.Frames;
            int frameCount = frames != null ? frames.Length : 0;
            float frameDuration = Mathf.Max(0.01f, animation.FrameDuration);
            float total = Mathf.Max(frameDuration * Mathf.Max(1, frameCount), animation.LungeDuration);

            float elapsed = 0f;
            int frame = 0;
            while (elapsed < total)
            {
                if (frameCount > 0)
                {
                    spriteRenderer.sprite = frames[Mathf.Min(frame, frameCount - 1)];
                }

                // Only the visual child moves. Never touch this component's own transform.
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, total));
                float hop = Mathf.Sin(t * Mathf.PI);
                VisualRoot.localPosition = _baseVisualPosition + new Vector3(
                    animation.HopOffset.x * hop, animation.HopOffset.y * hop, 0f);

                elapsed += frameDuration;
                frame++;
                yield return new WaitForSeconds(frameDuration);
            }

            Restore();
            _routine = null;
        }

        private void Restore()
        {
            if (!_hasBase || spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sprite = _baseSprite;
            VisualRoot.localPosition = _baseVisualPosition;
        }

        private SpriteRenderer FindVisualRenderer()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].transform != transform)
                {
                    return renderers[i];
                }
            }

            return null;
        }
    }
}
