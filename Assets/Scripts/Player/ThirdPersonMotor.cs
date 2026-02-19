using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// Minimal third-person movement for MVP.
    /// Owner-only: reads local input and moves a CharacterController.
    /// 
    /// Networking:
    /// - Requires a NetworkTransform on the same GameObject.
    /// - Set NetworkTransform authority to Owner/Client in the inspector,
    ///   otherwise the server will correct the client's movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonMotor : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float sprintSpeed = 8.0f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.2f;

        [Header("Rotation")]
        [Tooltip("If true, player yaw follows the local camera yaw even while standing still.")]
        [SerializeField] private bool faceCameraYaw = true;

        [SerializeField] private Transform lookYawRoot; // rotates player yaw
        [SerializeField] private float yawDegreesPerSecond = 720f;

        [SerializeField] private LocalPlayerCameraRig cameraRig;

        private CharacterController cc;
        private float verticalVel;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (lookYawRoot == null) lookYawRoot = transform;
            if (cameraRig == null) cameraRig = GetComponent<LocalPlayerCameraRig>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                // Non-owner: do not read input.
                enabled = false;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null) return;

            var move = ReadMove();
            bool sprint = Keyboard.current.leftShiftKey.isPressed;
            bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;

            float speed = sprint ? sprintSpeed : moveSpeed;

            // Convert input into world-space relative to camera (if exists)
            var cam = Camera.main;
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cam != null)
            {
                forward = cam.transform.forward;
                right = cam.transform.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();
            }

            Vector3 planar = (forward * move.y + right * move.x);
            if (planar.sqrMagnitude > 1f) planar.Normalize();

            // Rotation feel:
            // - Default: face camera yaw (New World-ish) even while idle.
            // - When moving, still uses movement direction, but that direction is camera-relative.
            if (faceCameraYaw && cameraRig != null)
            {
                var targetRot = Quaternion.Euler(0f, cameraRig.Yaw, 0f);
                lookYawRoot.rotation = Quaternion.RotateTowards(
                    lookYawRoot.rotation,
                    targetRot,
                    yawDegreesPerSecond * Time.deltaTime);
            }
            else if (planar.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(planar, Vector3.up);
                lookYawRoot.rotation = Quaternion.RotateTowards(
                    lookYawRoot.rotation,
                    targetRot,
                    yawDegreesPerSecond * Time.deltaTime);
            }

            // Gravity + jump
            if (cc.isGrounded)
            {
                if (verticalVel < 0) verticalVel = -2f; // keep grounded

                if (jumpPressed)
                {
                    verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            verticalVel += gravity * Time.deltaTime;

            Vector3 velocity = planar * speed;
            velocity.y = verticalVel;

            cc.Move(velocity * Time.deltaTime);
        }

        private static Vector2 ReadMove()
        {
            float x = 0f;
            float y = 0f;

            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;

            var v = new Vector2(x, y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }
    }
}
