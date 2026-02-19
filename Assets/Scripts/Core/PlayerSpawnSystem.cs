using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Server-side spawn positioning for NGO player objects.
    /// 
    /// NGO will instantiate the Player Prefab automatically when a client connects.
    /// This script simply moves that spawned player to an available SpawnPoint.
    /// 
    /// Usage:
    /// - Put this on the same GameObject as NetworkManager (recommended).
    /// - Create one or more GameObjects in the scene tagged "PlayerSpawn" (or set tagName below).
    /// </summary>
    public class PlayerSpawnSystem : MonoBehaviour
    {
        [SerializeField] private string spawnTagName = "PlayerSpawn";

        private readonly List<Transform> cachedSpawns = new();
        private int nextIndex;

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        private void CacheSpawnPoints()
        {
            cachedSpawns.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag(spawnTagName))
            {
                cachedSpawns.Add(go.transform);
            }

            if (cachedSpawns.Count == 0)
            {
                Debug.LogWarning($"[Spawn] No spawn points found with tag '{spawnTagName}'. Using (0,0,0).");
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            if (cachedSpawns.Count == 0)
            {
                CacheSpawnPoints();
            }

            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var player = client.PlayerObject;
            if (player == null) return;

            Vector3 pos;
            Quaternion rot;

            if (cachedSpawns.Count == 0)
            {
                pos = Vector3.zero;
                rot = Quaternion.identity;
            }
            else
            {
                var t = cachedSpawns[nextIndex % cachedSpawns.Count];
                nextIndex++;
                pos = t.position;
                rot = t.rotation;
            }

            player.transform.SetPositionAndRotation(pos, rot);
            Debug.Log($"[Spawn] Positioned client {clientId} at {pos}");
        }
    }
}
