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

            // Group by (type,size,quantized position) to detect connections even without explicit connection data.
            var groups = new Dictionary<(SocketType type, int size, Vector3Int qpos), List<RoomSocket>>();
            foreach (var s in allSockets)
            {
                var p = s.transform.position;
                var q = new Vector3Int(
                    Mathf.RoundToInt(p.x * 10f),
                    Mathf.RoundToInt(p.y * 10f),
                    Mathf.RoundToInt(p.z * 10f));

                var key = (s.socketType, s.size, q);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<RoomSocket>();
                    groups[key] = list;
                }
                list.Add(s);
            }

            int connectors = 0;
            int caps = 0;

            // Handle connected groups (2 sockets at same spot): spawn ONE connector.
            var handled = new HashSet<RoomSocket>();
            foreach (var kvp in groups)
            {
                var list = kvp.Value;
                if (list == null || list.Count == 0) continue;

                if (list.Count >= 2)
                {
                    // Spawn one connector at the shared position.
                    var a = list[0];
                    var prefab = PickPrefab(kvp.Key.type, connected: true);
                    if (prefab != null)
                    {
                        var no = Instantiate(prefab, a.transform.position, a.transform.rotation);
                        no.Spawn(true);
                        spawned.Add(no);
                        connectors++;
                    }

                    foreach (var s in list) handled.Add(s);
                }
            }

            // Any unhandled socket gets a cap.
            foreach (var s in allSockets)
            {
                if (s == null) continue;
                if (handled.Contains(s)) continue;

                var prefab = PickPrefab(s.socketType, connected: false);
                if (prefab == null) continue;

                var no = Instantiate(prefab, s.transform.position, s.transform.rotation);
                no.Spawn(true);
                spawned.Add(no);
                caps++;
            }

            Debug.Log($"[DoorCap] Spawned connectors={connectors}, caps={caps} (sockets={allSockets.Count})");
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
