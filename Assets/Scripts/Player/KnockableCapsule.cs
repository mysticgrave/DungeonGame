using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Handles knock/ragdoll RPC and movement disable. When PlayerBodyStateMachine is present and ragdoll is set up,
    /// delegates to the state machine; otherwise uses legacy timer + ragdoll logic (no state machine).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class KnockableCapsule : NetworkBehaviour
    {
        [Header("Legacy ragdoll (only if no PlayerBodyStateMachine)")]
        [SerializeField] private float defaultKnockSeconds = 4f;
        [SerializeField] private bool startTimerOnGroundContact = true;
        [SerializeField] private float maxKnockSeconds = 8f;
        [SerializeField] private float groundCheckDistance = 0.25f;

        [Header("Refs")]
        [SerializeField] private MonoBehaviour[] disableWhileKnocked;

        [Header("Physics (legacy / non-ragdoll tumble)")]
        [SerializeField] private float knockedDrag = 2.0f;
        [SerializeField] private float knockedAngularDrag = 6.0f;
        [SerializeField] private float maxAngularVelocity = 14f;

        private CharacterController cc;
        private Rigidbody rb;
        private CapsuleCollider col;
        private RagdollColliderSwitch ragdollSwitch;
        private PlayerBodyStateMachine stateMachine;

        public event Action OnKnocked;
        public event Action OnRecovered;

        // Legacy path (when no state machine, or no ragdoll)
        private float knockedUntil;
        private float pendingDuration;
        private float knockedAt;
        private bool knocked;
        private bool groundedDuringKnock;
        private float baseDrag, baseAngularDrag, savedYaw;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();
            ragdollSwitch = GetComponent<RagdollColliderSwitch>();
            stateMachine = GetComponent<PlayerBodyStateMachine>();

            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxAngularVelocity = maxAngularVelocity;
            baseDrag = rb.linearDamping;
            baseAngularDrag = rb.angularDamping;
            col.enabled = false;
        }

        private void Update()
        {
            if (stateMachine != null && ragdollSwitch != null) return; // state machine drives ragdoll timer
            if (!knocked) return;

            if (startTimerOnGroundContact && !groundedDuringKnock)
            {
                if (!IsGroundedForKnock())
                {
                    if (Time.time >= knockedAt + maxKnockSeconds)
                    {
                        RecoverLegacy();
                        return;
                    }
                    return;
                }
                groundedDuringKnock = true;
                knockedUntil = Time.time + pendingDuration;
            }

            if (Time.time >= knockedUntil || Time.time >= knockedAt + maxKnockSeconds)
                RecoverLegacy();
        }

        /// <summary>Called by PlayerBodyStateMachine when entering/exiting Ragdoll. Disables movement and fires OnKnocked/OnRecovered. </summary>
        public void SetKnockedFromStateMachine(bool value)
        {
            SetMovementDisabled(value);
            if (value) OnKnocked?.Invoke();
            else OnRecovered?.Invoke();
        }

        /// <summary>Called by PlayerBodyStateMachine when entering Stunned/Frozen (or Standing from those). Only toggles CC and disableWhileKnocked list. </summary>
        public void SetMovementDisabled(bool disabled)
        {
            cc.enabled = !disabled;

            if (ragdollSwitch == null)
            {
                col.enabled = disabled;
                rb.isKinematic = !disabled;
                rb.useGravity = disabled;
                if (disabled)
                {
                    rb.linearDamping = knockedDrag;
                    rb.angularDamping = knockedAngularDrag;
                    rb.maxAngularVelocity = maxAngularVelocity;
                }
                else
                {
                    rb.linearDamping = baseDrag;
                    rb.angularDamping = baseAngularDrag;
                    rb.maxAngularVelocity = maxAngularVelocity;
                }
            }

            if (disableWhileKnocked != null)
            {
                foreach (var b in disableWhileKnocked)
                {
                    if (b == null || ReferenceEquals(b, this)) continue;
                    b.enabled = !disabled;
                }
            }
        }

        private void SetKnockedLegacy(bool value)
        {
            knocked = value;
            SetMovementDisabled(value);
            if (value) OnKnocked?.Invoke();
            else OnRecovered?.Invoke();
        }

        private void RecoverLegacy()
        {
            if (ragdollSwitch != null)
            {
                bool ccWasEnabled = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;

                ragdollSwitch.SnapRootToRagdoll();
                SnapRootToGround();
                transform.rotation = Quaternion.Euler(0f, savedYaw, 0f);
                ragdollSwitch.SetStanding();

                if (cc != null) cc.enabled = ccWasEnabled;
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                transform.position = rb.position;
                transform.rotation = Quaternion.Euler(0f, savedYaw, 0f);
            }
            SetKnockedLegacy(false);
        }

        private void SnapRootToGround()
        {
            if (cc == null) return;

            float halfHeight = cc.height * 0.5f;
            float bottomOffset = cc.center.y - halfHeight;

            float castHeight = 50f;
            Vector3 origin = new Vector3(transform.position.x, transform.position.y + castHeight, transform.position.z);
            float maxDist = castHeight + Mathf.Abs(bottomOffset) + 10f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
            {
                float groundY = hit.point.y;
                float targetY = groundY - bottomOffset + 0.05f;
                transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            }
        }

        /// <summary>Server-only. Call from traps/hazards (e.g. spinning object) when they hit a player. Triggers ragdoll on all clients.</summary>
        public void KnockFromServer(Vector3 impulse, float durationSeconds = -1f)
        {
            if (!IsServer) return;
            float dur = durationSeconds > 0 ? durationSeconds : defaultKnockSeconds;
            KnockClientRpc(impulse, dur);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void KnockRpc(Vector3 impulse, float seconds = -1f)
        {
            float dur = seconds > 0 ? seconds : defaultKnockSeconds;
            KnockClientRpc(impulse, dur);
        }

        [Rpc(SendTo.Everyone)]
        private void KnockClientRpc(Vector3 impulse, float seconds)
        {
            float dur = Mathf.Clamp(seconds, 0.2f, 10f);

            if (stateMachine != null && ragdollSwitch != null)
            {
                stateMachine.EnterRagdoll(impulse, dur);
                return;
            }

            // Legacy: no state machine or no ragdoll
            pendingDuration = dur;
            knockedAt = Time.time;
            groundedDuringKnock = !startTimerOnGroundContact;
            knockedUntil = groundedDuringKnock ? (Time.time + dur) : float.PositiveInfinity;
            savedYaw = transform.rotation.eulerAngles.y;

            if (ragdollSwitch != null)
                ragdollSwitch.SetRagdoll(impulse);
            SetKnockedLegacy(true);

            if (ragdollSwitch == null)
            {
                rb.AddForce(impulse, ForceMode.Impulse);
                rb.AddTorque(new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-3f, 3f)), ForceMode.Impulse);
            }
        }

        private bool IsGroundedForKnock()
        {
            Vector3 origin = (ragdollSwitch != null)
                ? ragdollSwitch.GetRagdollWorldPosition() + Vector3.up * 0.1f
                : rb.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, out _, groundCheckDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        public bool IsKnocked()
        {
            if (stateMachine != null)
                return stateMachine.IsRagdoll;
            return knocked;
        }
    }
}
