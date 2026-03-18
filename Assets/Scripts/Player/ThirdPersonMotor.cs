using DungeonGame.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
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
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.2f;
        [Tooltip("Sub-step CharacterController movement on long frames to reduce FPS-coupled slowdown.")]
        [SerializeField] private bool useMovementSubsteps = true;
        [Tooltip("Max dt per CharacterController move step when sub-stepping.")]
        [SerializeField] private float maxSubstepDelta = 1f / 60f;
        [Tooltip("Safety cap for number of move substeps each frame.")]
        [SerializeField] private int maxSubsteps = 8;

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
                return;
            }

            // Local owner should not interpolate its own NetworkTransform; interpolation is for remotes.
            // Keeping this on can add perceived input/movement latency, especially under low FPS.
            var nt = GetComponent<NetworkTransform>();
            if (nt != null)
                nt.Interpolate = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null) return;
            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);

            bool paused = PauseMenuController.IsPaused;
            var move = paused ? Vector2.zero : ReadMove();
            bool jumpPressed = !paused && Keyboard.current.spaceKey.wasPressedThisFrame;

            float speed = moveSpeed;

            // Convert input into world-space relative to the *local* camera.
            // Do not rely on Camera.main in multiplayer; only the owner's camera should drive movement.
            Transform camT = null;
            if (cameraRig != null) camT = cameraRig.CameraTransform;
            if (camT == null && Camera.main != null) camT = Camera.main.transform;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (camT != null)
            {
                forward = camT.forward;
                right = camT.right;
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();
            }

            Vector3 planar = (forward * move.y + right * move.x);
            if (paused) planar = Vector3.zero;
            else if (planar.sqrMagnitude > 1f) planar.Normalize();

            // Rotation feel (skip when paused — no input)
            // - Default: face camera yaw (New World-ish) even while idle.
            // - When moving, still uses movement direction, but that direction is camera-relative.
            if (!paused && faceCameraYaw && cameraRig != null)
            {
                var targetRot = Quaternion.Euler(0f, cameraRig.Yaw, 0f);
                lookYawRoot.rotation = Quaternion.RotateTowards(
                    lookYawRoot.rotation,
                    targetRot,
                    yawDegreesPerSecond * dt);
            }
            else if (!paused && planar.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(planar, Vector3.up);
                lookYawRoot.rotation = Quaternion.RotateTowards(
                    lookYawRoot.rotation,
                    targetRot,
                    yawDegreesPerSecond * dt);
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

            verticalVel += gravity * dt;

            Vector3 velocity = planar * speed;
            velocity.y = verticalVel;

            if (cc.enabled)
                MoveCharacter(velocity, dt);
        }

        private void MoveCharacter(Vector3 velocity, float dt)
        {
            if (!useMovementSubsteps || dt <= maxSubstepDelta)
            {
                cc.Move(velocity * dt);
                return;
            }

            // Sub-step to avoid CharacterController missing collisions on long frames.
            // Total displacement is always velocity * dt regardless of step count.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / Mathf.Max(0.001f, maxSubstepDelta)), 1, Mathf.Max(1, maxSubsteps));
            Vector3 stepMove = velocity * (dt / steps);
            for (int i = 0; i < steps; i++)
                cc.Move(stepMove);
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

        public float GetMoveInputMagnitude()
        {
            if (PauseMenuController.IsPaused) return 0f;
            if (Keyboard.current == null) return 0f;
            var move = ReadMove();
            return Mathf.Clamp01(move.magnitude);
        }

        /// <summary>Set move speed at runtime (e.g. from class stats).</summary>
        public void SetMoveSpeed(float value)
        {
            moveSpeed = Mathf.Max(0.1f, value);
        }

        public bool IsSprinting()
        {
            // Sprint removed — Left Shift is now dodge. Kept for animator compatibility.
            return false;
        }

        public Vector2 GetMoveInput()
        {
            if (PauseMenuController.IsPaused) return Vector2.zero;
            if (Keyboard.current == null) return Vector2.zero;
            var v = ReadMove();
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }
    }
}
