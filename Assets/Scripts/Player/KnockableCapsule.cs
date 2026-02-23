using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// MVP "ragdoll": temporarily disables CharacterController and enables a Rigidbody tumble.
    /// 
    /// Netcode note:
    /// - Server triggers the knock, but physics is simulated locally on each client for now.
    ///   (Good enough for slop prototyping; can move to server-authoritative later.)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class KnockableCapsule : NetworkBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float defaultKnockSeconds = 1.6f;
        [Tooltip("If true, the recovery timer starts only after the capsule first touches ground.")]
        [SerializeField] private bool startTimerOnGroundContact = true;
        [SerializeField] private float groundCheckDistance = 0.25f;

        [Header("Refs")]
        [SerializeField] private MonoBehaviour[] disableWhileKnocked;

        private CharacterController cc;
        private Rigidbody rb;
        private CapsuleCollider col;

        public event Action OnKnocked;
        public event Action OnRecovered;

        private float knockedUntil;
        private float pendingDuration;
        private bool knocked;
        private bool groundedDuringKnock;

        private float baseDrag;
        private float baseAngularDrag;
        private float savedYaw;

        [Header("Physics (knocked)")]
        [SerializeField] private float knockedDrag = 2.0f;
        [SerializeField] private float knockedAngularDrag = 6.0f;
        [SerializeField] private float maxAngularVelocity = 14f;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();

            // Rigidbody defaults for character tumble.
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxAngularVelocity = maxAngularVelocity;

            baseDrag = rb.linearDamping;
            baseAngularDrag = rb.angularDamping;

            // Use CapsuleCollider for physics; keep it disabled while controlled.
            col.enabled = false;
        }

        private void Update()
        {
            if (!knocked) return;

            if (startTimerOnGroundContact && !groundedDuringKnock)
            {
                if (!IsGroundedForKnock())
                {
                    // Don't recover mid-air.
                    return;
                }

                groundedDuringKnock = true;
                knockedUntil = Time.time + pendingDuration;
            }

            if (Time.time >= knockedUntil)
            {
                Recover();
            }
        }

        private void SetKnocked(bool value)
        {
            knocked = value;

            // Disable controller + enable physics collider
            cc.enabled = !value;
            col.enabled = value;

            rb.isKinematic = !value;
            rb.useGravity = value;

            if (value)
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

            if (disableWhileKnocked != null)
            {
                foreach (var b in disableWhileKnocked)
                {
                    if (b == null) continue;
                    // Never disable this component, otherwise it can't recover.
                    if (ReferenceEquals(b, this)) continue;
                    b.enabled = !value;
                }
            }

            if (value) OnKnocked?.Invoke();
            else OnRecovered?.Invoke();
        }

        private void Recover()
        {
            // Stop motion.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Snap transform to rigidbody pose.
            transform.position = rb.position;
            // Restore stable yaw (pre-knock) so FPS doesn't inherit spin.
            transform.rotation = Quaternion.Euler(0f, savedYaw, 0f);

            SetKnocked(false);
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
            // Start knock locally.
            float dur = Mathf.Clamp(seconds, 0.2f, 10f);
            pendingDuration = dur;

            groundedDuringKnock = !startTimerOnGroundContact;
            knockedUntil = groundedDuringKnock ? (Time.time + dur) : float.PositiveInfinity;

            // Save a stable yaw to restore after recovery.
            savedYaw = transform.rotation.eulerAngles.y;

            SetKnocked(true);

            // Apply impulse.
            rb.AddForce(impulse, ForceMode.Impulse);
            rb.AddTorque(new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-3f, 3f)), ForceMode.Impulse);
        }

        private bool IsGroundedForKnock()
        {
            // Raycast down from rigidbody position.
            var origin = rb.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, out _, groundCheckDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        public bool IsKnocked() => knocked;
    }
}
