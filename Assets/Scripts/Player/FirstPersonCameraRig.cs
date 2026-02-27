using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DungeonGame.Player
{
    /// <summary>
    /// FPS camera normally; switches to a detached follow camera when knocked.
    /// Owner-only. Use an optional camera prefab (with URP settings) or let the rig create one with correct far clip and Volume support.
    /// </summary>
    public class FirstPersonCameraRig : NetworkBehaviour
    {
        [Header("Camera")]
        [Tooltip("Optional. If set, this prefab is instantiated and used (Camera + URP data). Otherwise a camera is created with the settings below.")]
        [SerializeField] private GameObject cameraPrefab;
        [Tooltip("Far clip plane (used when no prefab; increase so sky/fog and distant meshes render).")]
        [SerializeField] private float farClipPlane = 2000f;
        [Tooltip("Field of view when creating camera from code.")]
        [SerializeField] private float fov = 70f;

        [Header("FPS")]
        [Tooltip("Height of the camera above the player root (feet).")]
        [SerializeField] private float eyeHeight = 1.65f;
        [Tooltip("Forward/back offset in player's look direction. Negative = pull camera back (closer to head).")]
        [SerializeField] private float forwardOffset = 0f;
        [Tooltip("Left/right offset. Positive = right, negative = left.")]
        [SerializeField] private float rightOffset = 0f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Knock Follow (3rd person when ragdolling)")]
        [SerializeField] private float followDistance = 4.0f;
        [SerializeField] private float followHeight = 1.6f;
        [SerializeField] private float followSmooth = 12f;

        [Header("Hide own body in first-person")]
        [Tooltip("Layer name used to hide the local player's body from their camera (create in Tags & Layers if missing).")]
        [SerializeField] private string localPlayerCullLayerName = "PlayerLocalCull";
        [Tooltip("Layer for FPS arms (ensure camera renders it). Create in Tags & Layers.")]
        [SerializeField] private string fpsArmsLayerName = "FPSArms";
        [Tooltip("Body/mesh to put on the cull layer so you don't see inside it. If null, uses RagdollRoot or the first SkinnedMeshRenderer's transform.")]
        [SerializeField] private Transform bodyToHideFromSelf;

        private Camera cam;
        private int _cullLayer = -1;
        private readonly List<Camera> _disabledSceneCameras = new();
        private Transform _bodyRoot;
        private const int DefaultLayer = 0;
        private float yaw;
        private float pitch;
        private bool followMode;
        private float followYaw;
        private float followPitch;

        private KnockableCapsule knock;
        private RagdollColliderSwitch ragdollSwitch;
        private DungeonGame.UI.CrosshairUI crosshair;
        private readonly List<AudioListener> _disabledListeners = new List<AudioListener>();

        /// <summary>Current look pitch in degrees. Used by UpperBodyLookSync to rotate spine/head for other players.</summary>
        public float Pitch => pitch;

        /// <summary>Runtime camera created for the local player. Null until OnNetworkSpawn completes. Use this instead of Camera.main so FPS arms attach to the correct camera.</summary>
        public Camera Camera => cam;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) return;

            knock = GetComponent<KnockableCapsule>();
            ragdollSwitch = GetComponent<RagdollColliderSwitch>();
            crosshair = GetComponent<DungeonGame.UI.CrosshairUI>();

            if (ragdollSwitch != null)
            {
                ragdollSwitch.OnRagdollEntered += EnterFollow;
                ragdollSwitch.OnRagdollExited += ExitFollow;
            }
            else if (knock != null)
            {
                knock.OnKnocked += EnterFollow;
                knock.OnRecovered += ExitFollow;
            }

            GameObject go;
            if (cameraPrefab != null)
            {
                go = Instantiate(cameraPrefab);
                cam = go.GetComponentInChildren<Camera>(true);
                if (cam == null) cam = go.GetComponent<Camera>();
                if (cam == null)
                {
                    Debug.LogError("[FirstPersonCameraRig] cameraPrefab has no Camera component.", this);
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
            DisableOtherMainCameras(cam);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.farClipPlane = farClipPlane;
            EnsureURPCameraData(cam);
            if (cam.gameObject.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            DisableOtherAudioListeners(cam.gameObject);

            SetupHideOwnBody();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            yaw = transform.rotation.eulerAngles.y;
            pitch = 0f;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                if (ragdollSwitch != null)
                {
                    ragdollSwitch.OnRagdollEntered -= EnterFollow;
                    ragdollSwitch.OnRagdollExited -= ExitFollow;
                }
                else if (knock != null)
                {
                    knock.OnKnocked -= EnterFollow;
                    knock.OnRecovered -= ExitFollow;
                }

                SetBodyVisibleToSelf(true);
                ReenableDisabledAudioListeners();
                foreach (var c in _disabledSceneCameras)
                {
                    if (c != null) c.gameObject.SetActive(true);
                }
                _disabledSceneCameras.Clear();
                if (cam != null) Destroy(cam.gameObject);
            }
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (cam == null) return;

            if (Mouse.current != null)
            {
                var delta = Mouse.current.delta.ReadValue();

                if (!followMode)
                {
                    yaw += delta.x * lookSensitivity;
                    pitch -= delta.y * lookSensitivity;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                    // Yaw rotates the player.
                    transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
                else
                {
                    // Independent orbit while knocked.
                    followYaw += delta.x * lookSensitivity;
                    followPitch -= delta.y * lookSensitivity;
                    followPitch = Mathf.Clamp(followPitch, -20f, 80f);
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (cam == null) return;

            if (!followMode)
            {
                var rot = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                else fwd.Normalize();
                Vector3 right = transform.right;
                right.y = 0f;
                if (right.sqrMagnitude > 0.01f) right.Normalize();
                var pos = transform.position + Vector3.up * eyeHeight + fwd * forwardOffset + right * rightOffset;
                cam.transform.SetPositionAndRotation(pos, rot);
            }
            else
            {
                // Follow the ragdoll body using Rigidbody.position (immune to parent-transform drift).
                Vector3 targetPos = ragdollSwitch != null
                    ? ragdollSwitch.GetRagdollWorldPosition()
                    : transform.position;
                var target = targetPos + Vector3.up * followHeight;

                // Orbit camera around the knocked player independently of body spin.
                var orbitRot = Quaternion.Euler(followPitch, followYaw, 0f);
                var offset = orbitRot * new Vector3(0f, 0f, -followDistance);
                var desired = target + offset;
                cam.transform.position = Vector3.Lerp(cam.transform.position, desired, Time.deltaTime * followSmooth);

                var lookRot = Quaternion.LookRotation((target - cam.transform.position).normalized, Vector3.up);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lookRot, Time.deltaTime * followSmooth);
            }
        }

        private void EnterFollow()
        {
            if (ragdollSwitch == null || ragdollSwitch.RagdollRoot == null)
                Debug.LogWarning("[CameraRig] RagdollColliderSwitch or ragdollRoot is null — camera will stay at root instead of following ragdoll body.", this);

            followMode = true;
            followYaw = yaw;
            followPitch = 15f;
            if (crosshair != null) crosshair.SetVisible(false);
            SetBodyVisibleToSelf(true);
        }

        private void ExitFollow()
        {
            followMode = false;

            // Re-center FPS view to current player yaw; keep pitch clamped.
            yaw = transform.rotation.eulerAngles.y;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (crosshair != null) crosshair.SetVisible(true);
            SetBodyVisibleToSelf(false);
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

        private void SetupHideOwnBody()
        {
            _cullLayer = LayerMask.NameToLayer(localPlayerCullLayerName);
            if (_cullLayer < 0)
            {
                Debug.LogWarning($"[FirstPersonCameraRig] Layer \"{localPlayerCullLayerName}\" not found. Create it in Edit → Project Settings → Tags and Layers. Body will not be hidden from local camera.", this);
                return;
            }

            cam.cullingMask &= ~(1 << _cullLayer);

            int fpsArmsLayer = LayerMask.NameToLayer(fpsArmsLayerName);
            if (fpsArmsLayer >= 0)
                cam.cullingMask |= (1 << fpsArmsLayer);

            // Prefer explicit assignment; otherwise move ALL SkinnedMeshRenderers (the visible body).
            // RagdollRoot often doesn't contain the mesh—it's just bones—so we target renderers directly.
            if (bodyToHideFromSelf != null)
            {
                SetBodyVisibleToSelf(false);
                return;
            }

            // Move every body mesh to the cull layer so we don't miss any (body, hair, armor, etc.).
            int count = 0;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                SetLayerRecursively(smr.transform, _cullLayer);
                count++;
            }
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
            {
                SetLayerRecursively(mr.transform, _cullLayer);
                count++;
            }
            if (count == 0)
            {
                _bodyRoot = transform;
                SetLayerRecursively(_bodyRoot, _cullLayer);
            }
        }

        private void SetBodyVisibleToSelf(bool visible)
        {
            if (_cullLayer < 0) return;
            int layer = visible ? DefaultLayer : _cullLayer;
            if (bodyToHideFromSelf != null)
            {
                SetLayerRecursively(bodyToHideFromSelf, layer);
                return;
            }
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                SetLayerRecursively(smr.transform, layer);
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                SetLayerRecursively(mr.transform, layer);
        }

        private void DisableOtherMainCameras(Camera keepThis)
        {
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (c != keepThis && c.CompareTag("MainCamera") && c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(false);
                    _disabledSceneCameras.Add(c);
                }
            }
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            if (t == null) return;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer);
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
