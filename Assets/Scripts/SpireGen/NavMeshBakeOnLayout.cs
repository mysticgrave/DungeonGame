using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Rebuilds NavMeshSurface after procedural layout generation.
    /// Attach this to the Spire generator object in Spire_Slice.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBakeOnLayout : NetworkBehaviour
    {
        private NavMeshSurface surface;

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            SpireLayoutGenerator.OnLayoutGenerated += HandleLayout;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SpireLayoutGenerator.OnLayoutGenerated -= HandleLayout;
            }
            base.OnNetworkDespawn();
        }

        private void HandleLayout(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (!IsServer) return;
            if (surface == null) return;

            // Only bake for the generator we're attached to.
            if (gen == null || gen.gameObject != gameObject) return;

            surface.BuildNavMesh();
            Debug.Log("[NavMesh] Rebuilt navmesh after layout generation");
        }
    }
}
