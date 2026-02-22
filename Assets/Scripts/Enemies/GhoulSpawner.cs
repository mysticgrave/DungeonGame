using System.Collections.Generic;
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

        [Header("Safety")]
        [SerializeField] private float minDistanceFromPlayers = 10f;
        [SerializeField] private int maxPositionAttempts = 30;

        private bool spawned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            SpawnIfNeeded();
        }

        private void SpawnIfNeeded()
        {
            if (spawned) return;
            spawned = true;

            if (ghoulPrefab == null)
            {
                Debug.LogError("[GhoulSpawner] Missing ghoulPrefab.");
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
                    return navHit.position;
                }

                return candidate;
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
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                // Offset slightly so multiple spawns don't stack.
                var basePos = spawnPoints[i % spawnPoints.Count].position;
                var jitter = Random.insideUnitCircle * 1.5f;
                return basePos + new Vector3(jitter.x, 0f, jitter.y);
            }

            // Random ring around the spawner.
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
