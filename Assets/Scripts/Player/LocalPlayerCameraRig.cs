using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DungeonGame.Player
{
    /// <summary>
    /// Creates a simple 3rd-person camera rig for the local player only.
    /// Use an optional camera prefab or let the rig create one with correct far clip and Volume support.
    /// 
    /// Controls:
    /// - Mouse: look
    /// </summary>
    public class LocalPlayerCameraRig : NetworkBehaviour
    {
        [Header("Camera")]
        [Tooltip("Optional. If set, this prefab is instantiated and used (Camera + URP data). Otherwise a camera is created with the settings below.")]
        [SerializeField] private GameObject cameraPrefab;
        [Tooltip("Far clip plane (used when no prefab; increase so sky/fog and distant meshes render).")]
        [SerializeField] private float farClipPlane = 2000f;
        [Tooltip("Field of view when creating camera from code.")]
        [SerializeField] private float fov = 70f;

        [Header("Rig")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private float distance = 4.0f;
        [SerializeField] private float height = 1.8f;
        [Tooltip("Shoulder offset (positive = to the right).")]
        [SerializeField] private float shoulderOffset = 0.6f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float minPitch = -70f;
        [SerializeField] private float maxPitch = 70f;

        private Camera cam;
        private float yaw;
        private float pitch;
        private readonly List<AudioListener> _disabledListeners = new List<AudioListener>();

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

            GameObject go;
            if (cameraPrefab != null)
            {
                go = Instantiate(cameraPrefab);
                cam = go.GetComponentInChildren<Camera>(true);
                if (cam == null) cam = go.GetComponent<Camera>();
                if (cam == null)
                {
                    Debug.LogError("[LocalPlayerCameraRig] cameraPrefab has no Camera component.", this);
                    Destroy(go);
                    return;
                }
            }
            else
            {
                go = new GameObject("LocalPlayerCamera");
                cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = farClipPlane;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = Color.black;
                EnsureURPCameraData(cam);
            }

            go.name = "LocalPlayerCamera";
            DontDestroyOnLoad(go);
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.farClipPlane = farClipPlane;
            EnsureURPCameraData(cam);
            if (cam.gameObject.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            DisableOtherAudioListeners(cam.gameObject);

            // Initialize orientation
            var e = transform.rotation.eulerAngles;
            yaw = e.y;
            pitch = 10f;

            // Lock cursor for mouse look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            ApplySceneBackground();
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            ApplySceneBackground();
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

            // Shoulder camera: offset to the right in camera space.
            var right = rot * Vector3.right;
            var camPos = targetPos - (rot * Vector3.forward) * distance + right * shoulderOffset;

            cam.transform.SetPositionAndRotation(camPos, rot);
        }

        private void ApplySceneBackground()
        {
            if (!IsOwner) return;
            if (cam == null) return;

            var sceneName = SceneManager.GetActiveScene().name;
            bool black = sceneName == "Spire_Slice";

            cam.clearFlags = black ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;

            // Only nuke the skybox in the spire.
            if (black)
            {
                RenderSettings.skybox = null;
            }
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (IsOwner)
            {
                ReenableDisabledAudioListeners();
                if (cam != null) Destroy(cam.gameObject);
            }

            base.OnNetworkDespawn();
        }

        private void DisableOtherAudioListeners(GameObject ourCameraObject)
        {
            _disabledListeners.Clear();
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (listener.gameObject == ourCameraObject) continue;
                if (listener.enabled)
                {
                    listener.enabled = false;
                    _disabledListeners.Add(listener);
                }
            }
        }

        private void ReenableDisabledAudioListeners()
        {
            foreach (var listener in _disabledListeners)
            {
                if (listener != null) listener.enabled = true;
            }
            _disabledListeners.Clear();
        }

        private static void EnsureURPCameraData(Camera camera)
        {
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1; // Default layer so Global Fog / Volumes apply
        }
    }
}
