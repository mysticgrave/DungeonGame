using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Spawns door connectors for connected sockets and wall caps for unconnected sockets.
    /// 
    /// IMPORTANT:
    /// - This is a MonoBehaviour (not a NetworkBehaviour) so it can live on the same Spire object as generation,
    ///   without requiring that object to be a spawned NetworkObject.
    /// - It will only act on the server.
    /// - It spawns door/cap prefabs as top-level NetworkObjects (no nesting under rooms).
    /// </summary>
    public class DoorCapBuilder : MonoBehaviour
    {
        [Header("Prefabs (NetworkObject)")]
        [SerializeField] private NetworkObject doorConnectorSmall;
        [SerializeField] private NetworkObject doorConnectorLarge;
        [SerializeField] private NetworkObject wallCapSmall;
        [SerializeField] private NetworkObject wallCapLarge;

        [Header("Options")]
        [SerializeField] private bool rebuildOnLayoutGenerated = true;

        // Track spawned objects so we can despawn on rebuild.
        private readonly List<NetworkObject> spawned = new();

        private void OnEnable()
        {
            if (!rebuildOnLayoutGenerated) return;
            SpireLayoutGenerator.OnLayoutGenerated += HandleLayout;
        }

        private void OnDisable()
        {
            SpireLayoutGenerator.OnLayoutGenerated -= HandleLayout;
        }

        private void HandleLayout(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            // Only run in our scene (avoid cross-talk if multiple spires exist later)
            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;

            Debug.Log($"[DoorCap] Layout generated event received (rooms={layout?.rooms?.Count ?? -1}). Rebuilding...");
            Rebuild(gen);
        }

        public void Rebuild(SpireLayoutGenerator gen)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (gen == null) return;

            if (doorConnectorSmall == null || wallCapSmall == null || doorConnectorLarge == null || wallCapLarge == null)
            {
                Debug.LogError("[DoorCap] Missing one or more prefabs. Assign doorConnectorSmall/doorConnectorLarge/wallCapSmall/wallCapLarge in inspector.");
                return;
            }

            DespawnPrevious();

            // Find all RoomPrefab instances the generator spawned (children under its transform).
            var rooms = gen.GetComponentsInChildren<RoomPrefab>(true);

            int count = 0;
            var seenSockets = new HashSet<Transform>();

            foreach (var room in rooms)
            {
                if (room == null) continue;

                // Ensure runtime sockets list is fresh/unique
                room.RefreshSockets();

                foreach (var socket in room.sockets)
                {
                    if (socket == null) continue;
                    if (socket.transform == null) continue;

                    // Avoid double-spawning if a socket is duplicated in the list.
                    if (!seenSockets.Add(socket.transform)) continue;

                    bool connected = gen.IsSocketUsed(room.transform, socket);
                    var prefab = PickPrefab(socket.socketType, connected);
                    if (prefab == null) continue;

                    var no = Instantiate(prefab, socket.transform.position, socket.transform.rotation);
                    no.Spawn(true);
                    spawned.Add(no);
                    count++;
                }
            }

            Debug.Log($"[DoorCap] Spawned {count} connectors/caps");
        }

        private NetworkObject PickPrefab(SocketType type, bool connected)
        {
            return (type, connected) switch
            {
                (SocketType.DoorLarge, true) => doorConnectorLarge,
                (SocketType.DoorLarge, false) => wallCapLarge,

                // Default: DoorSmall
                (_, true) => doorConnectorSmall,
                (_, false) => wallCapSmall,
            };
        }

        private void DespawnPrevious()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                var no = spawned[i];
                if (no == null) continue;
                if (no.IsSpawned) no.Despawn(true);
                else Destroy(no.gameObject);
            }
            spawned.Clear();
        }
    }
}
