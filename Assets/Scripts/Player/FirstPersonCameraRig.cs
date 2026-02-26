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
        [SerializeField] private float eyeHeight = 1.65f;
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
        [Tooltip("Body/mesh to put on the cull layer so you don't see inside it. If null, uses RagdollRoot or the first SkinnedMeshRenderer's transform.")]
        [SerializeField] private Transform bodyToHideFromSelf;

        private Camera cam;
        private int _cullLayer = -1;
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
                var pos = transform.position + Vector3.up * eyeHeight;
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
            if (_cullLayer < 0) return;

            cam.cullingMask &= ~(1 << _cullLayer);

            _bodyRoot = bodyToHideFromSelf != null ? bodyToHideFromSelf : (ragdollSwitch != null && ragdollSwitch.RagdollRoot != null ? ragdollSwitch.RagdollRoot : null);
            if (_bodyRoot == null)
            {
                var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null) _bodyRoot = smr.transform;
                else _bodyRoot = transform;
            }
            SetBodyVisibleToSelf(false);
        }

        private void SetBodyVisibleToSelf(bool visible)
        {
            if (_bodyRoot == null || _cullLayer < 0) return;
            SetLayerRecursively(_bodyRoot, visible ? DefaultLayer : _cullLayer);
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
