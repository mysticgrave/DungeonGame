using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Drives the character Animator for locomotion: sets a "Speed" float (0 = idle, ~0.5 = walk, 1 = run).
    /// Uses move input from FirstPersonMotor when available so animation starts/stops with key press; otherwise falls back to velocity.
    /// Disable Apply Root Motion on the Animator to prevent the character drifting or walking away from the camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotionAnimator : NetworkBehaviour
    {
        [Tooltip("Optional. Leave empty — Animator is auto-found on this object, children, or parent.")]
        [SerializeField] private Animator animator;

        [Tooltip("Sprint speed (match FirstPersonMotor). Only used when driving from velocity (no motor).")]
        [SerializeField] private float sprintSpeed = 7.5f;

        [Tooltip("Animator parameter name for movement speed (0 = idle, 0.5 = walk, 1 = run).")]
        [SerializeField] private string speedParamName = "Speed";

        [Tooltip("When true, Speed is driven by input (instant start/stop). When false, uses CharacterController velocity (slight delay).")]
        [SerializeField] private bool useInputForSpeed = true;

        private CharacterController _cc;
        private FirstPersonMotor _motor;
        private PlayerBodyStateMachine _bodyState;
        private int _speedParamId;
        private bool? _speedParamValid; // null = not checked yet, true/false = cached

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _motor = GetComponent<FirstPersonMotor>();
            _bodyState = GetComponent<PlayerBodyStateMachine>();
            CacheAnimator();
            _speedParamId = Animator.StringToHash(speedParamName);
        }

        private void CacheAnimator()
        {
            if (animator != null) return;
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) animator = GetComponentInParent<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) enabled = false;
        }

        private void LateUpdate()
        {
            if (animator == null) CacheAnimator();
            if (animator == null || !animator.enabled) return;

            if (!_speedParamValid.HasValue)
            {
                _speedParamValid = HasSpeedParameter();
                if (!_speedParamValid.Value)
                    Debug.LogWarning($"[PlayerLocomotionAnimator] Animator has no Float parameter named \"{speedParamName}\". Add it in the Animator Controller for Idle/Walk/Run. See LOCOMOTION_ANIMATOR.md.");
            }

            if (!_speedParamValid.Value) return;

            // Only drive locomotion when we can move (Standing). Stunned/Frozen/Ragdoll → idle.
            if (_bodyState != null && _bodyState.IsMovementDisabled)
            {
                animator.SetFloat(_speedParamId, 0f);
                return;
            }

            float normalized;
            if (useInputForSpeed && _motor != null)
            {
                float moveInput = _motor.GetMoveInputMagnitude();
                bool sprint = _motor.IsSprinting();
                normalized = moveInput > 0f ? (sprint ? 1f : Mathf.Lerp(0.5f, 0.65f, moveInput)) : 0f;
            }
            else
            {
                Vector3 horizontalVel = _cc.velocity;
                horizontalVel.y = 0f;
                float magnitude = horizontalVel.magnitude;
                normalized = sprintSpeed > 0 ? Mathf.Clamp01(magnitude / sprintSpeed) : 0f;
            }

            animator.SetFloat(_speedParamId, normalized);
        }

        private bool HasSpeedParameter()
        {
            if (animator?.runtimeAnimatorController == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == _speedParamId && p.type == AnimatorControllerParameterType.Float)
                    return true;
            }
            return false;
        }
    }
}
