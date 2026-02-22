using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Spawns door connectors for connected sockets and wall caps for unconnected sockets.
    /// 
    /// Uses the generator's "used socket" concept:
    /// - If a RoomSocket was used in a connection, it's considered connected -> spawn DoorConnector.
    /// - Otherwise -> spawn WallCap.
    /// 
    /// IMPORTANT: Door/cap prefabs should be NetworkObjects spawned as top-level objects
    /// (do not nest them under spawned room NetworkObjects).
    /// </summary>
    public class DoorCapBuilder : NetworkBehaviour
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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            if (rebuildOnLayoutGenerated)
            {
                SpireLayoutGenerator.OnLayoutGenerated += HandleLayout;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SpireLayoutGenerator.OnLayoutGenerated -= HandleLayout;
            }
            base.OnNetworkDespawn();
        }

        private void HandleLayout(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (!IsServer) return;

            // Only run in our scene (avoid cross-talk if multiple spires exist later)
            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;

            Debug.Log($"[DoorCap] Layout generated event received (rooms={layout?.rooms?.Count ?? -1}). Rebuilding...");
            Rebuild(gen);
        }

        public void Rebuild(SpireLayoutGenerator gen)
        {
            if (!IsServer) return;
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
            foreach (var room in rooms)
            {
                if (room == null) continue;

                foreach (var socket in room.sockets)
                {
                    if (socket == null) continue;

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
