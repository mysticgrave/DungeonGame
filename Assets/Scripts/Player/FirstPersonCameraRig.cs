using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// FPS camera normally; switches to a detached follow camera when knocked.
    /// Owner-only.
    /// </summary>
    public class FirstPersonCameraRig : NetworkBehaviour
    {
        [Header("FPS")]
        [SerializeField] private float eyeHeight = 1.65f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Knock Follow")]
        [SerializeField] private float followDistance = 4.0f;
        [SerializeField] private float followHeight = 1.6f;
        [SerializeField] private float followSmooth = 12f;

        private Camera cam;
        private float yaw;
        private float pitch;
        private bool followMode;
        private float followYaw;
        private float followPitch;

        private KnockableCapsule knock;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) return;

            knock = GetComponent<KnockableCapsule>();
            if (knock != null)
            {
                knock.OnKnocked += EnterFollow;
                knock.OnRecovered += ExitFollow;
            }

            var go = new GameObject("LocalPlayerCamera");
            DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            go.AddComponent<AudioListener>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            yaw = transform.rotation.eulerAngles.y;
            pitch = 0f;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                if (knock != null)
                {
                    knock.OnKnocked -= EnterFollow;
                    knock.OnRecovered -= ExitFollow;
                }

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
                // Follow behind the knocked capsule.
                // Important: do NOT use transform.forward here (it can spin due to physics).
                var target = transform.position + Vector3.up * followHeight;

                // Orbit camera around the knocked player independently of body spin.
                var orbitRot = Quaternion.Euler(followPitch, followYaw, 0f);
                var offset = orbitRot * new Vector3(0f, 0f, -followDistance);
                offset.y += 0f; // pitch already contributes

                var desired = target + offset;
                cam.transform.position = Vector3.Lerp(cam.transform.position, desired, Time.deltaTime * followSmooth);

                var lookRot = Quaternion.LookRotation((target - cam.transform.position).normalized, Vector3.up);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lookRot, Time.deltaTime * followSmooth);
            }
        }

        private void EnterFollow()
        {
            followMode = true;
            followYaw = yaw;
            followPitch = 15f;
        }

        private void ExitFollow()
        {
            followMode = false;

            // Re-center FPS view to current player yaw; keep pitch clamped.
            yaw = transform.rotation.eulerAngles.y;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }
}
