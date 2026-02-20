using System;
using System.Collections.Generic;
using DungeonGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Server-side socket-snap generator for Spire_Slice.
    /// MVP: generates a main path + branches by snapping room prefabs via RoomSockets (90-degree rotations only).
    /// 
    /// This generator is designed so that later you can switch from "spawn as NetworkObjects" to "send recipe" by
    /// using the returned SpireLayoutData.
    /// </summary>
    [RequireComponent(typeof(SpireSeed))]
    public class SpireLayoutGenerator : NetworkBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Room prefabs to use. These should have RoomPrefab + NetworkObject on the root.")]
        [SerializeField] private List<RoomPrefab> roomPrefabs = new();

        [Header("Shape")]
        [SerializeField, Min(1)] private int mainPathRooms = 20;
        [SerializeField, Range(0, 10)] private int branches = 4;
        [SerializeField, Min(1)] private int branchLengthMin = 2;
        [SerializeField, Min(1)] private int branchLengthMax = 6;

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

        private System.Random rng;

        private struct PlacedRoom
        {
            public RoomPrefab prefab;
            public Transform root;
            public Bounds worldBounds;
            public HashSet<RoomSocket> usedSockets;
        }

        private struct OpenSocket
        {
            public RoomSocket socket;
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
            var start = startRoom != null ? startRoom : PickRoomPrefab(null);
            var startRot = Rotation90(rng.Next(0, 4));
            var startPlacement = PlaceRoom(start, Vector3.zero, startRot);
            if (startPlacement.root == null)
            {
                Debug.LogError("[SpireGen] Failed to place start room.");
                return data;
            }

            data.rooms.Add(new RoomPlacement { roomId = start.roomId, position = startPlacement.root.position, rotationY = (int)startRot.eulerAngles.y });

            // Main path: always extend from the newest room's available sockets.
            for (int i = 0; i < mainPathRooms - 1; i++)
            {
                if (!TryExtendOnce(data))
                {
                    Debug.LogWarning($"[SpireGen] Main path extension failed at i={i}." );
                    break;
                }
            }

            // Branches: extend from random open sockets.
            for (int b = 0; b < branches; b++)
            {
                int len = rng.Next(branchLengthMin, branchLengthMax + 1);
                for (int i = 0; i < len; i++)
                {
                    if (!TryExtendOnce(data, preferRandomSocket: true)) break;
                }
            }

            return data;
        }

        private bool TryExtendOnce(SpireLayoutData data, bool preferRandomSocket = false)
        {
            if (openSockets.Count == 0)
            {
                CacheOpenSockets();
                if (openSockets.Count == 0) return false;
            }

            // Choose a target open socket.
            OpenSocket target;
            if (preferRandomSocket)
            {
                target = openSockets[rng.Next(0, openSockets.Count)];
            }
            else
            {
                // Prefer latest-added socket (more path-like)
                target = openSockets[openSockets.Count - 1];
            }

            // Try place a room connected to it.
            for (int attempt = 0; attempt < maxAttemptsPerPlacement; attempt++)
            {
                var prefab = PickRoomPrefab(target.socket);
                if (prefab == null) continue;

                if (!TryGetCompatibleSocket(prefab, out var sourceSocket)) continue;

                // Determine rotation and position so that sourceSocket faces opposite of target.socket.
                // We allow 90-degree rotations only.
                int rotSteps = rng.Next(0, 4);
                var rot = Rotation90(rotSteps);

                // Compute transform that would align sockets.
                // Start by rotating the prefab socket.
                var aligned = ComputeSnapTransform(target, prefab, sourceSocket, rot);

                var placedRoom = PlaceRoom(prefab, aligned.pos, aligned.rot);
                if (placedRoom.root == null) continue;

                // Mark sockets used and update open sockets cache.
                MarkSocketUsed(target.roomRoot, target.socket);
                MarkSocketUsed(placedRoom.root, sourceSocket);

                // Remove used target socket from open list.
                openSockets.RemoveAll(s => s.socket == target.socket && s.roomRoot == target.roomRoot);

                data.rooms.Add(new RoomPlacement
                {
                    roomId = prefab.roomId,
                    position = placedRoom.root.position,
                    rotationY = (int)placedRoom.root.rotation.eulerAngles.y
                });

                // Add new room sockets to open list
                AddOpenSocketsForRoom(placedRoom.root, prefab);

                return true;
            }

            // If we failed to extend from this socket, drop it and try later.
            openSockets.RemoveAll(s => s.socket == target.socket && s.roomRoot == target.roomRoot);
            return false;
        }

        private void CacheOpenSockets()
        {
            openSockets.Clear();
            foreach (var pr in placed)
            {
                AddOpenSocketsForRoom(pr.root, pr.prefab);
            }
        }

        private void AddOpenSocketsForRoom(Transform roomRoot, RoomPrefab prefab)
        {
            foreach (var sock in prefab.GetSockets(socketType, socketSize))
            {
                if (sock == null) continue;
                if (IsSocketUsed(roomRoot, sock)) continue;
                openSockets.Add(new OpenSocket { socket = sock, roomRoot = roomRoot });
            }

            // Prefer non-lowPriority sockets by sorting once in a while.
            openSockets.Sort((a, b) => (a.socket.lowPriority ? 1 : 0).CompareTo(b.socket.lowPriority ? 1 : 0));
        }

        private bool IsSocketUsed(Transform roomRoot, RoomSocket socket)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].root != roomRoot) continue;
                return placed[i].usedSockets != null && placed[i].usedSockets.Contains(socket);
            }
            return false;
        }

        private void MarkSocketUsed(Transform roomRoot, RoomSocket socket)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].root != roomRoot) continue;
                if (placed[i].usedSockets == null) placed[i].usedSockets = new HashSet<RoomSocket>();
                placed[i].usedSockets.Add(socket);
                return;
            }
        }

        private bool TryGetCompatibleSocket(RoomPrefab prefab, out RoomSocket socket)
        {
            foreach (var s in prefab.GetSockets(socketType, socketSize))
            {
                socket = s;
                return true;
            }
            socket = null;
            return false;
        }

        private (Vector3 pos, Quaternion rot) ComputeSnapTransform(OpenSocket target, RoomPrefab prefab, RoomSocket sourceSocket, Quaternion roomRot)
        {
            // We want: sourceSocket forward points opposite target socket forward.
            // We'll compute where the room root must be so that source socket position matches target socket position.
            // Because sockets are children, we use their local position/rotation.

            // Compute socket world pose if the room root is at origin with roomRot.
            var sourceLocalPos = sourceSocket.transform.localPosition;
            var sourceLocalRot = sourceSocket.transform.localRotation;

            var sourceWorldRot = roomRot * sourceLocalRot;

            // Align forward vectors: rotate around Y in 90-deg steps already embedded in roomRot.
            // We'll additionally rotate roomRot so the socket forward matches.
            // Since we restrict to 90 degree rotations, we can brute force the best match.

            // We'll brute-force 4 yaw rotations to match.
            Quaternion best = roomRot;
            float bestDot = -999f;
            for (int step = 0; step < 4; step++)
            {
                var r = Rotation90(step) * roomRot;
                var f = (r * sourceLocalRot) * Vector3.forward;
                f.y = 0; f.Normalize();

                var tf = target.socket.transform.forward;
                tf.y = 0; tf.Normalize();

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

        private PlacedRoom PlaceRoom(RoomPrefab prefab, Vector3 pos, Quaternion rot)
        {
            // Instantiate under generator for cleanup.
            var go = Instantiate(prefab.gameObject, pos, rot, transform);

            // Overlap check (simple AABB in world space using renderers/colliders).
            var bounds = ComputeWorldBounds(go);
            bounds.Expand(overlapPadding);
            for (int i = 0; i < placed.Count; i++)
            {
                if (bounds.Intersects(placed[i].worldBounds))
                {
                    Destroy(go);
                    return default;
                }
            }

            // Spawn network object
            var no = go.GetComponent<NetworkObject>();
            if (no != null && !no.IsSpawned)
            {
                no.Spawn(true);
            }

            var rp = go.GetComponent<RoomPrefab>();

            var pr = new PlacedRoom
            {
                prefab = rp,
                root = go.transform,
                worldBounds = bounds,
                usedSockets = new HashSet<RoomSocket>()
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

        private RoomPrefab PickRoomPrefab(RoomSocket connectingTo)
        {
            // MVP: uniform random; later add weights/tags/rarity + repeat cooldown.
            // Filter by having at least one compatible socket.
            var candidates = new List<RoomPrefab>();
            foreach (var p in roomPrefabs)
            {
                if (p == null) continue;
                if (HasCompatibleSocket(p)) candidates.Add(p);
            }

            if (candidates.Count == 0) return null;
            return candidates[rng.Next(0, candidates.Count)];
        }

        private bool HasCompatibleSocket(RoomPrefab prefab)
        {
            foreach (var _ in prefab.GetSockets(socketType, socketSize))
            {
                return true;
            }
            return false;
        }

        private static Quaternion Rotation90(int steps)
        {
            int s = ((steps % 4) + 4) % 4;
            return Quaternion.Euler(0f, 90f * s, 0f);
        }

        private void SpawnLayout(SpireLayoutData layout)
        {
            // Currently, GenerateLayout already instantiates/spawns.
            // This method exists for the future "recipe" backend.
            Debug.Log($"[SpireGen] Generated layout: rooms={layout.rooms.Count}, seed={layout.seed}");
        }
    }
}
