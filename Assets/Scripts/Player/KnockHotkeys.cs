using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// MVP hotkeys:
    /// - K: knock yourself forward (server-authoritative trigger)
    /// </summary>
    public class KnockHotkeys : NetworkBehaviour
    {
        [SerializeField] private float force = 14f;
        [SerializeField] private float upForce = 5f;
        [SerializeField] private float seconds = 1.6f;

        private KnockableCapsule knock;

        private void Awake()
        {
            knock = GetComponent<KnockableCapsule>();
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

            if (!Keyboard.current.kKey.wasPressedThisFrame) return;

            if (knock == null)
            {
                Debug.LogError("[KnockHotkeys] Missing KnockableCapsule");
                return;
            }

            var impulse = transform.forward * force + Vector3.up * upForce;
            knock.KnockRpc(impulse, seconds);
        }
    }
}
