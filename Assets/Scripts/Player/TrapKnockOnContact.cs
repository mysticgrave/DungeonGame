using Unity.Netcode;
using UnityEngine;
using DungeonGame.Hazards;

namespace DungeonGame.Player
{
    /// <summary>
    /// When the player's CharacterController hits a SpinningKnockTrap (or other trap that implements the same pattern),
    /// requests a knock/ragdoll via KnockRpc. Add to the same GameObject as CharacterController and KnockableCapsule.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(KnockableCapsule))]
    public class TrapKnockOnContact : NetworkBehaviour
    {
        [Tooltip("Seconds before this trap can knock the same player again (avoids spam).")]
        [SerializeField] private float cooldownPerTrapSeconds = 1f;

        private KnockableCapsule _knockable;
        private float _lastKnockTime;

        private void Awake()
        {
            _knockable = GetComponent<KnockableCapsule>();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsOwner) return;
            if (_knockable == null || _knockable.IsKnocked()) return;
            if (cooldownPerTrapSeconds > 0 && Time.time - _lastKnockTime < cooldownPerTrapSeconds) return;

            var trap = hit.gameObject.GetComponent<SpinningKnockTrap>();
            if (trap == null) return;

            Vector3 impulse = trap.GetImpulseFor(transform.position);
            if (impulse.sqrMagnitude < 0.01f) return;

            float duration = trap.KnockDurationSeconds;
            _knockable.KnockRpc(impulse, duration);
            _lastKnockTime = Time.time;
        }
    }
}
