using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.DevTools
{
    /// <summary>
    /// Orbit camera for the CharacterPreview scene.
    /// RMB drag to orbit, scroll to zoom, MMB drag to pan.
    /// </summary>
    public class PreviewOrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform to orbit around. If null, orbits around world origin.")]
        public Transform target;

        [Header("Orbit")]
        public float distance = 3f;
        public float minDistance = 0.5f;
        public float maxDistance = 10f;
        public float orbitSpeed = 0.3f;
        public float zoomSpeed = 0.3f;
        public float panSpeed = 0.5f;

        [Header("Initial Angles")]
        public float yaw = 180f;
        public float pitch = 10f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        private Vector3 _panOffset;

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 delta = mouse.delta.ReadValue();

            // RMB orbit
            if (mouse.rightButton.isPressed)
            {
                yaw += delta.x * orbitSpeed;
                pitch -= delta.y * orbitSpeed;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            // MMB pan
            if (mouse.middleButton.isPressed)
            {
                _panOffset -= transform.right * delta.x * panSpeed * distance * 0.001f;
                _panOffset -= transform.up * delta.y * panSpeed * distance * 0.001f;
            }

            // Scroll zoom — scroll.y returns ~120 per tick, normalize to ±1
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float normalizedScroll = Mathf.Sign(scroll) * Mathf.Min(Mathf.Abs(scroll) / 120f, 3f);
                distance -= normalizedScroll * zoomSpeed * distance;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            // Apply orbit
            Vector3 center = target != null ? target.position : Vector3.zero;
            center += _panOffset;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            transform.position = center + offset;
            transform.LookAt(center);
        }

        /// <summary>Reset camera to default position facing the target.</summary>
        public void ResetView()
        {
            yaw = 180f;
            pitch = 10f;
            distance = 3f;
            _panOffset = Vector3.zero;
        }

        /// <summary>Focus on a specific height (e.g. hand bone Y position).</summary>
        public void FocusOnHeight(float worldY)
        {
            _panOffset = new Vector3(0f, worldY - (target != null ? target.position.y : 0f), 0f);
            distance = 1.5f;
        }
    }
}
