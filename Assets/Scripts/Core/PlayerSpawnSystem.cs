using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Server-side spawn positioning for NGO player objects.
    /// Works in any scene (Town, Dungeon, etc.).
    ///
    /// Place PlayerSpawnPoint components in the scene to define where players appear.
    /// Falls back to tagged objects ("PlayerSpawn") if none are found.
    /// </summary>
    public class PlayerSpawnSystem : MonoBehaviour
    {
        [Tooltip("Used only when no PlayerSpawnPoint components are found in the scene.")]
        [SerializeField] private string spawnTagName = "PlayerSpawn";

        private readonly List<Transform> cachedSpawns = new();
        private int nextIndex;

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        private void CacheSpawnPoints()
        {
            cachedSpawns.Clear();

            var byComponent = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (byComponent != null && byComponent.Length > 0)
            {
                foreach (var t in byComponent.OrderBy(p => p.SpawnIndex).Select(p => p.transform))
                    cachedSpawns.Add(t);
            }

            if (cachedSpawns.Count == 0)
            {
                foreach (var go in GameObject.FindGameObjectsWithTag(spawnTagName))
                    cachedSpawns.Add(go.transform);
            }

            if (cachedSpawns.Count == 0)
                Debug.LogWarning($"[Spawn] No PlayerSpawnPoint components or tag '{spawnTagName}' found. Using (0,0,0).");
        }

        private void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            StartCoroutine(PositionClientWhenReady(clientId));
        }

        private IEnumerator PositionClientWhenReady(ulong clientId)
        {
            var nm = NetworkManager.Singleton;

            // Wait up to 5 seconds for PlayerObject to become available
            // (Netcode may not have spawned the player prefab yet when OnClientConnectedCallback fires)
            float timeout = 5f;
            float elapsed = 0f;
            NetworkClient client = null;

            while (elapsed < timeout)
            {
                if (nm == null || !nm.IsServer) yield break;
                if (nm.ConnectedClients.TryGetValue(clientId, out client) && client.PlayerObject != null)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (client?.PlayerObject == null)
            {
                Debug.LogWarning($"[Spawn] Timed out waiting for PlayerObject for client {clientId}.");
                yield break;
            }

            var player = client.PlayerObject;

            if (cachedSpawns.Count == 0)
                CacheSpawnPoints();

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

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.SetPositionAndRotation(pos, rot);
            GroundSnap.SnapTransform(player.transform, cc);

            if (cc != null) cc.enabled = true;

            Debug.Log($"[Spawn] Positioned client {clientId} at {player.transform.position}");
        }
    }
}
