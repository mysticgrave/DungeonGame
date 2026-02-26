using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DungeonGame.Player;

namespace DungeonGame.Hazards
{
    /// <summary>
    /// Spins a transform and knocks players into ragdoll when they are inside the detection volume.
    /// Uses a per-frame overlap check on the server so fast spinning does not tunnel through (no clipping).
    /// Also works with TrapKnockOnContact (OnControllerColliderHit) as a fallback.
    /// </summary>
    public class SpinningKnockTrap : MonoBehaviour
    {
        [Header("Spin")]
        [SerializeField] private float spinSpeedDegreesPerSecond = 360f;
        [SerializeField] private Vector3 spinAxis = Vector3.up;
        [Tooltip("If set, this transform is rotated. Otherwise the GameObject this script is on is rotated.")]
        [SerializeField] private Transform spinPivot;

        [Header("Detection (avoids clipping at high speed)")]
        [Tooltip("Server checks this radius every FixedUpdate. Players inside are knocked. Prevents tunneling.")]
        [SerializeField] private float detectionRadius = 1.5f;
        [Tooltip("Layer mask for overlap (default = Everything). Restrict to player layer to avoid extra checks.")]
        [SerializeField] private LayerMask detectionLayers = -1;
        [Tooltip("Seconds before the same player can be knocked again by this trap.")]
        [SerializeField] private float perPlayerCooldownSeconds = 1f;

        [Header("Knock on contact")]
        [SerializeField] private float knockImpulseMagnitude = 12f;
        [Tooltip("Upward component added to the impulse so the player is launched.")]
        [SerializeField] private float knockUpwardBias = 2f;
        [SerializeField] private float knockDurationSeconds = 4f;
        [Tooltip("If true, impulse is from trap center toward the player. Otherwise uses the spinner's forward.")]
        [SerializeField] private bool impulseAwayFromSpinner = true;

        private Transform _pivot;
        private readonly Dictionary<ulong, float> _lastKnockByPlayer = new Dictionary<ulong, float>();
        private static readonly Collider[] _overlapBuffer = new Collider[16];

        private void Awake()
        {
            _pivot = spinPivot != null ? spinPivot : transform;
        }

        private void Update()
        {
            if (_pivot == null) return;
            _pivot.Rotate(spinAxis.normalized, spinSpeedDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        private void FixedUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (_pivot == null) return;

            int count = Physics.OverlapSphereNonAlloc(_pivot.position, detectionRadius, _overlapBuffer, detectionLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var knock = _overlapBuffer[i].GetComponentInParent<KnockableCapsule>();
                if (knock == null) continue;

                var no = knock.GetComponent<NetworkObject>();
                if (no != null && perPlayerCooldownSeconds > 0 &&
                    _lastKnockByPlayer.TryGetValue(no.NetworkObjectId, out float last) && Time.time - last < perPlayerCooldownSeconds)
                    continue;

                Vector3 impulse = ComputeImpulse(knock.transform.position);
                if (impulse.sqrMagnitude < 0.01f) continue;

                knock.KnockFromServer(impulse, knockDurationSeconds);
                if (no != null)
                    _lastKnockByPlayer[no.NetworkObjectId] = Time.time;
            }
        }

        /// <summary>Called by TrapKnockOnContact when the player's CharacterController hits this trap. Returns impulse to apply.</summary>
        public Vector3 GetImpulseFor(Vector3 playerPosition)
        {
            return ComputeImpulse(playerPosition);
        }

        public float KnockDurationSeconds => knockDurationSeconds;

        private Vector3 ComputeImpulse(Vector3 playerPosition)
        {
            Vector3 dir = impulseAwayFromSpinner
                ? (playerPosition - _pivot.position).normalized
                : _pivot.forward;

            dir += Vector3.up * (knockUpwardBias / Mathf.Max(0.01f, knockImpulseMagnitude));
            dir.Normalize();
            return dir * knockImpulseMagnitude;
        }
    }
}
