using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// Disables renderers and colliders on rooms that are far from any player.
    /// Drastically reduces draw calls and physics cost for large procedural dungeons.
    /// Attach to the same GameObject as SpireLayoutGenerator (or any persistent object in the dungeon scene).
    /// </summary>
    public class RoomCullingManager : MonoBehaviour
    {
        [Header("Culling")]
        [Tooltip("Rooms beyond this distance from the nearest player will be culled.")]
        [SerializeField] private float cullDistance = 50f;

        [Tooltip("Hysteresis buffer — a room must come within (cullDistance - buffer) to reactivate. Prevents flicker.")]
        [SerializeField] private float hysteresisBuffer = 8f;

        [Tooltip("How often to re-evaluate culling (seconds). Lower = more responsive but more CPU.")]
        [SerializeField] private float updateInterval = 0.5f;

        [Header("Camera")]
        [Tooltip("Override the main camera far clip plane to limit draw distance. 0 = don't change.")]
        [SerializeField] private float cameraFarClip = 120f;

        private float _nextUpdateTime;
        private readonly List<TrackedRoom> _rooms = new();
        private readonly List<Transform> _playerTransforms = new();

        private struct TrackedRoom
        {
            public GameObject Root;
            public Bounds Bounds;
            public bool IsCulled;
            public Renderer[] Renderers;
            public Collider[] Colliders;
            public LODGroup[] LODs;
            public ParticleSystem[] Particles;
            public Light[] Lights;
        }

        private void Start()
        {
            if (cameraFarClip > 0f)
            {
                var cam = Camera.main;
                if (cam != null)
                    cam.farClipPlane = cameraFarClip;
            }
        }

        /// <summary>
        /// Register a spawned room for culling. Call after a room is instantiated.
        /// </summary>
        public void RegisterRoom(GameObject roomRoot)
        {
            if (roomRoot == null) return;

            // Mark all static geometry for batching (huge draw call reduction)
            MarkStaticRecursive(roomRoot);

            var renderers = roomRoot.GetComponentsInChildren<Renderer>(true);
            var colliders = roomRoot.GetComponentsInChildren<Collider>(true);
            var lods = roomRoot.GetComponentsInChildren<LODGroup>(true);

            // Gather heavy components we can disable
            var particles = roomRoot.GetComponentsInChildren<ParticleSystem>(true);
            var lights = roomRoot.GetComponentsInChildren<Light>(true);
            var managedLights = new List<Light>();
            foreach (var l in lights)
            {
                // Only manage non-directional lights (point, spot)
                if (l != null && l.type != LightType.Directional)
                    managedLights.Add(l);
            }

            // Compute world bounds from all renderers
            Bounds bounds = new Bounds(roomRoot.transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r.bounds.size.sqrMagnitude > 0.001f)
                    bounds.Encapsulate(r.bounds);
            }

            // Fallback: if no renderers, use a default size
            if (bounds.size.sqrMagnitude < 0.1f)
                bounds = new Bounds(roomRoot.transform.position, Vector3.one * 10f);

            _rooms.Add(new TrackedRoom
            {
                Root = roomRoot,
                Bounds = bounds,
                IsCulled = false,
                Renderers = renderers,
                Colliders = colliders,
                LODs = lods,
                Particles = particles,
                Lights = managedLights.ToArray()
            });
        }

        /// <summary>Unregister all rooms (call before regenerating dungeon).</summary>
        public void ClearRooms()
        {
            _rooms.Clear();
        }

        private void Update()
        {
            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + updateInterval;

            // Adjust camera far clip if it was reset
            if (cameraFarClip > 0f)
            {
                var cam = Camera.main;
                if (cam != null && Mathf.Abs(cam.farClipPlane - cameraFarClip) > 1f)
                    cam.farClipPlane = cameraFarClip;
            }

            RefreshPlayerList();
            if (_playerTransforms.Count == 0) return;

            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                var room = _rooms[i];
                if (room.Root == null)
                {
                    _rooms.RemoveAt(i);
                    continue;
                }

                float nearestDist = NearestPlayerDistance(room.Bounds);

                if (room.IsCulled)
                {
                    // Reactivate if within (cullDistance - buffer)
                    if (nearestDist < cullDistance - hysteresisBuffer)
                    {
                        SetRoomCulled(ref room, false);
                        _rooms[i] = room;
                    }
                }
                else
                {
                    // Cull if beyond cullDistance
                    if (nearestDist > cullDistance)
                    {
                        SetRoomCulled(ref room, true);
                        _rooms[i] = room;
                    }
                }
            }
        }

        private void RefreshPlayerList()
        {
            _playerTransforms.Clear();

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                // Fallback to camera
                var cam = Camera.main;
                if (cam != null)
                    _playerTransforms.Add(cam.transform);
                return;
            }

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                if (kvp.Value.PlayerObject != null)
                    _playerTransforms.Add(kvp.Value.PlayerObject.transform);
            }

            // Client: might only have local player
            if (_playerTransforms.Count == 0)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayer != null)
                    _playerTransforms.Add(localPlayer.transform);
            }
        }

        private float NearestPlayerDistance(Bounds bounds)
        {
            float best = float.MaxValue;
            foreach (var pt in _playerTransforms)
            {
                if (pt == null) continue;
                // Distance from player to nearest point on room bounds
                float d = Vector3.Distance(pt.position, bounds.ClosestPoint(pt.position));
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Mark static children for batching. Skips objects with Rigidbody, Animator,
        /// NetworkObject, or CharacterController (they move at runtime).
        /// </summary>
        private static void MarkStaticRecursive(GameObject go)
        {
            // Don't mark objects that move at runtime
            bool canBeStatic = go.GetComponent<Rigidbody>() == null
                            && go.GetComponent<Animator>() == null
                            && go.GetComponent<CharacterController>() == null
                            && go.GetComponent<NetworkObject>() == null;

            if (canBeStatic)
                go.isStatic = true;

            // Always recurse into children (a NetworkObject root has static mesh children)
            for (int i = 0; i < go.transform.childCount; i++)
                MarkStaticRecursive(go.transform.GetChild(i).gameObject);
        }

        private static void SetRoomCulled(ref TrackedRoom room, bool culled)
        {
            room.IsCulled = culled;

            // Toggle renderers
            foreach (var r in room.Renderers)
            {
                if (r != null)
                    r.enabled = !culled;
            }

            // Toggle non-trigger colliders (keep triggers for zone detection)
            foreach (var c in room.Colliders)
            {
                if (c != null && !c.isTrigger)
                    c.enabled = !culled;
            }

            // Toggle particle systems
            foreach (var ps in room.Particles)
            {
                if (ps == null) continue;
                if (culled)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else
                {
                    ps.Play(true);
                }
            }

            // Toggle non-directional lights
            foreach (var l in room.Lights)
            {
                if (l != null)
                    l.enabled = !culled;
            }
        }
    }
}
