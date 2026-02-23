using System;
using System.Collections.Generic;
using DungeonGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Server-side socket-snap generator for Spire_Slice.
    /// MVP+: generates a main path + branches + loop connectors by snapping room prefabs via RoomSockets.
    /// 
    /// Design goals:
    /// - Deterministic from SpireSeed
    /// - 90° yaw rotations only (for stability)
    /// - Spawn rooms as NetworkObjects for MVP (easy sync)
    /// - Generator produces a pure layout recipe (SpireLayoutData) so we can later switch to local instantiation.
    /// </summary>
    [RequireComponent(typeof(SpireSeed))]
    public class SpireLayoutGenerator : NetworkBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Room prefabs to use. Each should have RoomPrefab + NetworkObject on the root.")]
        [SerializeField] private List<RoomPrefab> roomPrefabs = new();

        [Header("Shape")]
        [SerializeField, Min(1)] private int mainPathRooms = 20;
        [SerializeField, Range(0, 10)] private int branches = 4;
        [SerializeField, Min(1)] private int branchLengthMin = 2;
        [SerializeField, Min(1)] private int branchLengthMax = 6;

        [Header("Loops")]
        [Tooltip("How many loop connections to attempt after building the tree.")]
        [SerializeField, Range(0, 10)] private int loopAttempts = 2;

        [Tooltip("How close sockets must be to connect via a connector room.")]
        [SerializeField, Range(0.1f, 5f)] private float loopSocketSnapTolerance = 0.6f;

        [Header("Landmarks")]
        [Tooltip("Place a landmark about every N placements along the main path. 0 disables.")]
        [SerializeField, Range(0, 20)] private int landmarkEvery = 8;

        [Header("Sockets")]
        [SerializeField] private SocketType socketType = SocketType.DoorSmall;
        [SerializeField] private int socketSize = 0;

        [Header("Placement")]
        [Tooltip("Extra spacing buffer for overlap checks.")]
        [SerializeField] private Vector3 overlapPadding = new(0.25f, 0.25f, 0.25f);

        [Tooltip("Max attempts when trying to place a room.")]
        [SerializeField, Range(5, 200)] private int maxAttemptsPerPlacement = 50;

        [Tooltip("Optional starting room prefab. If null, a random room is chosen.")]
        [SerializeField] private RoomPrefab startRoom;

        private SpireSeed spireSeed;

        private readonly List<PlacedRoom> placed = new();
        private readonly List<OpenSocket> openSockets = new();

        private readonly HashSet<string> usedUniqueRoomIds = new();
        private readonly List<string> recentRoomIds = new();

        private System.Random rng;

        private class PlacedRoom
        {
            public RoomPrefab prefab; // instance component
            public Transform root;
            public Bounds worldBounds;
            public HashSet<string> usedSocketPaths; // relative paths from room root
        }

        private struct OpenSocket
        {
            public RoomSocket socket; // instance socket ref
            public Transform roomRoot;
        }

        private void Awake()
        {
            spireSeed = GetComponent<SpireSeed>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            SpireSeed.OnSeedChanged += HandleSeed;

            // If seed already available, generate immediately.
            if (spireSeed != null && spireSeed.Seed != 0)
            {
                HandleSeed(spireSeed.Seed);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SpireSeed.OnSeedChanged -= HandleSeed;
            }
            base.OnNetworkDespawn();
        }

        private void HandleSeed(int seed)
        {
            if (!IsServer) return;

            rng = spireSeed.CreateRandom("layout");

            ClearExistingGeneratedRooms();

            var layout = GenerateLayout(seed);
            SpawnLayout(layout);
        }

        private void ClearExistingGeneratedRooms()
        {
            // Destroy previously generated room roots (children of this generator object).
            // Note: if you want persistence across loads, parent elsewhere.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                var no = child.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned)
                {
                    no.Despawn(true);
                }
                else
                {
                    Destroy(child.gameObject);
                }
            }

            placed.Clear();
            openSockets.Clear();
            usedUniqueRoomIds.Clear();
            recentRoomIds.Clear();
        }

        public SpireLayoutData GenerateLayout(int seed)
        {
            if (roomPrefabs.Count == 0)
            {
                Debug.LogError("[SpireGen] No room prefabs assigned.");
                return new SpireLayoutData { seed = seed };
            }

            var data = new SpireLayoutData { seed = seed };

            // Place start room at origin.
            var start = startRoom != null ? startRoom : PickRoomPrefab(null, forceLandmark: false, forceConnector: false);
            var startRot = Rotation90(rng.Next(0, 4));
            var startPlacement = PlaceRoom(start, Vector3.zero, startRot);
            if (startPlacement == null || startPlacement.root == null)
            {
                Debug.LogError("[SpireGen] Failed to place start room.");
                return data;
            }

            int startIndex = data.rooms.Count;
            data.rooms.Add(new RoomPlacement
            {
                roomId = start.roomId,
                position = startPlacement.root.position,
                rotationY = (int)startRot.eulerAngles.y
            });

            NotePlaced(start.roomId, start);

            // Main path: always extend from the newest room's available sockets.
            for (int i = 0; i < mainPathRooms - 1; i++)
            {
                bool placeLandmark = landmarkEvery > 0 && i > 0 && (i % landmarkEvery == 0);

                if (!TryExtendOnce(data, preferRandomSocket: false, forceLandmark: placeLandmark))
                {
                    Debug.LogWarning($"[SpireGen] Main path extension failed at i={i}.");
                    break;
                }
            }

            // Branches: extend from random open sockets.
            for (int b = 0; b < branches; b++)
            {
                int len = rng.Next(branchLengthMin, branchLengthMax + 1);
                for (int i = 0; i < len; i++)
                {
                    if (!TryExtendOnce(data, preferRandomSocket: true, forceLandmark: false)) break;
                }
            }

            // Loops: attempt to connect two existing open sockets using a connector room.
            for (int l = 0; l < loopAttempts; l++)
            {
                TryCreateLoop(data);
            }

            return data;
        }

        private bool TryExtendOnce(SpireLayoutData data, bool preferRandomSocket, bool forceLandmark)
        {
            if (openSockets.Count == 0)
            {
                CacheOpenSockets();
                if (openSockets.Count == 0) return false;
            }

            PruneOpenSockets();
            if (openSockets.Count == 0) return false;

            // Choose a target open socket.
            OpenSocket target = preferRandomSocket
                ? openSockets[rng.Next(0, openSockets.Count)]
                : openSockets[openSockets.Count - 1];

            if (target.socket == null || target.roomRoot == null)
            {
                // Should not happen after pruning, but be defensive.
                openSockets.RemoveAll(s => s.socket == null || s.roomRoot == null);
                return false;
            }

            // Try place a room connected to it.
            for (int attempt = 0; attempt < maxAttemptsPerPlacement; attempt++)
            {
                var prefabAsset = PickRoomPrefab(target.socket, forceLandmark: forceLandmark, forceConnector: false);
                if (prefabAsset == null) continue;

                if (!TryGetCompatibleSocket(prefabAsset, out var sourceSocketAsset)) continue;

                string sourceSocketPath = GetRelativePath(prefabAsset.transform, sourceSocketAsset.transform);

                // Determine rotation and position so that sourceSocket faces opposite of target.socket.
                var initialRot = Rotation90(rng.Next(0, 4));
                var aligned = ComputeSnapTransform(target, sourceSocketAsset, initialRot);

                var placedRoom = PlaceRoom(prefabAsset, aligned.pos, aligned.rot);
                if (placedRoom == null || placedRoom.root == null) continue;

                // Resolve socket instance on placed room.
                var sourceSocketInstance = FindSocketInstance(placedRoom.root, sourceSocketPath);
                if (sourceSocketInstance == null)
                {
                    Debug.LogWarning($"[SpireGen] Could not resolve socket instance path '{sourceSocketPath}' on '{placedRoom.root.name}'.");
                }

                // Mark sockets used.
                MarkSocketUsed(target.roomRoot, target.socket);
                if (sourceSocketInstance != null)
                {
                    MarkSocketUsed(placedRoom.root, sourceSocketInstance);
                }

                // Remove used target socket from open list.
                openSockets.RemoveAll(s => s.socket == target.socket && s.roomRoot == target.roomRoot);

                int newRoomIndex = data.rooms.Count;
                data.rooms.Add(new RoomPlacement
                {
                    roomId = prefabAsset.roomId,
                    position = placedRoom.root.position,
                    rotationY = (int)placedRoom.root.rotation.eulerAngles.y
                });

                // Record explicit connection (A = target/existing socket, B = new room socket)
                if (sourceSocketInstance != null)
                {
                    data.connections.Add(new SocketConnection
                    {
                        a = new SocketRef
                        {
                            roomIndex = FindRoomIndexByRoot(data, target.roomRoot),
                            socketPath = GetRelativePath(target.roomRoot, target.socket.transform),
                            socketType = target.socket.socketType,
                            size = target.socket.size,
                        },
                        b = new SocketRef
                        {
                            roomIndex = newRoomIndex,
                            socketPath = sourceSocketPath,
                            socketType = sourceSocketInstance.socketType,
                            size = sourceSocketInstance.size,
                        }
                    });
                }

                NotePlaced(prefabAsset.roomId, prefabAsset);

                // Add new room sockets to open list.
                AddOpenSocketsForRoom(placedRoom.root, placedRoom.prefab);

                return true;
            }

            // If we failed to extend from this socket, drop it.
            openSockets.RemoveAll(s => s.socket == target.socket && s.roomRoot == target.roomRoot);
            return false;
        }

        private void CacheOpenSockets()
        {
            openSockets.Clear();
            foreach (var pr in placed)
            {
                if (pr == null || pr.root == null || pr.prefab == null) continue;
                AddOpenSocketsForRoom(pr.root, pr.prefab);
            }
            PruneOpenSockets();
        }

        private void PruneOpenSockets()
        {
            // Remove any stale/destroyed sockets (can happen during edit-time changes or scene reloads).
            openSockets.RemoveAll(s => s.socket == null || s.roomRoot == null);
        }

        private void AddOpenSocketsForRoom(Transform roomRoot, RoomPrefab prefabInstance)
        {
            foreach (var sock in prefabInstance.GetSockets(socketType, socketSize))
            {
                if (sock == null) continue;
                if (IsSocketUsed(roomRoot, sock)) continue;
                openSockets.Add(new OpenSocket { socket = sock, roomRoot = roomRoot });
            }

            // Prefer non-lowPriority sockets by sorting.
            openSockets.Sort((a, b) => (a.socket.lowPriority ? 1 : 0).CompareTo(b.socket.lowPriority ? 1 : 0));
        }

        public bool IsSocketUsed(Transform roomRoot, RoomSocket socket)
        {
            if (roomRoot == null || socket == null) return false;

            string path = GetRelativePath(roomRoot, socket.transform);

            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i] == null) continue;
                if (placed[i].root != roomRoot) continue;
                return placed[i].usedSocketPaths != null && placed[i].usedSocketPaths.Contains(path);
            }
            return false;
        }

        private void MarkSocketUsed(Transform roomRoot, RoomSocket socket)
        {
            if (roomRoot == null || socket == null) return;

            string path = GetRelativePath(roomRoot, socket.transform);

            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i] == null) continue;
                if (placed[i].root != roomRoot) continue;

                placed[i].usedSocketPaths ??= new HashSet<string>();
                placed[i].usedSocketPaths.Add(path);
                return;
            }
        }

        private bool TryGetCompatibleSocket(RoomPrefab prefabAsset, out RoomSocket socket)
        {
            foreach (var s in prefabAsset.GetSockets(socketType, socketSize))
            {
                socket = s;
                return true;
            }

            socket = null;
            return false;
        }

        private (Vector3 pos, Quaternion rot) ComputeSnapTransform(OpenSocket target, RoomSocket sourceSocketAsset, Quaternion initialRoomRot)
        {
            if (target.socket == null || sourceSocketAsset == null)
            {
                return (Vector3.zero, initialRoomRot);
            }

            // We want: sourceSocket forward points opposite target socket forward.
            // We'll brute force the yaw rotations (0/90/180/270) around initialRoomRot.

            var sourceLocalPos = sourceSocketAsset.transform.localPosition;
            var sourceLocalRot = sourceSocketAsset.transform.localRotation;

            Quaternion best = initialRoomRot;
            float bestDot = -999f;

            var tf = target.socket.transform.forward;
            tf.y = 0; tf.Normalize();

            for (int step = 0; step < 4; step++)
            {
                var r = Rotation90(step) * initialRoomRot;
                var f = (r * sourceLocalRot) * Vector3.forward;
                f.y = 0; f.Normalize();

                float dot = Vector3.Dot(f, -tf);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = r;
                }
            }

            var finalRot = best;
            var finalSourceWorldPos = finalRot * sourceLocalPos;
            var targetPos = target.socket.transform.position;
            var pos = targetPos - finalSourceWorldPos;

            return (pos, finalRot);
        }

        private bool TryCreateLoop(SpireLayoutData data)
        {
            if (openSockets.Count < 2)
            {
                CacheOpenSockets();
                if (openSockets.Count < 2) return false;
            }

            // Try random pairs.
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var a = openSockets[rng.Next(0, openSockets.Count)];
                var b = openSockets[rng.Next(0, openSockets.Count)];
                if (a.roomRoot == b.roomRoot && a.socket == b.socket) continue;

                // Choose a connector room asset.
                var connector = PickRoomPrefab(a.socket, forceLandmark: false, forceConnector: true);
                if (connector == null) return false;

                // Connector must have at least 2 compatible sockets.
                var sockets = new List<RoomSocket>();
                foreach (var s in connector.GetSockets(socketType, socketSize)) sockets.Add(s);
                if (sockets.Count < 2) continue;

                // Use first two compatible sockets as endpoints.
                var s0 = sockets[0];
                var s1 = sockets[1];
                string s0Path = GetRelativePath(connector.transform, s0.transform);
                string s1Path = GetRelativePath(connector.transform, s1.transform);

                // Try 4 rotations.
                for (int rotStep = 0; rotStep < 4; rotStep++)
                {
                    var rot = Rotation90(rotStep);
                    var align = ComputeSnapTransform(a, s0, rot);

                    // Compute the world pose of socket1 for this placement.
                    var s1WorldPos = align.rot * s1.transform.localPosition + align.pos;

                    // Check if socket1 lands near socket B.
                    float dist = Vector3.Distance(s1WorldPos, b.socket.transform.position);
                    if (dist > loopSocketSnapTolerance) continue;

                    // Check forward alignment.
                    var s1Forward = (align.rot * s1.transform.localRotation) * Vector3.forward;
                    s1Forward.y = 0; s1Forward.Normalize();

                    var bForward = b.socket.transform.forward;
                    bForward.y = 0; bForward.Normalize();

                    if (Vector3.Dot(s1Forward, -bForward) < 0.92f) continue;

                    // Place connector.
                    var placedConn = PlaceRoom(connector, align.pos, align.rot);
                    if (placedConn == null || placedConn.root == null) continue;

                    var s0Inst = FindSocketInstance(placedConn.root, s0Path);
                    var s1Inst = FindSocketInstance(placedConn.root, s1Path);

                    // Mark both existing sockets and connector sockets used.
                    MarkSocketUsed(a.roomRoot, a.socket);
                    MarkSocketUsed(b.roomRoot, b.socket);
                    if (s0Inst != null) MarkSocketUsed(placedConn.root, s0Inst);
                    if (s1Inst != null) MarkSocketUsed(placedConn.root, s1Inst);

                    // Remove used open sockets.
                    openSockets.RemoveAll(s => (s.roomRoot == a.roomRoot && s.socket == a.socket) || (s.roomRoot == b.roomRoot && s.socket == b.socket));

                    data.rooms.Add(new RoomPlacement
                    {
                        roomId = connector.roomId,
                        position = placedConn.root.position,
                        rotationY = (int)placedConn.root.rotation.eulerAngles.y
                    });

                    NotePlaced(connector.roomId, connector);

                    // Add any additional sockets as open (if the connector has more than 2).
                    AddOpenSocketsForRoom(placedConn.root, placedConn.prefab);

                    Debug.Log("[SpireGen] Created loop connector.");
                    return true;
                }
            }

            return false;
        }

        private PlacedRoom PlaceRoom(RoomPrefab prefabAsset, Vector3 pos, Quaternion rot)
        {
            // Instantiate under generator for cleanup.
            var go = Instantiate(prefabAsset.gameObject, pos, rot, transform);

            // Overlap check (simple AABB in world space using renderers/colliders).
            var bounds = ComputeWorldBounds(go);
            bounds.Expand(overlapPadding);
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i] == null) continue;
                if (bounds.Intersects(placed[i].worldBounds))
                {
                    Destroy(go);
                    return null;
                }
            }

            // Spawn network object.
            var no = go.GetComponent<NetworkObject>();
            if (no != null && !no.IsSpawned)
            {
                no.Spawn(true);
            }

            var rpInstance = go.GetComponentInChildren<RoomPrefab>(true);
            if (rpInstance == null)
            {
                Debug.LogError($"[SpireGen] Spawned room is missing RoomPrefab component: {go.name}");
                Destroy(go);
                return null;
            }

            // Use the RoomPrefab's transform as the logical room root (in case the component isn't on the GO root).
            var pr = new PlacedRoom
            {
                prefab = rpInstance,
                root = rpInstance.transform,
                worldBounds = bounds,
                usedSocketPaths = new HashSet<string>()
            };

            placed.Add(pr);
            return pr;
        }

        private static Bounds ComputeWorldBounds(GameObject go)
        {
            // Prefer colliders; fall back to renderers.
            var cols = go.GetComponentsInChildren<Collider>();
            bool has = false;
            Bounds b = new Bounds(go.transform.position, Vector3.one);
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (!has)
                {
                    b = c.bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(c.bounds);
                }
            }

            if (has) return b;

            var rends = go.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!has)
                {
                    b = r.bounds;
                    has = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (!has)
            {
                b = new Bounds(go.transform.position, Vector3.one);
            }

            return b;
        }

        private RoomPrefab PickRoomPrefab(RoomSocket connectingTo, bool forceLandmark, bool forceConnector)
        {
            // Weighted selection with repeat cooldown + unique-per-slice + landmark/connector filters.
            var candidates = new List<RoomPrefab>();
            int totalWeight = 0;

            foreach (var p in roomPrefabs)
            {
                if (p == null) continue;
                if (!HasCompatibleSocket(p)) continue;

                if (forceLandmark && !p.landmark) continue;
                if (forceConnector && !p.connector) continue;

                // Uniques: explicit uniquePerSlice or implicit landmark uniqueness.
                if ((p.uniquePerSlice || p.landmark) && usedUniqueRoomIds.Contains(p.roomId)) continue;

                // Repeat cooldown.
                if (p.repeatCooldown > 0 && recentRoomIds.Contains(p.roomId)) continue;

                int w = Mathf.Max(0, p.weight);
                if (w <= 0) continue;

                candidates.Add(p);
                totalWeight += w;
            }

            if (candidates.Count == 0)
            {
                // If landmark was requested but none available, fall back to normal.
                if (forceLandmark)
                {
                    return PickRoomPrefab(connectingTo, forceLandmark: false, forceConnector: forceConnector);
                }
                return null;
            }

            int roll = rng.Next(0, totalWeight);
            foreach (var c in candidates)
            {
                roll -= Mathf.Max(0, c.weight);
                if (roll < 0) return c;
            }

            return candidates[candidates.Count - 1];
        }

        private bool HasCompatibleSocket(RoomPrefab prefab)
        {
            foreach (var _ in prefab.GetSockets(socketType, socketSize))
            {
                return true;
            }
            return false;
        }

        private void NotePlaced(string roomId, RoomPrefab prefab)
        {
            if (prefab != null && (prefab.uniquePerSlice || prefab.landmark))
            {
                usedUniqueRoomIds.Add(roomId);
            }

            // Maintain a rolling set of recent room IDs for cooldown.
            recentRoomIds.Add(roomId);

            // Global cap = worst-case cooldown across prefabs.
            int maxCooldown = 0;
            foreach (var p in roomPrefabs)
            {
                if (p == null) continue;
                if (p.repeatCooldown > maxCooldown) maxCooldown = p.repeatCooldown;
            }

            int keep = Mathf.Clamp(maxCooldown, 0, 100);
            if (keep == 0)
            {
                recentRoomIds.Clear();
                return;
            }

            while (recentRoomIds.Count > keep)
            {
                recentRoomIds.RemoveAt(0);
            }
        }

        private static Quaternion Rotation90(int steps)
        {
            int s = ((steps % 4) + 4) % 4;
            return Quaternion.Euler(0f, 90f * s, 0f);
        }

        private int FindRoomIndexByRoot(SpireLayoutData data, Transform roomRoot)
        {
            // Rooms list index corresponds to generation order.
            // We locate by matching position/rotation against stored placements.
            // This is MVP-safe because we only call it for sockets in existing placed rooms.
            if (data == null || roomRoot == null) return 0;

            for (int i = 0; i < data.rooms.Count; i++)
            {
                var rp = data.rooms[i];
                // Position match with tolerance.
                if ((rp.position - roomRoot.position).sqrMagnitude < 0.0001f)
                {
                    return i;
                }
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

        private static RoomSocket FindSocketInstance(Transform roomRoot, string relativePath)
        {
            if (roomRoot == null) return null;
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var t = roomRoot.Find(relativePath);
            if (t == null) return null;
            return t.GetComponent<RoomSocket>();
        }

        public static event Action<SpireLayoutGenerator, SpireLayoutData> OnLayoutGenerated;

        private void SpawnLayout(SpireLayoutData layout)
        {
            // Currently, GenerateLayout already instantiates/spawns.
            // This method exists for the future "recipe" backend.
            Debug.Log($"[SpireGen] Generated layout: rooms={layout.rooms.Count}, seed={layout.seed}");
            OnLayoutGenerated?.Invoke(this, layout);
        }
    }
}
