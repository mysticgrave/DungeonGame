using System;
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

            // Fallback: if no layout event fires (misconfigured generator), try a delayed build.
            Invoke(nameof(DelayedBuild), 1.0f);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SpireLayoutGenerator.OnLayoutGenerated -= HandleLayout;
            }

            CancelInvoke(nameof(DelayedBuild));
            base.OnNetworkDespawn();
        }

        private void DelayedBuild()
        {
            if (!IsServer) return;
            if (surface == null) return;

            surface.BuildNavMesh();
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[NavMesh] Fallback build (tris={tri.indices?.Length ?? 0})");
            OnNavMeshBuilt?.Invoke(this);
        }

        public static event Action<NavMeshBakeOnLayout> OnNavMeshBuilt;

        private void HandleLayout(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (!IsServer) return;
            if (surface == null) return;

            // Bake only if the layout generator is in the same Unity scene.
            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;

            surface.BuildNavMesh();

            // Basic sanity check
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[NavMesh] Rebuilt navmesh after layout generation (tris={tri.indices?.Length ?? 0})");

            OnNavMeshBuilt?.Invoke(this);
        }
    }
}
