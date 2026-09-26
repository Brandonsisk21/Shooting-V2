using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>First-person mouse look plus scope zoom (FOV narrowing, no sway).</summary>
    public class PlayerLook : MonoBehaviour
    {
        public Transform body;
        public Transform pivot;
        public Camera view;
        public float sensitivity = 2f;
        [Tooltip("Unzoomed vertical field of view in degrees.")]
        public float baseFov = 60f;
        public float zoomTransitionSpeed = 25f;

        /// <summary>Current scope magnification; 1 = unzoomed. Set by whoever owns the weapons.</summary>
        public float Zoom { get; set; } = 1f;

        private float _pitch;

        private void Update()
        {
            if (view == null) return;
            float target = ZoomedFov(baseFov, Zoom);
            float t = 1f - Mathf.Exp(-zoomTransitionSpeed * Time.deltaTime);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, target, t);
        }

        public void Look(Vector2 mouseDelta)
        {
            // Scale by zoom so on-screen aim speed feels the same while scoped.
            float scale = sensitivity / Mathf.Max(1f, Zoom);
            body.Rotate(0f, mouseDelta.x * scale, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * scale, -89f, 89f);
            pivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void ResetPitch()
        {
            _pitch = 0f;
            pivot.localRotation = Quaternion.identity;
        }

        /// <summary>The FOV that magnifies the view by <paramref name="zoom"/>x.</summary>
        public static float ZoomedFov(float fov, float zoom)
        {
            if (zoom <= 1f) return fov;
            float halfTan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / zoom;
            return 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
        }
    }
}
