using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonGame.Items;
using DungeonGame.Player;
using DungeonGame.SpireGen;
using DungeonGame.UI;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// When players enter the dungeon (Spire scene), repositions all connected players to spawn points
    /// once the dungeon layout is fully generated. Players are frozen (CharacterController disabled)
    /// until the layout finishes so they don't fall through the void during generation.
    ///
    /// Handles late-joining clients: if a client connects after generation is done, waits for their
    /// scene synchronization to complete, then repositions them.
    /// </summary>
    public class DungeonPlayerSpawner : NetworkBehaviour
    {
        [Tooltip("Delay before fallback reposition (when no layout event fires). Use ~2s if layout runs asynchronously.")]
        [SerializeField] private float fallbackDelay = 4f;

        [Tooltip("Max time to wait for spawn points to appear (seconds). Covers room NetworkObject replication delay.")]
        [SerializeField] private float spawnPointWaitTimeout = 10f;

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
            // Handle BOTH cases: client arrives during generation, or after it's done.
            // Either way they need freezing + repositioning once rooms are replicated.
            StartCoroutine(HandleLateClient(clientId));
        }

        /// <summary>
        /// Freeze the client immediately, wait for generation + room replication, then reposition.
        /// </summary>
        private IEnumerator HandleLateClient(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            float timeout = 10f;
            float elapsed = 0f;
            NetworkClient client = null;

            // Wait for PlayerObject
            while (elapsed < timeout)
            {
                if (nm == null || !nm.IsServer) yield break;
                if (nm.ConnectedClients.TryGetValue(clientId, out client) && client.PlayerObject != null)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (client?.PlayerObject == null)
            {
                Debug.LogWarning($"[DungeonSpawn] Timed out waiting for PlayerObject for client {clientId}.");
                yield break;
            }

            // Freeze immediately
            var cc = client.PlayerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Debug.Log($"[DungeonSpawn] Client {clientId} connected — frozen while waiting for dungeon.");

            // Wait for generation to finish (if still running)
            while (SpireLayoutGenerator.IsGenerating)
            {
                if (nm == null || !nm.IsServer) yield break;
                yield return null;
            }

            // If the main reposition already ran and handled this client, we're done
            if (_repositioned)
            {
                // Still need to wait for rooms to replicate on the client before unfreezing.
                // The server has all rooms, but the client may not have received them yet.
                // Wait a short time for NetworkObject replication, then reposition this specific client.
                yield return new WaitForSeconds(1.5f);
            }

            // Now reposition this specific client
            yield return StartCoroutine(RepositionSingleClient(clientId));
        }

        /// <summary>
        /// Reposition a single late-joining client. Waits for spawn points to exist (room replication).
        /// </summary>
        private IEnumerator RepositionSingleClient(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) yield break;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) yield break;
            if (client.PlayerObject == null) yield break;

            // Wait for spawn points to appear (rooms may still be replicating to clients)
            float waited = 0f;
            List<Transform> spawns = null;
            while (waited < spawnPointWaitTimeout)
            {
                spawns = GetSpawnPointsInScene();
                if (spawns.Count > 0) break;
                waited += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (spawns == null || spawns.Count == 0)
            {
                Debug.LogWarning($"[DungeonSpawn] No spawn points found for late client {clientId} after {spawnPointWaitTimeout}s. Unfreezing at current position.");
                var fallbackCc = client.PlayerObject.GetComponent<CharacterController>();
                if (fallbackCc != null) fallbackCc.enabled = true;
                HideLoadingScreenClientRpc();
                yield break;
            }

            // Pick a spawn point (use client count as offset to avoid stacking)
            int index = (int)(clientId % (ulong)spawns.Count);
            var spawnT = spawns[index];

            var motor = client.PlayerObject.GetComponent<ThirdPersonMotor>();
            if (motor != null)
            {
                motor.ServerTeleport(spawnT.position, spawnT.rotation);
            }
            else
            {
                var charController = client.PlayerObject.GetComponent<CharacterController>();
                if (charController != null) charController.enabled = false;
                client.PlayerObject.transform.SetPositionAndRotation(spawnT.position, spawnT.rotation);
                SnapPlayerToGround(client.PlayerObject.transform);
                if (charController != null) charController.enabled = true;
            }

            // Re-enable CharacterController
            var ccFinal = client.PlayerObject.GetComponent<CharacterController>();
            if (ccFinal != null) ccFinal.enabled = true;

            // Clear inventory and re-equip
            var handSystem = client.PlayerObject.GetComponent<HandSystem>();
            if (handSystem != null)
            {
                handSystem.ServerClearHands();
                handSystem.ReequipStartingWeaponClientRpc();
            }

            Debug.Log($"[DungeonSpawn] Late client {clientId} repositioned to spawn point.");
            HideLoadingScreenClientRpc();
        }

        private void OnLayoutGenerated(SpireLayoutGenerator gen, SpireLayoutData layout)
        {
            if (gen == null) return;
            if (gen.gameObject.scene != gameObject.scene) return;
            StartCoroutine(RepositionAllPlayersDeferred());
        }

        private void FallbackReposition()
        {
            if (_repositioned) return;
            if (SpireLayoutGenerator.IsGenerating) return;
            StartCoroutine(RepositionAllPlayersDeferred());
        }

        /// <summary>
        /// Wait for spawn points to appear (room NetworkObjects may take a frame or two to register),
        /// then reposition all players.
        /// </summary>
        private IEnumerator RepositionAllPlayersDeferred()
        {
            if (_repositioned) yield break;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) yield break;

            // Wait for spawn points — rooms are NetworkObjects that may need a few frames to register
            float waited = 0f;
            List<Transform> spawns = null;
            while (waited < spawnPointWaitTimeout)
            {
                spawns = GetSpawnPointsInScene();
                if (spawns.Count > 0) break;
                waited += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            if (spawns == null || spawns.Count == 0)
            {
                Debug.LogWarning("[DungeonSpawn] No PlayerSpawnPoint found after waiting. Players may be mis-positioned.");
                // Still mark repositioned so late clients get handled individually
                _repositioned = true;
                yield break;
            }

            int index = 0;
            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                var t = spawns[index % spawns.Count];

                // Use ThirdPersonMotor.ServerTeleport for owner-authoritative NetworkTransform sync
                var motor = player.GetComponent<ThirdPersonMotor>();
                if (motor != null)
                {
                    motor.ServerTeleport(t.position, t.rotation);
                }
                else
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.SetPositionAndRotation(t.position, t.rotation);
                    SnapPlayerToGround(player.transform);
                    if (cc != null) cc.enabled = true;
                }

                // Re-enable CharacterController (was frozen during generation)
                var ccEnable = player.GetComponent<CharacterController>();
                if (ccEnable != null) ccEnable.enabled = true;

                // Clear inventory and re-equip starting weapon from MetaProgression
                var handSystem = player.GetComponent<HandSystem>();
                if (handSystem != null)
                {
                    handSystem.ServerClearHands();
                    handSystem.ReequipStartingWeaponClientRpc();
                }

                index++;
            }

            _repositioned = true;
            Debug.Log($"[DungeonSpawn] Repositioned {index} player(s) to dungeon spawn points.");

            // Hide loading screen now that players are positioned and ready
            var loader = LoadingScreenManager.Instance;
            if (loader != null && loader.IsVisible)
                loader.FadeOutAndHide();
            HideLoadingScreenClientRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void HideLoadingScreenClientRpc()
        {
            var loader = LoadingScreenManager.Instance;
            if (loader != null && loader.IsVisible)
                loader.FadeOutAndHide();
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
