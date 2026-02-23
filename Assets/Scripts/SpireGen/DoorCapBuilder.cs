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

            // Pair sockets by proximity + facing. This supports seams/gaps (socket positions won't be identical).
            const float FacingDotThreshold = -0.9f; // mostly opposite
            float connectTolerance = 0.8f; // meters; adjust if needed

            int connectors = 0;
            int caps = 0;

            var paired = new HashSet<RoomSocket>();

            // Deterministic order: sort by position then name.
            allSockets.Sort((a, b) =>
            {
                int c = a.transform.position.x.CompareTo(b.transform.position.x);
                if (c != 0) return c;
                c = a.transform.position.z.CompareTo(b.transform.position.z);
                if (c != 0) return c;
                return string.CompareOrdinal(a.name, b.name);
            });

            for (int i = 0; i < allSockets.Count; i++)
            {
                var a = allSockets[i];
                if (a == null) continue;
                if (paired.Contains(a)) continue;

                RoomSocket best = null;
                float bestDist = float.MaxValue;

                for (int j = i + 1; j < allSockets.Count; j++)
                {
                    var b = allSockets[j];
                    if (b == null) continue;
                    if (paired.Contains(b)) continue;

                    if (a.socketType != b.socketType) continue;
                    if (a.size != b.size) continue;

                    float d = Vector3.Distance(a.transform.position, b.transform.position);
                    if (d > connectTolerance) continue;

                    var af = a.transform.forward; af.y = 0; af.Normalize();
                    var bf = b.transform.forward; bf.y = 0; bf.Normalize();
                    if (Vector3.Dot(af, bf) > FacingDotThreshold) continue;

                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = b;
                    }
                }

                if (best != null)
                {
                    // Connected pair: spawn ONE connector at the midpoint.
                    var prefab = PickPrefab(a.socketType, connected: true);
                    if (prefab != null)
                    {
                        var pos = (a.transform.position + best.transform.position) * 0.5f;
                        var rot = a.transform.rotation;
                        var no = Instantiate(prefab, pos, rot);
                        no.Spawn(true);
                        spawned.Add(no);
                        connectors++;
                    }

                    paired.Add(a);
                    paired.Add(best);
                }
            }

            // Unpaired sockets get caps.
            foreach (var s in allSockets)
            {
                if (s == null) continue;
                if (paired.Contains(s)) continue;

                var prefab = PickPrefab(s.socketType, connected: false);
                if (prefab == null) continue;

                var no = Instantiate(prefab, s.transform.position, s.transform.rotation);
                no.Spawn(true);
                spawned.Add(no);
                caps++;
            }

            Debug.Log($"[DoorCap] Spawned connectors={connectors}, caps={caps} (sockets={allSockets.Count}, tol={connectTolerance})");
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
