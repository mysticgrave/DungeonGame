using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Combat
{
    /// <summary>
    /// MVP debug combat: click to damage.
    /// Owner-only, client-side raycast; server applies damage via RPC.
    /// 
    /// Controls:
    /// - LMB: deal 1 damage
    /// - RMB: deal 2 damage
    /// </summary>
    public class DebugDamageRaycaster : NetworkBehaviour
    {
        [SerializeField] private float range = 4.0f;
        [SerializeField] private LayerMask hitMask = ~0;

        private Camera cam;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Mouse.current == null) return;

            cam = Camera.main;
            if (cam == null) return;

            int dmg = 0;
            if (Mouse.current.leftButton.wasPressedThisFrame) dmg = 1;
            if (Mouse.current.rightButton.wasPressedThisFrame) dmg = 2;
            if (dmg <= 0) return;

            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, range, hitMask, QueryTriggerInteraction.Ignore)) return;

            var health = hit.collider.GetComponentInParent<NetworkHealth>();
            if (health == null) return;

            // Ask server to apply damage.
            health.TakeDamageRpc(dmg);
        }
    }
}
