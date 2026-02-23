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
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class KnockableCapsule : NetworkBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float defaultKnockSeconds = 1.6f;

        [Header("Refs")]
        [SerializeField] private MonoBehaviour[] disableWhileKnocked;

        private CharacterController cc;
        private Rigidbody rb;
        private CapsuleCollider col;

        private float knockedUntil;
        private bool knocked;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();

            // Rigidbody defaults for character tumble.
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Use CapsuleCollider for physics; keep it disabled while controlled.
            col.enabled = false;
        }

        private void Update()
        {
            if (!knocked) return;

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

            if (disableWhileKnocked != null)
            {
                foreach (var b in disableWhileKnocked)
                {
                    if (b == null) continue;
                    b.enabled = !value;
                }
            }
        }

        private void Recover()
        {
            // Stop motion.
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Snap transform to rigidbody pose.
            transform.position = rb.position;
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

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
            knockedUntil = Time.time + Mathf.Clamp(seconds, 0.2f, 10f);
            SetKnocked(true);

            // Apply impulse.
            rb.AddForce(impulse, ForceMode.Impulse);
            rb.AddTorque(new Vector3(Random.Range(-3f, 3f), Random.Range(-2f, 2f), Random.Range(-3f, 3f)), ForceMode.Impulse);
        }

        public bool IsKnocked() => knocked;
    }
}
