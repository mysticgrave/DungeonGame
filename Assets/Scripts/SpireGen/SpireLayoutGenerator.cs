using System;
using System.Collections;
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

        [Header("Boss / End Room")]
        [Tooltip("The final destination room placed at the end of the main path. Must have at least 1 compatible socket. Excluded from the normal room pool.")]
        [SerializeField] private RoomPrefab bossRoom;

        [Tooltip("Minimum number of rooms on the main path before the boss room can be placed. If the main path is shorter, the boss room is placed at the end regardless.")]
        [SerializeField, Min(1)] private int minRoomsBeforeBoss = 10;

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

        private Coroutine _generateCoroutine;

        private void HandleSeed(int seed)
        {
            if (!IsServer) return;

            if (_generateCoroutine != null)
                StopCoroutine(_generateCoroutine);

            _generateCoroutine = StartCoroutine(GenerateCoroutine(seed));
        }

        private IEnumerator GenerateCoroutine(int seed)
        {
            rng = spireSeed.CreateRandom("layout");
            ClearExistingGeneratedRooms();

            var data = new SpireLayoutData { seed = seed };
            yield return StartCoroutine(GenerateLayoutCoroutine(seed, data));

            SpawnLayout(data);
            _generateCoroutine = null;
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

        [Header("Debug")]
        [Tooltip("Max total backtracks across the entire main-path build.")]
        [SerializeField, Range(0, 50)] private int maxBacktracks = 20;

        [Tooltip("Yield to the next frame after this many placement attempts to prevent freezing.")]
        [SerializeField, Range(5, 100)] private int attemptsPerFrame = 20;

        /// <summary>
        /// Global counter incremented each time PlaceRoom instantiates + tests a room.
        /// Used to yield periodically in the coroutine.
        /// </summary>
        private int _placementAttempts;

        private IEnumerator GenerateLayoutCoroutine(int seed, SpireLayoutData data)
        {
            if (roomPrefabs.Count == 0)
            {
                Debug.LogError("[SpireGen] No room prefabs assigned.");
                yield break;
            }

            _placementAttempts = 0;

            // ── Phase 1: Start room ──
            var start = startRoom != null ? startRoom : PickRoomPrefab(null, forceLandmark: false, forceConnector: false);
            var startRot = Rotation90(rng.Next(0, 4));
            var startPlacement = PlaceRoom(start, Vector3.zero, startRot);
            if (startPlacement == null || startPlacement.root == null)
            {
                Debug.LogError("[SpireGen] Failed to place start room.");
                yield break;
            }

            data.rooms.Add(new RoomPlacement
            {
                roomId = start.roomId,
                position = startPlacement.root.position,
                rotationY = (int)startRot.eulerAngles.y
            });
            NotePlaced(start.roomId, start);

            // ── Phase 2: Build linear main path (start → boss) ──
            var mainPath = new List<PlacedRoom> { startPlacement };
            yield return StartCoroutine(BuildMainPathCoroutine(data, mainPath));

            Debug.Log($"[SpireGen] Main path: {mainPath.Count} rooms.");

            // ── Phase 3: Boss room at the tip ──
            if (bossRoom != null)
            {
                if (mainPath.Count < minRoomsBeforeBoss)
                    Debug.LogWarning($"[SpireGen] Main path only reached {mainPath.Count} rooms (minimum {minRoomsBeforeBoss} before boss). Placing boss anyway.");

                if (TryPlaceBossRoom(data, mainPath))
                    Debug.Log($"[SpireGen] Boss room placed at index {data.bossRoomIndex} after {mainPath.Count} main-path rooms.");
                else
                    Debug.LogError("[SpireGen] Failed to place boss room!");
            }

            yield return null;

            // ── Phase 4: Branches off main path open sockets ──
            yield return StartCoroutine(BuildBranchesCoroutine(data, mainPath));

            // ── Phase 5: Loop connectors ──
            for (int l = 0; l < loopAttempts; l++)
                TryCreateLoop(data);

            Debug.Log($"[SpireGen] GenerateLayout done: rooms={data.rooms.Count}, connections={data.connections.Count}");
        }

        /// <summary>Check if we should yield to the next frame to keep the app responsive.</summary>
        private IEnumerator YieldIfNeeded()
        {
            if (_placementAttempts >= attemptsPerFrame)
            {
                _placementAttempts = 0;
                yield return null;
            }
        }

        /// <summary>
        /// Builds a strictly linear main path by only extending from the tip room.
        /// If the tip is stuck, backtracks (removes the tip) and retries with a different piece.
        /// Yields periodically so the app stays responsive.
        /// </summary>
        private IEnumerator BuildMainPathCoroutine(SpireLayoutData data, List<PlacedRoom> mainPath)
        {
            int totalBacktracks = 0;

            while (mainPath.Count < mainPathRooms)
            {
                yield return StartCoroutine(YieldIfNeeded());

                var tip = mainPath[mainPath.Count - 1];
                bool placeLandmark = landmarkEvery > 0 && mainPath.Count > 1 && (mainPath.Count % landmarkEvery == 0);

                bool extended = TryExtendFromRoom(data, tip, minSockets: 2, forceLandmark: placeLandmark, mainPath);
                if (!extended)
                    extended = TryExtendFromRoom(data, tip, minSockets: 2, forceLandmark: false, mainPath);
                if (!extended)
                    extended = TryExtendFromRoom(data, tip, minSockets: 1, forceLandmark: false, mainPath);

                if (extended)
                    continue;

                // Stuck at the tip — backtrack.
                if (mainPath.Count <= 1 || totalBacktracks >= maxBacktracks)
                {
                    Debug.LogWarning($"[SpireGen] Main path stuck at {mainPath.Count} rooms after {totalBacktracks} total backtracks.");
                    break;
                }

                Debug.Log($"[SpireGen] Backtracking at room {mainPath.Count} (backtrack {totalBacktracks + 1}/{maxBacktracks}).");
                UndoLastMainPathRoom(data, mainPath);
                totalBacktracks++;
            }
        }

        /// <summary>
        /// Try to extend the main path from a specific room's unused sockets.
        /// Tries every prefab × every socket × 4 rotations before giving up.
        /// </summary>
        private bool TryExtendFromRoom(SpireLayoutData data, PlacedRoom tip, int minSockets, bool forceLandmark, List<PlacedRoom> mainPath)
        {
            // Get unused sockets on the tip room.
            var tipSockets = new List<RoomSocket>();
            foreach (var sock in tip.prefab.GetSockets(socketType, socketSize))
            {
                if (sock == null) continue;
                if (IsSocketUsed(tip.root, sock)) continue;
                tipSockets.Add(sock);
            }
            ShuffleList(tipSockets);
            if (tipSockets.Count == 0) return false;

            // Build candidate prefabs (shuffled).
            var candidates = new List<RoomPrefab>();
            foreach (var p in roomPrefabs)
            {
                if (p == null) continue;
                if (CountCompatibleSockets(p) < minSockets) continue;
                if (!PassesMainPathFilters(p, forceLandmark)) continue;
                candidates.Add(p);
            }
            ShuffleList(candidates);
            if (candidates.Count == 0 && forceLandmark)
            {
                // Relax landmark requirement.
                foreach (var p in roomPrefabs)
                {
                    if (p == null) continue;
                    if (CountCompatibleSockets(p) < minSockets) continue;
                    if (!PassesMainPathFilters(p, false)) continue;
                    candidates.Add(p);
                }
                ShuffleList(candidates);
            }
            if (candidates.Count == 0) return false;

            foreach (var targetSocket in tipSockets)
            {
                var targetOpen = new OpenSocket { socket = targetSocket, roomRoot = tip.root };

                foreach (var prefabAsset in candidates)
                {
                    var sourceSockets = GetCompatibleSockets(prefabAsset);
                    ShuffleList(sourceSockets);

                    foreach (var sourceSocketAsset in sourceSockets)
                    {
                        string sourceSocketPath = GetRelativePath(prefabAsset.transform, sourceSocketAsset.transform);
                        var aligned = ComputeSnapTransform(targetOpen, sourceSocketAsset, Rotation90(rng.Next(0, 4)));

                        var placedRoom = PlaceRoom(prefabAsset, aligned.pos, aligned.rot);
                        if (placedRoom == null || placedRoom.root == null) continue;

                        var sourceSocketInstance = FindSocketInstanceById(placedRoom.prefab, sourceSocketAsset.socketId)
                            ?? FindSocketInstance(placedRoom.root, sourceSocketPath);

                        // Success — wire it up.
                        MarkSocketUsed(tip.root, targetSocket);
                        if (sourceSocketInstance != null)
                            MarkSocketUsed(placedRoom.root, sourceSocketInstance);

                        data.rooms.Add(new RoomPlacement
                        {
                            roomId = prefabAsset.roomId,
                            position = placedRoom.root.position,
                            rotationY = (int)placedRoom.root.rotation.eulerAngles.y
                        });

                        if (sourceSocketInstance != null)
                            RecordConnection(data, targetOpen, placedRoom.root, sourceSocketInstance);

                        NotePlaced(prefabAsset.roomId, prefabAsset);
                        mainPath.Add(placedRoom);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Filter for main-path room selection. Skips boss, respects unique/cooldown.</summary>
        private bool PassesMainPathFilters(RoomPrefab p, bool forceLandmark)
        {
            if (!HasCompatibleSocket(p)) return false;
            if (bossRoom != null && p.roomId == bossRoom.roomId) return false;
            if (forceLandmark && !p.landmark) return false;
            if ((p.uniquePerSlice || p.landmark) && usedUniqueRoomIds.Contains(p.roomId)) return false;
            if (p.weight <= 0) return false;
            // Intentionally skip repeatCooldown for the main path — reaching target length is more important.
            return true;
        }

        /// <summary>Remove the last room on the main path so we can try a different piece.</summary>
        private void UndoLastMainPathRoom(SpireLayoutData data, List<PlacedRoom> mainPath)
        {
            if (mainPath.Count <= 1) return;

            var tip = mainPath[mainPath.Count - 1];
            mainPath.RemoveAt(mainPath.Count - 1);

            // Remove from placed list.
            placed.Remove(tip);

            // Remove from data.rooms (last entry).
            if (data.rooms.Count > 0)
                data.rooms.RemoveAt(data.rooms.Count - 1);

            // Remove any connections referencing this room's NetworkObject.
            if (tip.root != null)
            {
                var tipNet = tip.root.GetComponentInParent<NetworkObject>();
                if (tipNet != null)
                {
                    ulong tipId = tipNet.NetworkObjectId;
                    data.connections.RemoveAll(c => c.a.roomNetId == tipId || c.b.roomNetId == tipId);
                }
            }

            // Restore the socket on the previous tip that was used to connect to this room.
            // We do this by clearing the used socket path that matched.
            var prevTip = mainPath[mainPath.Count - 1];
            if (prevTip.usedSocketPaths != null && prevTip.usedSocketPaths.Count > 0)
            {
                // Remove the most recently added used socket (the one that connected to the removed room).
                // Since HashSet doesn't track order, we re-check which sockets are actually connected.
                var toRestore = new List<string>();
                foreach (var path in prevTip.usedSocketPaths)
                {
                    var sockTransform = prevTip.root.Find(path);
                    if (sockTransform == null) continue;
                    // If no placed room is connected at this socket position anymore, restore it.
                    bool stillConnected = false;
                    foreach (var pr in placed)
                    {
                        if (pr == prevTip) continue;
                        if (pr.root == null) continue;
                        foreach (var s in pr.prefab.GetSockets(socketType, socketSize))
                        {
                            if (s == null) continue;
                            if (Vector3.Distance(s.transform.position, sockTransform.position) < 0.5f)
                            {
                                stillConnected = true;
                                break;
                            }
                        }
                        if (stillConnected) break;
                    }
                    if (!stillConnected)
                        toRestore.Add(path);
                }
                foreach (var p in toRestore)
                    prevTip.usedSocketPaths.Remove(p);
            }

            // Despawn and destroy the removed room.
            if (tip.root != null)
            {
                var no = tip.root.GetComponentInParent<NetworkObject>();
                if (no != null && no.IsSpawned)
                    no.Despawn(true);
                else if (tip.root.gameObject != null)
                    Destroy(tip.root.gameObject);
            }
        }

        /// <summary>
        /// Builds branches off the main path. Each branch is a linear chain grown from
        /// an unused socket on a main-path room, with retries and backtracking.
        /// Yields periodically so the app stays responsive.
        /// </summary>
        private IEnumerator BuildBranchesCoroutine(SpireLayoutData data, List<PlacedRoom> mainPath)
        {
            var branchPoints = new List<PlacedRoom>();
            foreach (var room in mainPath)
            {
                if (room.root == null) continue;
                foreach (var sock in room.prefab.GetSockets(socketType, socketSize))
                {
                    if (sock == null) continue;
                    if (IsSocketUsed(room.root, sock)) continue;
                    branchPoints.Add(room);
                    break;
                }
            }
            ShuffleList(branchPoints);

            int branchesBuilt = 0;
            foreach (var branchRoot in branchPoints)
            {
                if (branchesBuilt >= branches) break;

                int targetLen = rng.Next(branchLengthMin, branchLengthMax + 1);
                var branch = new List<PlacedRoom> { branchRoot };
                int branchBacktracks = 0;
                int maxBranchBacktracks = Mathf.Max(5, targetLen * 2);

                while (branch.Count - 1 < targetLen)
                {
                    yield return StartCoroutine(YieldIfNeeded());

                    var tip = branch[branch.Count - 1];
                    bool isLastRoom = (branch.Count - 1 == targetLen - 1);
                    int minSock = isLastRoom ? 1 : 2;

                    bool extended = TryExtendFromRoom(data, tip, minSockets: minSock, forceLandmark: false, mainPath: branch);
                    if (!extended && minSock > 1)
                        extended = TryExtendFromRoom(data, tip, minSockets: 1, forceLandmark: false, mainPath: branch);

                    if (extended)
                        continue;

                    if (branch.Count <= 1 || branchBacktracks >= maxBranchBacktracks)
                        break;

                    UndoLastMainPathRoom(data, branch);
                    branchBacktracks++;
                }

                int branchLen = branch.Count - 1;
                if (branchLen >= branchLengthMin)
                {
                    branchesBuilt++;
                    Debug.Log($"[SpireGen] Branch {branchesBuilt}/{branches}: {branchLen} rooms.");
                }
                else if (branchLen > 0)
                {
                    Debug.Log($"[SpireGen] Branch too short ({branchLen} < {branchLengthMin}), removing.");
                    while (branch.Count > 1)
                        UndoLastMainPathRoom(data, branch);
                }
            }

            if (branchesBuilt < branches)
                Debug.LogWarning($"[SpireGen] Only built {branchesBuilt}/{branches} branches (not enough free sockets on main path).");
        }

        private void RecordConnection(SpireLayoutData data, OpenSocket target, Transform newRoomRoot, RoomSocket newSocket)
        {
            var aNet = target.roomRoot.GetComponentInParent<NetworkObject>();
            var bNet = newRoomRoot.GetComponentInParent<NetworkObject>();

            data.connections.Add(new SocketConnection
            {
                a = new SocketRef
                {
                    roomNetId = aNet != null ? aNet.NetworkObjectId : 0,
                    socketId = target.socket.socketId,
                    socketType = target.socket.socketType,
                    size = target.socket.size,
                },
                b = new SocketRef
                {
                    roomNetId = bNet != null ? bNet.NetworkObjectId : 0,
                    socketId = newSocket.socketId,
                    socketType = newSocket.socketType,
                    size = newSocket.size,
                }
            });
        }

        private bool TryExtendOnce(SpireLayoutData data, bool preferRandomSocket, bool forceLandmark, int minSockets = 1)
        {
            if (openSockets.Count == 0)
            {
                CacheOpenSockets();
                if (openSockets.Count == 0) return false;
            }

            PruneOpenSockets();
            if (openSockets.Count == 0) return false;

            // Build a list of target sockets to try (shuffled or ordered).
            var targetCandidates = new List<OpenSocket>(openSockets);
            if (preferRandomSocket)
                ShuffleList(targetCandidates);
            else
                targetCandidates.Reverse(); // newest first

            foreach (var target in targetCandidates)
            {
                if (target.socket == null || target.roomRoot == null) continue;

                if (TryPlaceAtSocket(data, target, forceLandmark, minSockets))
                    return true;
            }

            return false;
        }

        private bool TryPlaceAtSocket(SpireLayoutData data, OpenSocket target, bool forceLandmark, int minSockets)
        {
            // Build a shuffled list of candidate prefabs for this attempt.
            var candidatePrefabs = new List<RoomPrefab>();
            foreach (var p in roomPrefabs)
            {
                if (p == null) continue;
                if (CountCompatibleSockets(p) < minSockets) continue;
                candidatePrefabs.Add(p);
            }
            ShuffleList(candidatePrefabs);

            int attempts = 0;
            foreach (var prefabAsset in candidatePrefabs)
            {
                if (attempts >= maxAttemptsPerPlacement) break;

                // Apply the same filters as PickRoomPrefab.
                if (!PassesPlacementFilters(prefabAsset, forceLandmark, forceConnector: false)) continue;

                // Get all compatible sockets on this candidate, shuffled.
                var compatibleSockets = GetCompatibleSockets(prefabAsset);
                ShuffleList(compatibleSockets);

                foreach (var sourceSocketAsset in compatibleSockets)
                {
                    if (attempts >= maxAttemptsPerPlacement) break;
                    attempts++;

                    string sourceSocketPath = GetRelativePath(prefabAsset.transform, sourceSocketAsset.transform);

                    var aligned = ComputeSnapTransform(target, sourceSocketAsset, Rotation90(rng.Next(0, 4)));

                    var placedRoom = PlaceRoom(prefabAsset, aligned.pos, aligned.rot);
                    if (placedRoom == null || placedRoom.root == null) continue;

                    var sourceSocketInstance = FindSocketInstanceById(placedRoom.prefab, sourceSocketAsset.socketId);
                    if (sourceSocketInstance == null)
                        sourceSocketInstance = FindSocketInstance(placedRoom.root, sourceSocketPath);

                    if (sourceSocketInstance == null)
                        Debug.LogWarning($"[SpireGen] Could not resolve socket on '{placedRoom.root.name}'.");

                    MarkSocketUsed(target.roomRoot, target.socket);
                    if (sourceSocketInstance != null)
                        MarkSocketUsed(placedRoom.root, sourceSocketInstance);

                    openSockets.RemoveAll(s => s.socket == target.socket && s.roomRoot == target.roomRoot);

                    data.rooms.Add(new RoomPlacement
                    {
                        roomId = prefabAsset.roomId,
                        position = placedRoom.root.position,
                        rotationY = (int)placedRoom.root.rotation.eulerAngles.y
                    });

                    if (sourceSocketInstance != null)
                    {
                        var aNet = target.roomRoot.GetComponentInParent<NetworkObject>();
                        var bNet = placedRoom.root.GetComponentInParent<NetworkObject>();

                        var conn = new SocketConnection
                        {
                            a = new SocketRef
                            {
                                roomNetId = aNet != null ? aNet.NetworkObjectId : 0,
                                socketId = target.socket.socketId,
                                socketType = target.socket.socketType,
                                size = target.socket.size,
                            },
                            b = new SocketRef
                            {
                                roomNetId = bNet != null ? bNet.NetworkObjectId : 0,
                                socketId = sourceSocketInstance.socketId,
                                socketType = sourceSocketInstance.socketType,
                                size = sourceSocketInstance.size,
                            }
                        };

                        data.connections.Add(conn);
                        Debug.Log($"[SpireGen] CONNECT a(room={conn.a.roomNetId}, socket={conn.a.socketId[..Mathf.Min(6, conn.a.socketId.Length)]}) -> b(room={conn.b.roomNetId}, socket={conn.b.socketId[..Mathf.Min(6, conn.b.socketId.Length)]})");
                    }

                    NotePlaced(prefabAsset.roomId, prefabAsset);
                    AddOpenSocketsForRoom(placedRoom.root, placedRoom.prefab);
                    return true;
                }
            }

            // If landmark was required but nothing worked, retry without it.
            if (forceLandmark)
                return TryPlaceAtSocket(data, target, forceLandmark: false, minSockets);

            return false;
        }

        private bool PassesPlacementFilters(RoomPrefab p, bool forceLandmark, bool forceConnector)
        {
            if (!HasCompatibleSocket(p)) return false;
            if (forceLandmark && !p.landmark) return false;
            if (forceConnector && !p.connector) return false;
            if ((p.uniquePerSlice || p.landmark) && usedUniqueRoomIds.Contains(p.roomId)) return false;
            if (p.repeatCooldown > 0 && recentRoomIds.Contains(p.roomId)) return false;
            if (p.weight <= 0) return false;
            // Never place the boss room through normal selection.
            if (bossRoom != null && p.roomId == bossRoom.roomId) return false;
            return true;
        }

        /// <summary>
        /// Tries to place the boss room at the end of the main path.
        /// Walks backwards from the tip if the tip's sockets are all blocked.
        /// </summary>
        private bool TryPlaceBossRoom(SpireLayoutData data, List<PlacedRoom> mainPath)
        {
            if (bossRoom == null) return false;

            var compatibleSockets = GetCompatibleSockets(bossRoom);
            if (compatibleSockets.Count == 0)
            {
                Debug.LogError("[SpireGen] Boss room has no compatible sockets.");
                return false;
            }

            // Try from tip backwards along main path.
            for (int m = mainPath.Count - 1; m >= 0; m--)
            {
                var room = mainPath[m];
                if (room.root == null) continue;

                var roomSockets = new List<RoomSocket>();
                foreach (var sock in room.prefab.GetSockets(socketType, socketSize))
                {
                    if (sock == null) continue;
                    if (IsSocketUsed(room.root, sock)) continue;
                    roomSockets.Add(sock);
                }
                ShuffleList(roomSockets);

                foreach (var targetSocket in roomSockets)
                {
                    var targetOpen = new OpenSocket { socket = targetSocket, roomRoot = room.root };
                    ShuffleList(compatibleSockets);

                    foreach (var sourceSocketAsset in compatibleSockets)
                    {
                        string sourceSocketPath = GetRelativePath(bossRoom.transform, sourceSocketAsset.transform);
                        var aligned = ComputeSnapTransform(targetOpen, sourceSocketAsset, Rotation90(rng.Next(0, 4)));

                        var placedRoom = PlaceRoom(bossRoom, aligned.pos, aligned.rot);
                        if (placedRoom == null || placedRoom.root == null) continue;

                        var sourceSocketInstance = FindSocketInstanceById(placedRoom.prefab, sourceSocketAsset.socketId)
                            ?? FindSocketInstance(placedRoom.root, sourceSocketPath);

                        MarkSocketUsed(room.root, targetSocket);
                        if (sourceSocketInstance != null)
                            MarkSocketUsed(placedRoom.root, sourceSocketInstance);

                        data.bossRoomIndex = data.rooms.Count;
                        data.rooms.Add(new RoomPlacement
                        {
                            roomId = bossRoom.roomId,
                            position = placedRoom.root.position,
                            rotationY = (int)placedRoom.root.rotation.eulerAngles.y
                        });

                        if (sourceSocketInstance != null)
                            RecordConnection(data, targetOpen, placedRoom.root, sourceSocketInstance);

                        NotePlaced(bossRoom.roomId, bossRoom);
                        return true;
                    }
                }
            }

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

        private List<RoomSocket> GetCompatibleSockets(RoomPrefab prefabAsset)
        {
            var list = new List<RoomSocket>();
            foreach (var s in prefabAsset.GetSockets(socketType, socketSize))
                list.Add(s);
            return list;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private int CountCompatibleSockets(RoomPrefab prefab)
        {
            int count = 0;
            foreach (var _ in prefab.GetSockets(socketType, socketSize))
                count++;
            return count;
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
            _placementAttempts++;

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

                // Boss room is placed separately — never through normal selection.
                if (bossRoom != null && p.roomId == bossRoom.roomId) continue;

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

        private static RoomSocket FindSocketInstanceById(RoomPrefab roomInstance, string socketId)
        {
            if (roomInstance == null) return null;
            if (string.IsNullOrWhiteSpace(socketId)) return null;

            roomInstance.RefreshSockets();
            foreach (var s in roomInstance.sockets)
            {
                if (s != null && s.socketId == socketId) return s;
            }

            return null;
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
