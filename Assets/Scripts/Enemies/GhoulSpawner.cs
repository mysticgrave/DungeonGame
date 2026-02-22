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
                Vector3 pos = GetSpawnPos(i);
                var no = Instantiate(ghoulPrefab, pos, Quaternion.identity);
                no.Spawn(true);
            }

            Debug.Log($"[GhoulSpawner] Spawned {count} ghouls");
        }

        private Vector3 GetSpawnPos(int i)
        {
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                return spawnPoints[i % spawnPoints.Count].position;
            }

            // Random ring around the spawner.
            var r = Random.insideUnitCircle.normalized * Random.Range(2f, radius);
            return transform.position + new Vector3(r.x, 0f, r.y);
        }
    }
}
