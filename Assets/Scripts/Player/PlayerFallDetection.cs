using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// When Standing: tracks fall time and sets Animator "IsFalling" for a fall animation.
    /// On landing after a long enough fall, triggers ragdoll (no damage — ragdoll instead of fall damage).
    /// Add a Bool parameter "IsFalling" to your Animator and a Fall state that plays while true.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFallDetection : MonoBehaviour
    {
        [Header("Fall → Ragdoll on landing")]
        [Tooltip("Minimum time in the air (seconds) before landing triggers ragdoll. Below this, land normally.")]
        [SerializeField] private float fallRagdollThresholdSeconds = 1.2f;
        [Tooltip("Impulse applied when ragdolling from a hard landing (e.g. slight down + forward).")]
        [SerializeField] private Vector3 landingRagdollImpulse = new Vector3(0f, -2f, 0.5f);
        [Tooltip("Ragdoll duration after a hard landing. &lt;= 0 uses state machine default.")]
        [SerializeField] private float landingRagdollDuration = 3f;

        [Header("Fall animation")]
        [Tooltip("Animator Bool parameter set true while falling (after this many seconds in air). Add a Fall state in your Animator.")]
        [SerializeField] private string isFallingParamName = "IsFalling";
        [Tooltip("Start playing fall animation after this many seconds in the air (avoids flicker on small steps).")]
        [SerializeField] private float fallAnimationStartSeconds = 0.35f;

        [Header("Optional refs (auto-found if null)")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerBodyStateMachine stateMachine;

        private CharacterController _cc;
        private float _fallStartTime = -1f;
        private bool _wasGrounded = true;
        private int _isFallingParamId;
        private bool _isFallingParamValid;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerBodyStateMachine>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            _isFallingParamId = Animator.StringToHash(isFallingParamName);
        }

        private void Update()
        {
            if (stateMachine != null && stateMachine.IsMovementDisabled)
            {
                SetFallingBool(false);
                _fallStartTime = -1f;
                _wasGrounded = true;
                return;
            }

            bool grounded = _cc.isGrounded;

            if (_wasGrounded && !grounded)
                _fallStartTime = Time.time;

            if (grounded)
            {
                if (_fallStartTime >= 0f)
                {
                    float fallTime = Time.time - _fallStartTime;
                    if (fallTime >= fallRagdollThresholdSeconds && stateMachine != null)
                        stateMachine.EnterRagdoll(landingRagdollImpulse, landingRagdollDuration > 0 ? landingRagdollDuration : -1f);
                }
                _fallStartTime = -1f;
                SetFallingBool(false);
            }
            else
            {
                bool showFallAnim = _fallStartTime >= 0f && (Time.time - _fallStartTime) >= fallAnimationStartSeconds;
                SetFallingBool(showFallAnim);
            }

            _wasGrounded = grounded;
        }

        private void SetFallingBool(bool value)
        {
            if (animator == null || !animator.enabled) return;
            if (!_isFallingParamValid)
            {
                _isFallingParamValid = HasIsFallingParameter();
                if (!_isFallingParamValid) return;
            }
            animator.SetBool(_isFallingParamId, value);
        }

        private bool HasIsFallingParameter()
        {
            if (animator?.runtimeAnimatorController == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == _isFallingParamId && p.type == AnimatorControllerParameterType.Bool)
                    return true;
            }
            return false;
        }
    }
}
