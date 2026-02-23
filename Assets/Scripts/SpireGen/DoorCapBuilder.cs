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

            // Gather all sockets across generated rooms.
            var allSockets = new List<RoomSocket>();
            foreach (var room in rooms)
            {
                if (room == null) continue;
                room.RefreshSockets();
                foreach (var s in room.sockets)
                {
                    if (s == null) continue;
                    allSockets.Add(s);
                }
            }

            // Use explicit connection list from layout data.
            var connectedKeys = new HashSet<(int roomIndex, string socketPath)>();

            int connectors = 0;
            int caps = 0;

            // Spawn ONE connector per connection at Socket A (target socket).
            if (layout != null && layout.connections != null)
            {
                foreach (var c in layout.connections)
                {
                    connectedKeys.Add((c.a.roomIndex, c.a.socketPath));
                    connectedKeys.Add((c.b.roomIndex, c.b.socketPath));

                    var aSock = ResolveSocket(rooms, c.a);
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
            foreach (var room in rooms)
            {
                if (room == null) continue;

                int roomIndex = GetRoomIndex(layout, room.transform.position);

                foreach (var s in room.sockets)
                {
                    if (s == null) continue;

                    var key = (roomIndex, GetRelativePath(room.transform, s.transform));
                    if (connectedKeys.Contains(key)) continue;

                    var prefab = PickPrefab(s.socketType, connected: false);
                    if (prefab == null) continue;

                    var no = Instantiate(prefab, s.transform.position, s.transform.rotation);
                    no.Spawn(true);
                    spawned.Add(no);
                    caps++;
                }
            }

            Debug.Log($"[DoorCap] Spawned connectors={connectors}, caps={caps} (sockets={allSockets.Count}, connections={layout?.connections?.Count ?? 0})");
        }

        private RoomSocket ResolveSocket(RoomPrefab[] rooms, SocketRef sref)
        {
            if (rooms == null) return null;
            if (sref.roomIndex < 0 || sref.roomIndex >= rooms.Length) return null;
            var room = rooms[sref.roomIndex];
            if (room == null) return null;

            var t = room.transform.Find(sref.socketPath);
            if (t == null) return null;
            return t.GetComponent<RoomSocket>();
        }

        private int GetRoomIndex(SpireLayoutData layout, Vector3 roomPos)
        {
            if (layout == null) return 0;
            for (int i = 0; i < layout.rooms.Count; i++)
            {
                if ((layout.rooms[i].position - roomPos).sqrMagnitude < 0.0001f) return i;
            }
            return 0;
        }

        private static string GetRelativePath(Transform root, Transform leaf)
        {
            if (root == null || leaf == null) return string.Empty;
            if (leaf == root) return string.Empty;

            var stack = new Stack<string>();
            var t = leaf;
            while (t != null && t != root)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", stack);
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
