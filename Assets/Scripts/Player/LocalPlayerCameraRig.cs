using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// Creates a simple 3rd-person camera rig for the local player only.
    /// Ensures exactly one AudioListener by only enabling it for the owner camera.
    /// 
    /// Controls:
    /// - Mouse: look
    /// - RMB: (optional) can be used later for aim
    /// </summary>
    public class LocalPlayerCameraRig : NetworkBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private float distance = 4.0f;
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float minPitch = -70f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Camera")]
        [SerializeField] private float fov = 70f;

        private Camera cam;
        private float yaw;
        private float pitch;

        public float Yaw => yaw;
        public Transform CameraTransform => cam != null ? cam.transform : null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                // No camera for non-owner.
                return;
            }

            if (followTarget == null) followTarget = transform;

            // Create camera object
            var go = new GameObject("LocalPlayerCamera");
            DontDestroyOnLoad(go);

            cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.tag = "MainCamera";

            // Pure black background (no skybox).
#if UNITY_6000_0_OR_NEWER
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
#else
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
#endif

            // Only the local camera gets an audio listener.
            go.AddComponent<AudioListener>();

            // Initialize orientation
            var e = transform.rotation.eulerAngles;
            yaw = e.y;
            pitch = 10f;

            // Ensure skybox doesn't reappear via scene lighting defaults.
            RenderSettings.skybox = null;

            // Lock cursor for mouse look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (cam == null) return;

            if (Mouse.current != null)
            {
                var delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * lookSensitivity;
                pitch -= delta.y * lookSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            // Build camera transform
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var targetPos = followTarget.position + Vector3.up * height;
            var camPos = targetPos - (rot * Vector3.forward) * distance;

            cam.transform.SetPositionAndRotation(camPos, rot);
        }

        public override void OnNetworkDespawn()
        {
            // If we owned the camera, clean it up.
            if (IsOwner && cam != null)
            {
                Destroy(cam.gameObject);
            }

            base.OnNetworkDespawn();
        }
    }
}
