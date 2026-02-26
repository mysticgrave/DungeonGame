using System.Collections.Generic;
using DungeonGame.Combat;
using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Central enemy lifecycle manager. Place on a NetworkObject in the dungeon scene.
    ///
    /// - Auto-collects all EnemySpawnPoint markers in the scene after navmesh is ready.
    /// - Each spawn point defines what enemies spawn there, how many, and respawn rules.
    /// - This script handles initial spawning, death tracking, and respawn timers.
    /// </summary>
    public class EnemySpawner : NetworkBehaviour
    {
        [Header("Spawn Point Filters")]
        [Tooltip("Which spawn point kinds this spawner manages. Leave empty for all.")]
        [SerializeField] private EnemySpawnPoint.SpawnKind[] allowedKinds;

        [Header("Safety")]
        [SerializeField] private float minDistanceFromPlayers = 10f;
        [SerializeField] private int maxPositionAttempts = 30;

        [Header("Spawn Radius")]
        [Tooltip("Only spawn at a point when at least one player is within this distance. 0 = no limit (spawn everywhere).")]
        [SerializeField] private float playerSpawnRadius = 40f;

        private readonly List<EnemySpawnPoint> _points = new();
        private readonly HashSet<EnemySpawnPoint> _pointsWithInitialSpawn = new();
        private bool _initialSpawnDone;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            NavMeshBakeOnLayout.OnNavMeshBuilt += HandleNavMeshBuilt;
            InvokeRepeating(nameof(TryInitialSpawn), 0.25f, 0.5f);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                NavMeshBakeOnLayout.OnNavMeshBuilt -= HandleNavMeshBuilt;
            CancelInvoke(nameof(TryInitialSpawn));
            base.OnNetworkDespawn();
        }

        private void HandleNavMeshBuilt(NavMeshBakeOnLayout baker)
        {
            if (!IsServer || baker == null) return;
            if (baker.gameObject.scene != gameObject.scene) return;
            _initialSpawnDone = false;
            CollectSpawnPoints();
            DoInitialSpawn();
        }

        private void TryInitialSpawn()
        {
            if (_initialSpawnDone) { CancelInvoke(nameof(TryInitialSpawn)); return; }

            if (!UnityEngine.AI.NavMesh.SamplePosition(transform.position, out _, 50f, UnityEngine.AI.NavMesh.AllAreas))
                return;

            CollectSpawnPoints();
            DoInitialSpawn();
        }

        private void CollectSpawnPoints()
        {
            _points.Clear();

            var all = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            if (all == null) return;

            HashSet<EnemySpawnPoint.SpawnKind> filter = null;
            if (allowedKinds != null && allowedKinds.Length > 0)
                filter = new HashSet<EnemySpawnPoint.SpawnKind>(allowedKinds);

            foreach (var pt in all)
            {
                if (pt == null) continue;
                if (pt.gameObject.scene != gameObject.scene) continue;
                if (pt.enemyPool == null || pt.enemyPool.Length == 0) continue;
                if (filter != null && !filter.Contains(pt.kind)) continue;
                _points.Add(pt);
            }

            Debug.Log($"[EnemySpawner] Collected {_points.Count} spawn points in scene.");
        }

        private void DoInitialSpawn()
        {
            if (_initialSpawnDone) return;
            if (_points.Count == 0) return;

            _initialSpawnDone = true;
            CancelInvoke(nameof(TryInitialSpawn));

            foreach (var pt in _points)
            {
                pt.alive.Clear();
                pt.pendingRespawnTimers.Clear();
                pt.totalRespawnsUsed = 0;
                _pointsWithInitialSpawn.Remove(pt);

                if (!IsPointInSpawnRadius(pt)) continue;

                _pointsWithInitialSpawn.Add(pt);
                for (int i = 0; i < pt.maxAlive; i++)
                    SpawnAtPoint(pt);
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!_initialSpawnDone) return;

            PruneDestroyedPoints();

            float dt = Time.deltaTime;

            foreach (var pt in _points)
            {
                if (pt == null) continue;
                if (playerSpawnRadius > 0f && !IsPointInSpawnRadius(pt))
                    continue;

                // When a player first enters range, do initial spawn for this point if we haven't yet.
                if (!_pointsWithInitialSpawn.Contains(pt))
                {
                    _pointsWithInitialSpawn.Add(pt);
                    for (int i = pt.alive.Count; i < pt.maxAlive; i++)
                        SpawnAtPoint(pt);
                    continue;
                }

                // Clean up dead references and queue respawns.
                for (int i = pt.alive.Count - 1; i >= 0; i--)
                {
                    if (pt.alive[i] == null)
                    {
                        pt.alive.RemoveAt(i);

                        if (pt.CanRespawn)
                        {
                            pt.pendingRespawnTimers.Add(pt.respawnDelay);
                            pt.totalRespawnsUsed++;
                        }
                    }
                }

                // Tick respawn timers.
                for (int i = pt.pendingRespawnTimers.Count - 1; i >= 0; i--)
                {
                    pt.pendingRespawnTimers[i] -= dt;
                    if (pt.pendingRespawnTimers[i] <= 0f)
                    {
                        pt.pendingRespawnTimers.RemoveAt(i);
                        if (pt.alive.Count < pt.maxAlive)
                            SpawnAtPoint(pt);
                    }
                }
            }
        }

        private void SpawnAtPoint(EnemySpawnPoint pt)
        {
            var config = pt.PickEnemy();
            if (config == null || config.prefab == null) return;

            if (!TryGetNavMeshSpawnPosition(pt, out Vector3 pos))
            {
                Debug.LogWarning($"[EnemySpawner] No valid NavMesh position near spawn point '{pt.gameObject.name}'. Skipping spawn.");
                return;
            }

            var go = Instantiate(config.prefab, pos, Quaternion.identity);

            var no = go.GetComponent<NetworkObject>();
            if (no != null) no.Spawn(true);

            var ai = go.GetComponent<EnemyAI>();
            if (ai != null) ai.Init(config);

            pt.alive.Add(go);
        }

        /// <summary>
        /// Get a spawn position that is on the NavMesh so the NavMeshAgent can be created.
        /// Uses only NavMesh.SamplePosition — never physics raycast — so the position is valid for the agent.
        /// </summary>
        private bool TryGetNavMeshSpawnPosition(EnemySpawnPoint pt, out Vector3 position)
        {
            position = pt.transform.position;

            // Pass 1: jittered positions on navmesh, away from players.
            for (int attempt = 0; attempt < maxPositionAttempts; attempt++)
            {
                Vector3 candidate = GetCandidatePos(pt);

                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var navHit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                position = navHit.position;
                if (!IsTooCloseToAnyPlayer(position))
                    return true;
            }

            // Pass 2: any position on navmesh near the point (relax player distance).
            for (int attempt = 0; attempt < maxPositionAttempts; attempt++)
            {
                Vector3 candidate = GetCandidatePos(pt);

                if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var navHit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                position = navHit.position;
                return true;
            }

            // Pass 3: exact spawn point.
            if (UnityEngine.AI.NavMesh.SamplePosition(pt.transform.position, out var fallback, 8f, UnityEngine.AI.NavMesh.AllAreas))
            {
                position = fallback.position;
                return true;
            }

            return false;
        }

        private static Vector3 GetCandidatePos(EnemySpawnPoint pt)
        {
            if (pt.jitterRadius <= 0f) return pt.transform.position;
            var jitter = Random.insideUnitCircle * pt.jitterRadius;
            return pt.transform.position + new Vector3(jitter.x, 0f, jitter.y);
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
                    return true;
            }
            return false;
        }

        private void PruneDestroyedPoints()
        {
            for (int i = _points.Count - 1; i >= 0; i--)
            {
                if (_points[i] == null)
                    _points.RemoveAt(i);
            }
            _pointsWithInitialSpawn.RemoveWhere(pt => pt == null);
        }

        /// <summary>True if at least one player is within playerSpawnRadius of the spawn point. If playerSpawnRadius is 0, always true.</summary>
        private bool IsPointInSpawnRadius(EnemySpawnPoint pt)
        {
            if (pt == null) return false;
            if (playerSpawnRadius <= 0f) return true;

            var nm = NetworkManager.Singleton;
            if (nm == null) return false;

            Vector3 ptPos = pt.transform.position;
            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;
                if (Vector3.Distance(ptPos, player.transform.position) <= playerSpawnRadius)
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerSpawnRadius <= 0f) return;

            var points = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pt in points)
            {
                if (pt == null || pt.gameObject.scene != gameObject.scene) continue;
                UnityEditor.Handles.color = new Color(1f, 0.9f, 0.2f, 0.2f);
                UnityEditor.Handles.DrawSolidDisc(pt.transform.position, Vector3.up, playerSpawnRadius);
                UnityEditor.Handles.color = new Color(1f, 0.9f, 0.2f, 0.8f);
                UnityEditor.Handles.DrawWireDisc(pt.transform.position, Vector3.up, playerSpawnRadius);
            }
        }
#endif
    }
}
