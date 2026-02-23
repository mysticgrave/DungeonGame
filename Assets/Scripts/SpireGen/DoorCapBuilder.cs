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
            Rebuild(gen, layout);
        }

        public void Rebuild(SpireLayoutGenerator gen, SpireLayoutData layout)
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
            var roomInstances = gen.GetComponentsInChildren<RoomPrefab>(true);

            // Map roomNetId -> RoomPrefab instance
            var roomsByNetId = new Dictionary<ulong, RoomPrefab>();
            foreach (var r in roomInstances)
            {
                if (r == null) continue;
                var no = r.GetComponentInParent<NetworkObject>();
                if (no == null) continue;
                roomsByNetId[no.NetworkObjectId] = r;
            }

            // Gather all sockets across generated rooms.
            var allSockets = new List<(ulong roomNetId, RoomPrefab room, RoomSocket socket)>();
            foreach (var kvp in roomsByNetId)
            {
                var roomNetId = kvp.Key;
                var room = kvp.Value;
                if (room == null) continue;
                room.RefreshSockets();
                foreach (var s in room.sockets)
                {
                    if (s == null) continue;
                    allSockets.Add((roomNetId, room, s));
                }
            }

            // Use explicit connection list from layout data.
            var connectedKeys = new HashSet<(ulong roomNetId, string socketId)>();

            int connectors = 0;
            int caps = 0;

            // Spawn ONE connector per connection at Socket A (target socket).
            if (layout != null && layout.connections != null)
            {
                foreach (var c in layout.connections)
                {
                    connectedKeys.Add((c.a.roomNetId, c.a.socketId));
                    connectedKeys.Add((c.b.roomNetId, c.b.socketId));

                    if (!roomsByNetId.TryGetValue(c.a.roomNetId, out var roomA) || roomA == null) continue;

                    // Find socket by socketId
                    RoomSocket aSock = null;
                    foreach (var s in roomA.sockets)
                    {
                        if (s != null && s.socketId == c.a.socketId) { aSock = s; break; }
                    }
                    if (aSock == null) continue;

                    var prefab = PickPrefab(c.a.socketType, connected: true);
                    if (prefab == null) continue;

                    var no = Instantiate(prefab, aSock.transform.position, aSock.transform.rotation);
                    no.Spawn(true);
                    spawned.Add(no);
                    connectors++;
                }
            }

            // Caps for any socket not connected.
            foreach (var item in allSockets)
            {
                var roomNetId = item.roomNetId;
                var s = item.socket;
                if (s == null) continue;

                var key = (roomNetId, s.socketId);
                if (connectedKeys.Contains(key)) continue;

                var prefab = PickPrefab(s.socketType, connected: false);
                if (prefab == null) continue;

                var no = Instantiate(prefab, s.transform.position, s.transform.rotation);
                no.Spawn(true);
                spawned.Add(no);
                caps++;
            }

            Debug.Log($"[DoorCap] Spawned connectors={connectors}, caps={caps} (sockets={allSockets.Count}, connections={layout?.connections?.Count ?? 0})");
        }

        // (old index/path mapping removed; we now key by room NetworkObjectId + socketId)

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
