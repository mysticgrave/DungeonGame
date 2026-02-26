using System.Collections.Generic;
using System.Linq;
using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// When players enter the dungeon (Spire scene), repositions all connected players to spawn points
    /// once the dungeon layout is fully generated. Players are frozen (CharacterController disabled)
    /// until the layout finishes so they don't fall through the void during generation.
    /// </summary>
    public class DungeonPlayerSpawner : NetworkBehaviour
    {
        [Tooltip("Delay before fallback reposition (when no layout event fires). Use ~2s if layout runs asynchronously.")]
        [SerializeField] private float fallbackDelay = 2f;

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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedDuringGen;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedDuringGen;
            }
            base.OnNetworkDespawn();
        }

        private void OnClientConnectedDuringGen(ulong clientId)
        {
            if (!SpireLayoutGenerator.IsGenerating) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var player = client.PlayerObject;
            if (player == null) return;

            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Debug.Log($"[DungeonSpawn] Client {clientId} connected during generation — frozen until layout finishes.");
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
            if (SpireLayoutGenerator.IsGenerating) return;
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

                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                var t = spawns[index % spawns.Count];
                player.transform.SetPositionAndRotation(t.position, t.rotation);
                SnapPlayerToGround(player.transform);

                if (cc != null) cc.enabled = true;
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

        private static void SnapPlayerToGround(Transform playerRoot)
        {
            var cc = playerRoot.GetComponent<CharacterController>();
            GroundSnap.SnapTransform(playerRoot, cc);
        }
    }
}
