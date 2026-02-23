using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// Minimal FPS movement using CharacterController.
    /// Owner-only.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonMotor : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.2f;

        private CharacterController cc;
        private float verticalVel;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) enabled = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null) return;

            var move = ReadMove();
            bool sprint = Keyboard.current.leftShiftKey.isPressed;
            bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;

            float speed = sprint ? sprintSpeed : moveSpeed;

            // Move relative to player forward/right
            Vector3 planar = (transform.forward * move.y + transform.right * move.x);
            if (planar.sqrMagnitude > 1f) planar.Normalize();

            if (cc.isGrounded)
            {
                if (verticalVel < 0) verticalVel = -2f;
                if (jumpPressed)
                {
                    verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            verticalVel += gravity * Time.deltaTime;

            Vector3 vel = planar * speed;
            vel.y = verticalVel;
            cc.Move(vel * Time.deltaTime);
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
