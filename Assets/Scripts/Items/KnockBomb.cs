using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Items
{
    /// <summary>
    /// MVP knockback bomb.
    /// Server spawns it; it explodes after fuse and knocks/damages players in radius.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class KnockBomb : NetworkBehaviour
    {
        [SerializeField] private float fuseSeconds = 1.2f;
        [SerializeField] private float radius = 6.0f;
        [SerializeField] private float knockForce = 18f;
        [SerializeField] private float upForce = 6f;
        [SerializeField] private int damage = 1;

        private float explodeAt;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                explodeAt = Time.time + fuseSeconds;
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Time.time < explodeAt) return;

            Explode();

            // Cleanup
            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
            {
                no.Despawn(true);
            }
        }

        private void Explode()
        {
            var hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var no = h.GetComponentInParent<NetworkObject>();
                if (no == null || !no.IsPlayerObject) continue;

                var kb = no.GetComponent<KnockableCapsule>();
                var hp = no.GetComponent<PlayerHealth>();

                // impulse away
                var dir = (no.transform.position - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                dir.Normalize();

                var impulse = dir * knockForce + Vector3.up * upForce;
                if (kb != null) kb.KnockRpc(impulse);
                if (hp != null && damage > 0) hp.DamageRpc(damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
