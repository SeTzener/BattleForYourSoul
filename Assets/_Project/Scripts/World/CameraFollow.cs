using UnityEngine;

namespace BattleForYourSoul.World
{
    /// <summary>
    /// Smoothly follows a target in <c>LateUpdate</c> (after movement has been applied).
    ///
    /// It does not do pixel snapping: the Pixel Perfect Camera on the same GameObject handles that
    /// so the camera and the world stay on the pixel grid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Tooltip("Approximate time to catch up to the target. 0 = rigid follow.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        private Vector3 _velocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                transform.position.z);

            transform.position = smoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
