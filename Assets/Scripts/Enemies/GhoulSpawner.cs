using System.Collections.Generic;
using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// MVP spawner.
    /// Spawns a number of ghouls on the server when the scene loads.
    /// </summary>
    public class GhoulSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject ghoulPrefab;
        [SerializeField, Min(0)] private int count = 8;
        [SerializeField] private float radius = 12f;
        [SerializeField] private List<Transform> spawnPoints = new();

        [Header("Procedural Spawn Points")]
        [Tooltip("If true, will auto-collect EnemySpawnPoint markers in the scene after layout/navmesh is ready.")]
        [SerializeField] private bool autoCollectSpawnMarkers = true;

        [Tooltip("Which marker kinds this spawner is allowed to use.")]
        [SerializeField] private EnemySpawnPoint.SpawnKind[] allowedKinds = { EnemySpawnPoint.SpawnKind.Default };

        [Header("Safety")]
        [SerializeField] private float minDistanceFromPlayers = 10f;
        [SerializeField] private int maxPositionAttempts = 30;

        private bool spawned;
        private readonly List<Transform> cachedMarkerPoints = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            // Wait for navmesh to be built (procedural layout) before spawning agents.
            NavMeshBakeOnLayout.OnNavMeshBuilt += HandleNavMeshBuilt;

            // Also retry for a short window in case the bake event fired before we subscribed
            // (race during scene load).
            InvokeRepeating(nameof(TrySpawnTick), 0.25f, 0.5f);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NavMeshBakeOnLayout.OnNavMeshBuilt -= HandleNavMeshBuilt;
            }

            CancelInvoke(nameof(TrySpawnTick));
            base.OnNetworkDespawn();
        }

        private void HandleNavMeshBuilt(NavMeshBakeOnLayout baker)
        {
            if (!IsServer) return;

            // Only react if the baker is in the same scene as this spawner.
            if (baker == null) return;
            if (baker.gameObject.scene != gameObject.scene) return;

            CacheSpawnMarkers();

            spawned = false; // allow retry
            SpawnIfNeeded();
        }

        private void TrySpawnTick()
        {
            // Keep trying until we succeed.
            CacheSpawnMarkers();
            SpawnIfNeeded();

            if (spawned)
            {
                CancelInvoke(nameof(TrySpawnTick));
            }
        }

        private void CacheSpawnMarkers()
        {
            if (!autoCollectSpawnMarkers) return;

            cachedMarkerPoints.Clear();

            // Gather markers only in our current scene.
            var markers = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            if (markers == null || markers.Length == 0) return;

            var allowed = new HashSet<EnemySpawnPoint.SpawnKind>(allowedKinds ?? new[] { EnemySpawnPoint.SpawnKind.Default });

            foreach (var m in markers)
            {
                if (m == null) continue;
                if (m.gameObject.scene != gameObject.scene) continue;
                if (!allowed.Contains(m.kind)) continue;

                cachedMarkerPoints.Add(m.transform);
            }
        }

        private void SpawnIfNeeded()
        {
            if (spawned) return;
            spawned = true;

            // If navmesh isn't ready yet, we'll retry after bake event / retry tick.
            if (!UnityEngine.AI.NavMesh.SamplePosition(transform.position, out _, 50f, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawned = false;
                return;
            }

            if (ghoulPrefab == null)
            {
                Debug.LogError("[GhoulSpawner] Missing ghoulPrefab.");
                spawned = false;
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = FindValidSpawnPos(i);
                var no = Instantiate(ghoulPrefab, pos, Quaternion.identity);
                no.Spawn(true);
            }

            Debug.Log($"[GhoulSpawner] Spawned {count} ghouls");
        }

        private Vector3 FindValidSpawnPos(int i)
        {
            // Try a few times to keep ghouls away from players and on valid ground.
            for (int attempt = 0; attempt < maxPositionAttempts; attempt++)
            {
                Vector3 candidate = GetCandidatePos(i, attempt);

                if (IsTooCloseToAnyPlayer(candidate))
                {
                    continue;
                }

                // Snap to NavMesh if available.
                if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var navHit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    var snapped = navHit.position;
                    if (IsTooCloseToAnyPlayer(snapped))
                    {
                        continue;
                    }
                    return snapped;
                }

                // If no navmesh yet, still enforce distance.
                if (!IsTooCloseToAnyPlayer(candidate))
                {
                    return candidate;
                }
            }

            // Fallback: whatever we get (still try to snap to navmesh).
            var fallback = GetCandidatePos(i, maxPositionAttempts);
            if (UnityEngine.AI.NavMesh.SamplePosition(fallback, out var navHit2, 6f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return navHit2.position;
            }
            return fallback;
        }

        private Vector3 GetCandidatePos(int i, int attempt)
        {
            // 1) Explicit spawn points assigned in inspector.
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                var basePos = spawnPoints[i % spawnPoints.Count].position;
                var jitter = Random.insideUnitCircle * 1.5f;
                return basePos + new Vector3(jitter.x, 0f, jitter.y);
            }

            // 2) Procedural spawn markers collected from room prefabs.
            if (cachedMarkerPoints.Count > 0)
            {
                var t = cachedMarkerPoints[Random.Range(0, cachedMarkerPoints.Count)];
                if (t != null)
                {
                    var jitter = Random.insideUnitCircle * 1.25f;
                    return t.position + new Vector3(jitter.x, 0f, jitter.y);
                }
            }

            // 3) Fallback: random ring around the spawner.
            var r = Random.insideUnitCircle.normalized * Random.Range(2f, radius);
            return transform.position + new Vector3(r.x, 0f, r.y);
        }

        private bool IsTooCloseToAnyPlayer(Vector3 pos)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;

            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                if (Vector3.Distance(pos, player.transform.position) < minDistanceFromPlayers)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
