using System.Collections.Generic;
using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Loot
{
    /// <summary>
    /// Central chest spawner. Place on a NetworkObject in the dungeon scene
    /// (can share the same GameObject as EnemySpawner).
    ///
    /// - Listens for NavMesh build completion.
    /// - Collects all ChestSpawnPoint markers in the scene.
    /// - For each point: rolls spawn chance, picks a chest type, instantiates + network spawns.
    /// - One-shot spawn — chests do not respawn.
    /// </summary>
    public class ChestSpawner : NetworkBehaviour
    {
        private readonly List<ChestSpawnPoint> _points = new();
        private bool _spawned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            NavMeshBakeOnLayout.OnNavMeshBuilt += HandleNavMeshBuilt;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                NavMeshBakeOnLayout.OnNavMeshBuilt -= HandleNavMeshBuilt;
            base.OnNetworkDespawn();
        }

        private void HandleNavMeshBuilt(NavMeshBakeOnLayout baker)
        {
            if (!IsServer || baker == null) return;
            if (baker.gameObject.scene != gameObject.scene) return;

            CollectSpawnPoints();
            SpawnChests();
        }

        private void CollectSpawnPoints()
        {
            _points.Clear();

            var all = FindObjectsByType<ChestSpawnPoint>(FindObjectsSortMode.None);
            if (all == null) return;

            foreach (var point in all)
            {
                if (point == null) continue;
                if (point.gameObject.scene != gameObject.scene) continue;
                if (point.chestPool == null || point.chestPool.Length == 0) continue;
                _points.Add(point);
            }

            Debug.Log($"[ChestSpawner] Collected {_points.Count} chest spawn points.");
        }

        private void SpawnChests()
        {
            if (_spawned) return;
            _spawned = true;

            int spawned = 0;

            foreach (var point in _points)
            {
                // Roll spawn chance
                if (Random.value > point.spawnChance)
                {
                    Debug.Log($"[ChestSpawner] Point at {point.transform.position} — spawn chance failed ({point.spawnChance:P0}).");
                    continue;
                }

                // Pick which chest type
                var config = point.PickChest();
                if (config == null || config.prefab == null)
                {
                    Debug.LogWarning($"[ChestSpawner] Point at {point.transform.position} — PickChest returned null or missing prefab.");
                    continue;
                }

                // Instantiate and network-spawn
                var go = Instantiate(config.prefab, point.transform.position, point.transform.rotation);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"[ChestSpawner] Chest prefab '{config.chestName}' is missing a NetworkObject component!");
                    Destroy(go);
                    continue;
                }

                netObj.Spawn(true);

                // Initialize with config so it knows its loot table
                var lootChest = go.GetComponent<LootChest>();
                if (lootChest != null)
                {
                    lootChest.Init(config);
                }
                else
                {
                    Debug.LogWarning($"[ChestSpawner] Chest prefab '{config.chestName}' is missing a LootChest component!");
                }

                spawned++;
            }

            Debug.Log($"[ChestSpawner] Spawned {spawned}/{_points.Count} chests.");
        }
    }
}
