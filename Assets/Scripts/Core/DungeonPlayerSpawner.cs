using System.Collections.Generic;
using System.Linq;
using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// When players enter the dungeon (Spire scene), repositions all connected players to spawn points in the scene.
    /// Use this in the dungeon scene (e.g. Spire_Slice). Spawn points can be in the scene or inside room prefabs (e.g. start room).
    /// 
    /// - If the dungeon is procedural: subscribes to SpireLayoutGenerator.OnLayoutGenerated and repositions after rooms (and their spawn points) exist.
    /// - Fallback: repositions once after a short delay in Start (for fixed scenes or if layout already ran).
    /// </summary>
    public class DungeonPlayerSpawner : MonoBehaviour
    {
        [Tooltip("Delay before fallback reposition (when no layout event fires). Use ~1–2s if layout runs asynchronously.")]
        [SerializeField] private float fallbackDelay = 1.5f;

        private bool _repositioned;

        private void OnEnable()
        {
            SpireLayoutGenerator.OnLayoutGenerated += OnLayoutGenerated;
        }

        private void OnDisable()
        {
            SpireLayoutGenerator.OnLayoutGenerated -= OnLayoutGenerated;
            CancelInvoke(nameof(FallbackReposition));
        }

        private void Start()
        {
            Invoke(nameof(FallbackReposition), fallbackDelay);
        }

        private void OnLayoutGenerated(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;
            RepositionAllPlayers();
        }

        private void FallbackReposition()
        {
            if (_repositioned) return;
            RepositionAllPlayers();
        }

        private void RepositionAllPlayers()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            var spawns = GetSpawnPointsInScene();
            if (spawns.Count == 0)
            {
                Debug.Log("[DungeonSpawn] No PlayerSpawnPoint in scene; skipping reposition.");
                return;
            }

            int index = 0;
            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                var t = spawns[index % spawns.Count];
                player.transform.SetPositionAndRotation(t.position, t.rotation);
                SnapPlayerToGround(player.transform);
                index++;
            }

            _repositioned = true;
            Debug.Log($"[DungeonSpawn] Repositioned {index} player(s) to dungeon spawn points.");
        }

        private List<Transform> GetSpawnPointsInScene()
        {
            var list = new List<Transform>();
            var points = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (points != null && points.Length > 0)
            {
                foreach (var p in points.OrderBy(x => x.SpawnIndex))
                    list.Add(p.transform);
            }
            return list;
        }

        /// <summary>
        /// Raycast down from the player and place their feet on the ground so they don't fall through.
        /// </summary>
        private static void SnapPlayerToGround(Transform playerRoot)
        {
            var cc = playerRoot.GetComponent<CharacterController>();
            if (cc == null) return;

            float bottomY = playerRoot.position.y + cc.center.y - cc.height * 0.5f;
            float rayStartY = playerRoot.position.y + Mathf.Max(1f, cc.height * 0.5f);
            Vector3 origin = new Vector3(playerRoot.position.x, rayStartY, playerRoot.position.z);
            float maxDist = rayStartY - bottomY + 3f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, ~0, QueryTriggerInteraction.Ignore))
            {
                float groundY = hit.point.y;
                float lift = groundY - bottomY;
                playerRoot.position += Vector3.up * lift;
            }
        }
    }
}
