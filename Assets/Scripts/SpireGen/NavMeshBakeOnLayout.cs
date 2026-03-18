using System;
using System.Collections;
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
    ///
    /// ONLY bakes when OnLayoutGenerated fires. A fallback timeout catches misconfigured
    /// generators that never fire the event, but it waits for IsGenerating to be false first
    /// AND waits an initial grace period for generation to start.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshBakeOnLayout : NetworkBehaviour
    {
        public const string IgnoreNavMeshLayerName = "IgnoreNavMesh";
        public const string IgnoreNavMeshTag = "IgnoreNavMesh";

        [Tooltip("Seconds to wait for generation to START before considering fallback. Must be long enough for SpireLayoutGenerator.OnNetworkSpawn to fire.")]
        [SerializeField] private float startGracePeriod = 3f;

        [Tooltip("Max seconds to wait for generation to FINISH (after it starts) before fallback bake. 0 = wait forever.")]
        [SerializeField] private float fallbackTimeout = 60f;

        private NavMeshSurface surface;
        private bool _hasBaked;
        private Coroutine _fallbackCoroutine;

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            _hasBaked = false;
            SpireLayoutGenerator.OnLayoutGenerated += HandleLayout;

            // Start fallback coroutine as safety net.
            // The primary path is HandleLayout (fired by the generator when done).
            _fallbackCoroutine = StartCoroutine(FallbackBakeCoroutine());
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SpireLayoutGenerator.OnLayoutGenerated -= HandleLayout;
            }

            if (_fallbackCoroutine != null)
            {
                StopCoroutine(_fallbackCoroutine);
                _fallbackCoroutine = null;
            }

            base.OnNetworkDespawn();
        }

        public static event Action<NavMeshBakeOnLayout> OnNavMeshBuilt;

        /// <summary>
        /// Primary bake path — called by SpireLayoutGenerator.OnLayoutGenerated
        /// after all rooms are placed and generation is fully complete.
        /// </summary>
        private void HandleLayout(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (!IsServer) return;
            if (surface == null) return;
            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;

            Debug.Log("[NavMesh] OnLayoutGenerated received — baking NavMesh.");

            // Cancel fallback since the event fired properly
            if (_fallbackCoroutine != null)
            {
                StopCoroutine(_fallbackCoroutine);
                _fallbackCoroutine = null;
            }

            // Reset so this generation gets a fresh bake
            _hasBaked = false;
            DoBake();
        }

        /// <summary>
        /// Fallback safety net for misconfigured generators that never fire OnLayoutGenerated.
        ///
        /// Timing:
        /// 1. Wait grace period for generation to START (IsGenerating may still be false
        ///    because SpireLayoutGenerator.OnNetworkSpawn hasn't fired yet)
        /// 2. If generation started, wait for it to FINISH
        /// 3. Only then bake (if HandleLayout hasn't already done it)
        /// </summary>
        private IEnumerator FallbackBakeCoroutine()
        {
            // Phase 1: Grace period — wait for generation to start.
            // SpireLayoutGenerator.OnNetworkSpawn may fire after ours, so IsGenerating
            // could still be false. Don't bake yet!
            float graceElapsed = 0f;
            while (graceElapsed < startGracePeriod)
            {
                // If generation started, move to phase 2 immediately
                if (SpireLayoutGenerator.IsGenerating)
                    break;

                // If HandleLayout already baked, we're done
                if (_hasBaked)
                {
                    _fallbackCoroutine = null;
                    yield break;
                }

                graceElapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            // Phase 2: Wait for generation to finish
            float waitElapsed = 0f;
            while (SpireLayoutGenerator.IsGenerating)
            {
                if (_hasBaked)
                {
                    _fallbackCoroutine = null;
                    yield break;
                }

                waitElapsed += 0.5f;
                if (fallbackTimeout > 0f && waitElapsed >= fallbackTimeout)
                {
                    Debug.LogWarning($"[NavMesh] Timed out waiting for generation after {waitElapsed:F0}s. Baking anyway.");
                    break;
                }
                yield return new WaitForSeconds(0.5f);
            }

            // Extra frames for cleanup
            yield return null;
            yield return null;

            _fallbackCoroutine = null;

            if (!_hasBaked)
            {
                Debug.Log("[NavMesh] Fallback bake triggered (OnLayoutGenerated never received).");
                DoBake();
            }
        }

        private void DoBake()
        {
            if (_hasBaked)
            {
                Debug.Log("[NavMesh] Already baked this generation — skipping.");
                return;
            }

            _hasBaked = true;
            ApplyIgnoreNavMeshAndBake();
        }

        /// <summary>
        /// Reset bake state so a new generation can trigger a fresh bake.
        /// </summary>
        public void ResetBakeState()
        {
            _hasBaked = false;
        }

        private void ApplyIgnoreNavMeshAndBake()
        {
            // Force reliable settings for procedural dungeons:
            // - CollectObjects.All: scan all loaded scenes (rooms may be reparented by NGO)
            // - PhysicsColliders: immune to static batching (MarkStaticRecursive runs during generation
            //   and can combine/disable MeshRenderers before we bake, causing missing NavMesh areas)
            // - All layers included (then exclude IgnoreNavMesh below)
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0; // Start with all layers

            int ignoreLayer = LayerMask.NameToLayer(IgnoreNavMeshLayerName);
            if (ignoreLayer >= 0)
            {
                // Move tagged objects to the ignore layer
                foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    if (go != null && go.CompareTag(IgnoreNavMeshTag))
                        go.layer = ignoreLayer;
                }

                // Exclude IgnoreNavMesh layer
                surface.layerMask = ~(1 << ignoreLayer);
            }

            surface.BuildNavMesh();

            var tri = NavMesh.CalculateTriangulation();
            int triCount = tri.indices != null ? tri.indices.Length / 3 : 0;
            Debug.Log($"[NavMesh] Rebuilt navmesh ({triCount} triangles, {tri.vertices?.Length ?? 0} verts), " +
                      $"geometry=PhysicsColliders, layers={(int)surface.layerMask}");

            OnNavMeshBuilt?.Invoke(this);
            NotifyDungeonReadyClientRpc();
        }

        [ClientRpc]
        private void NotifyDungeonReadyClientRpc()
        {
            // Loading screen is hidden by DungeonPlayerSpawner after repositioning.
        }
    }
}
