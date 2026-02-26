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
    /// Excludes the "IgnoreNavMesh" layer (and objects with tag "IgnoreNavMesh") from the bake.
    /// Attach this to the Spire generator object in Spire_Slice.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBakeOnLayout : NetworkBehaviour
    {
        public const string IgnoreNavMeshLayerName = "IgnoreNavMesh";
        public const string IgnoreNavMeshTag = "IgnoreNavMesh";

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

            ApplyIgnoreNavMeshAndBake();
        }

        public static event Action<NavMeshBakeOnLayout> OnNavMeshBuilt;

        private void HandleLayout(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (!IsServer) return;
            if (surface == null) return;

            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;

            ApplyIgnoreNavMeshAndBake();
        }

        /// <summary>
        /// Assign IgnoreNavMesh layer to objects with tag "IgnoreNavMesh", then set the surface
        /// to exclude that layer and build. This makes both the tag and the layer work.
        /// </summary>
        private void ApplyIgnoreNavMeshAndBake()
        {
            int ignoreLayer = LayerMask.NameToLayer(IgnoreNavMeshLayerName);
            if (ignoreLayer >= 0)
            {
                // So tag-based exclusion works: move any object with the tag onto the ignore layer.
                foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    if (go != null && go.CompareTag(IgnoreNavMeshTag))
                        go.layer = ignoreLayer;
                }

                // Exclude the IgnoreNavMesh layer from the bake (works even if user forgot to uncheck in Inspector).
                var current = surface.layerMask;
                surface.layerMask = current & ~(1 << ignoreLayer);
            }

            surface.BuildNavMesh();
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[NavMesh] Rebuilt navmesh (tris={tri.indices?.Length ?? 0}), layer '{IgnoreNavMeshLayerName}' excluded.");
            OnNavMeshBuilt?.Invoke(this);
        }
    }
}
