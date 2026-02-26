using System;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Central state machine for player body/status: Standing, Ragdoll, Stunned, Frozen, etc.
    /// Handles transitions, ragdoll timer, and notifies listeners. Add new states to the enum and handle them in Enter/Exit.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerBodyStateMachine : MonoBehaviour
    {
        public enum BodyState
        {
            Standing,
            Ragdoll,
            Stunned,
            Frozen,
            // Extend with more states as needed (e.g. GettingUp, Invulnerable)
        }

        [Header("Ragdoll config (used when entering Ragdoll)")]
        [SerializeField] private float defaultRagdollSeconds = 4f;
        [SerializeField] private bool startRagdollTimerOnGroundContact = true;
        [SerializeField] private float maxRagdollSeconds = 8f;
        [SerializeField] private float groundCheckDistance = 0.25f;

        [Header("Stunned / Frozen (placeholder)")]
        [Tooltip("Auto-return to Standing after this many seconds. 0 = no auto-exit.")]
        [SerializeField] private float defaultStunnedSeconds = 2f;
        [SerializeField] private float defaultFrozenSeconds = 3f;

        private BodyState _currentState = BodyState.Standing;
        private BodyState _previousState = BodyState.Standing;

        // Ragdoll timer (only used while state == Ragdoll)
        private float _ragdollUntil;
        private float _ragdollPendingDuration;
        private float _ragdollAt;
        private bool _ragdollGrounded;
        private float _savedYaw;
        private Vector3 _ragdollImpulse;

        // Stunned/Frozen timers
        private float _statusUntil;

        private CharacterController _cc;
        private RagdollColliderSwitch _ragdollSwitch;
        private KnockableCapsule _knockable;

        public BodyState CurrentState => _currentState;
        public BodyState PreviousState => _previousState;

        /// <summary>Fired when state changes. (newState, previousState) </summary>
        public event Action<BodyState, BodyState> OnStateChanged;

        /// <summary>True when the player cannot move (Ragdoll, Stunned, Frozen, etc.). </summary>
        public bool IsMovementDisabled => _currentState != BodyState.Standing;

        public bool IsRagdoll => _currentState == BodyState.Ragdoll;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _ragdollSwitch = GetComponent<RagdollColliderSwitch>();
            _knockable = GetComponent<KnockableCapsule>();
        }

        private void Start()
        {
            if (_currentState != BodyState.Ragdoll && _ragdollSwitch != null)
                _ragdollSwitch.SetStanding();
        }

        private void Update()
        {
            switch (_currentState)
            {
                case BodyState.Ragdoll:
                    UpdateRagdollTimer();
                    break;
                case BodyState.Stunned:
                case BodyState.Frozen:
                    UpdateStatusTimer();
                    break;
            }
        }

        /// <summary>
        /// Request transition to Ragdoll (e.g. from knock). durationSeconds &lt;= 0 uses defaultRagdollSeconds.
        /// </summary>
        public void EnterRagdoll(Vector3 impulse, float durationSeconds = -1f)
        {
            float dur = durationSeconds > 0 ? Mathf.Clamp(durationSeconds, 0.2f, 10f) : defaultRagdollSeconds;
            _ragdollImpulse = impulse;
            _ragdollPendingDuration = dur;
            _ragdollAt = Time.time;
            _ragdollGrounded = !startRagdollTimerOnGroundContact;
            _ragdollUntil = _ragdollGrounded ? (Time.time + dur) : float.PositiveInfinity;
            _savedYaw = transform.rotation.eulerAngles.y;

            if (_ragdollSwitch != null)
                _ragdollSwitch.SetRagdoll(impulse);
            else
            {
                // No ragdoll: still go to "Ragdoll" state and use fallback timer (KnockableCapsule handles non-ragdoll physics).
                _ragdollUntil = Time.time + dur;
                _ragdollGrounded = true;
            }

            TransitionTo(BodyState.Ragdoll);
        }

        /// <summary>Request transition to Stunned. durationSeconds &lt;= 0 uses defaultStunnedSeconds. </summary>
        public void EnterStunned(float durationSeconds = -1f)
        {
            _statusUntil = durationSeconds > 0 ? (Time.time + durationSeconds) : (defaultStunnedSeconds > 0 ? Time.time + defaultStunnedSeconds : float.PositiveInfinity);
            TransitionTo(BodyState.Stunned);
        }

        /// <summary>Request transition to Frozen. durationSeconds &lt;= 0 uses defaultFrozenSeconds. </summary>
        public void EnterFrozen(float durationSeconds = -1f)
        {
            _statusUntil = durationSeconds > 0 ? (Time.time + durationSeconds) : (defaultFrozenSeconds > 0 ? Time.time + defaultFrozenSeconds : float.PositiveInfinity);
            TransitionTo(BodyState.Frozen);
        }

        /// <summary>Force transition to Standing (e.g. from gameplay or when timers expire). </summary>
        public void EnterStanding()
        {
            TransitionTo(BodyState.Standing);
        }

        public void TransitionTo(BodyState newState)
        {
            if (_currentState == newState) return;

            BodyState prev = _currentState;
            ExitState(prev);
            _previousState = prev;
            _currentState = newState;
            EnterState(newState, prev);
            OnStateChanged?.Invoke(newState, prev);
        }

        private void EnterState(BodyState state, BodyState from)
        {
            switch (state)
            {
                case BodyState.Standing:
                    if (from == BodyState.Ragdoll)
                        DoRagdollRecovery();
                    if (_ragdollSwitch != null && !_ragdollSwitch.IsRagdoll)
                        _ragdollSwitch.SetStanding();
                    if (_knockable != null)
                    {
                        _knockable.SetMovementDisabled(false);
                        if (from == BodyState.Ragdoll)
                            _knockable.SetKnockedFromStateMachine(false);
                    }
                    break;
                case BodyState.Ragdoll:
                    if (_knockable != null)
                        _knockable.SetKnockedFromStateMachine(true);
                    break;
                case BodyState.Stunned:
                case BodyState.Frozen:
                    if (_knockable != null)
                        _knockable.SetMovementDisabled(true);
                    break;
            }
        }

        private void ExitState(BodyState state)
        {
            switch (state)
            {
                case BodyState.Ragdoll:
                    // Recovery is done in EnterState(Standing) when from == Ragdoll
                    break;
                case BodyState.Stunned:
                case BodyState.Frozen:
                    if (_knockable != null)
                        _knockable.SetMovementDisabled(false);
                    break;
            }
        }

        private void DoRagdollRecovery()
        {
            if (_ragdollSwitch != null)
            {
                _ragdollSwitch.SnapRootToRagdoll();
                SnapRootToGround();
                transform.rotation = Quaternion.Euler(0f, _savedYaw, 0f);
                _ragdollSwitch.SetStanding();
            }
            // If no ragdoll, KnockableCapsule / non-ragdoll path would have handled physics; we just go Standing.
        }

        private void SnapRootToGround()
        {
            if (_cc == null) return;
            float bottomY = transform.position.y + _cc.center.y - _cc.height * 0.5f;
            float rayStartY = transform.position.y + Mathf.Max(1f, _cc.height * 0.5f);
            Vector3 origin = new Vector3(transform.position.x, rayStartY, transform.position.z);
            float maxDist = rayStartY - bottomY + 2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
            {
                float groundY = hit.point.y;
                if (bottomY < groundY)
                    transform.position += Vector3.up * (groundY - bottomY);
            }
        }

        private void UpdateRagdollTimer()
        {
            if (startRagdollTimerOnGroundContact && !_ragdollGrounded)
            {
                if (!IsGroundedForRagdoll())
                {
                    if (Time.time >= _ragdollAt + maxRagdollSeconds)
                    {
                        EnterStanding();
                        return;
                    }
                    return;
                }
                _ragdollGrounded = true;
                _ragdollUntil = Time.time + _ragdollPendingDuration;
            }

            if (Time.time >= _ragdollUntil || Time.time >= _ragdollAt + maxRagdollSeconds)
                EnterStanding();
        }

        private bool IsGroundedForRagdoll()
        {
            Vector3 origin = (_ragdollSwitch != null)
                ? _ragdollSwitch.GetRagdollWorldPosition() + Vector3.up * 0.1f
                : transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, out _, groundCheckDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        private void UpdateStatusTimer()
        {
            if (_statusUntil < float.PositiveInfinity && Time.time >= _statusUntil)
                EnterStanding();
        }
    }
}
