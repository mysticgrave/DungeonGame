using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// Handles dodge/roll input. Owner-only.
    /// Left Shift + movement direction = dodge in that direction.
    /// Left Shift with no movement = dodge backward (relative to camera).
    /// Sends RPC to server which triggers the body state machine.
    /// </summary>
    public class PlayerDodge : NetworkBehaviour
    {
        private PlayerBodyStateMachine _bodyState;
        private Animator _animator;
        private ThirdPersonMotor _motor;
        private Transform _cameraTransform;

        private static readonly int DodgeTrigger = Animator.StringToHash("Dodge");
        private bool _hasDodgeTrigger;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _bodyState = GetComponent<PlayerBodyStateMachine>();
            _animator = GetComponentInChildren<Animator>(true);
            _motor = GetComponent<ThirdPersonMotor>();

            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;

            _hasDodgeTrigger = _animator != null && WeaponController.HasTriggerParameter(_animator, DodgeTrigger);
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_bodyState == null) return;
            if (_bodyState.CurrentState != PlayerBodyStateMachine.BodyState.Standing) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (!kb.leftShiftKey.wasPressedThisFrame) return;

            // Check cooldown client-side to avoid unnecessary RPCs
            if (Time.time < _bodyState.LastDodgeTime + _bodyState.DodgeCooldown) return;

            Vector3 dodgeDir = GetDodgeDirection();

            // Execute dodge locally on owner (owner-authoritative movement)
            _bodyState.EnterDodge(dodgeDir);

            // Play animation locally for responsiveness
            if (_hasDodgeTrigger)
                _animator.SetTrigger(DodgeTrigger);

            // Notify server for validation and syncing animation to other clients
            DodgeServerRpc(dodgeDir);
        }

        private Vector3 GetDodgeDirection()
        {
            var kb = Keyboard.current;
            float h = 0f, v = 0f;
            if (kb.wKey.isPressed) v += 1f;
            if (kb.sKey.isPressed) v -= 1f;
            if (kb.aKey.isPressed) h -= 1f;
            if (kb.dKey.isPressed) h += 1f;

            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;

            if (_cameraTransform == null)
                return -transform.forward; // fallback: dodge backward

            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 dir = camForward * v + camRight * h;

            // No input — dodge backward relative to camera
            if (dir.sqrMagnitude < 0.01f)
                dir = -camForward;

            dir.y = 0f;
            return dir.normalized;
        }

        [Rpc(SendTo.Server)]
        private void DodgeServerRpc(Vector3 direction)
        {
            // Sync animation to non-owner clients
            PlayDodgeAnimClientRpc();
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayDodgeAnimClientRpc()
        {
            if (_hasDodgeTrigger)
                _animator.SetTrigger(DodgeTrigger);
        }
    }
}
