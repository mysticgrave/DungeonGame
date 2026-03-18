using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.Core
{
    /// <summary>
    /// Server-side spawn positioning for NGO player objects.
    /// Works in any scene (Town, Dungeon, etc.).
    ///
    /// Auto-bootstraps: when a scene loads that contains PlayerSpawnPoint components,
    /// a PlayerSpawnSystem is created automatically. No manual scene placement needed.
    /// </summary>
    public class PlayerSpawnSystem : MonoBehaviour
    {
        [Tooltip("Used only when no PlayerSpawnPoint components are found in the scene.")]
        [SerializeField] private string spawnTagName = "PlayerSpawn";

        private readonly List<Transform> cachedSpawns = new();
        private int nextIndex;

        private static PlayerSpawnSystem _instance;

        // ── Auto-bootstrap ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Also check the current scene immediately
            TryBootstrap();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_instance != null)
            {
                // Re-cache spawn points and reposition all existing players in the new scene
                _instance.cachedSpawns.Clear();
                _instance.nextIndex = 0;
                _instance.StartCoroutine(_instance.RepositionAllPlayersDeferred());
                return;
            }
            TryBootstrap();
        }

        private static void TryBootstrap()
        {
            // Don't duplicate
            if (_instance != null) return;

            // Check if there are spawn points in the loaded scene(s)
            var points = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (points == null || points.Length == 0)
            {
                // Also check for tagged objects
                try
                {
                    var tagged = GameObject.FindGameObjectsWithTag("PlayerSpawn");
                    if (tagged == null || tagged.Length == 0) return;
                }
                catch { return; } // Tag not defined
            }

            var go = new GameObject("[PlayerSpawnSystem]");
            _instance = go.AddComponent<PlayerSpawnSystem>();
            DontDestroyOnLoad(go);
            Debug.Log("[Spawn] Auto-created PlayerSpawnSystem (found spawn points in scene).");
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void CacheSpawnPoints()
        {
            cachedSpawns.Clear();

            var byComponent = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (byComponent != null && byComponent.Length > 0)
            {
                foreach (var t in byComponent.OrderBy(p => p.SpawnIndex).Select(p => p.transform))
                    cachedSpawns.Add(t);
            }

            if (cachedSpawns.Count == 0)
            {
                try
                {
                    foreach (var go in GameObject.FindGameObjectsWithTag(spawnTagName))
                        cachedSpawns.Add(go.transform);
                }
                catch { /* Tag not defined */ }
            }

            if (cachedSpawns.Count == 0)
                Debug.LogWarning($"[Spawn] No PlayerSpawnPoint components or tag '{spawnTagName}' found. Using (0,0,0).");
            else
                Debug.Log($"[Spawn] Cached {cachedSpawns.Count} spawn point(s).");
        }

        /// <summary>
        /// Called when a scene loads to reposition all already-connected players.
        /// This handles the case where the host starts in MainMenu, then loads Town —
        /// OnClientConnectedCallback already fired before Town existed, so the host
        /// player was never positioned at a Town spawn point.
        /// </summary>
        private IEnumerator RepositionAllPlayersDeferred()
        {
            // Wait a frame for the scene to fully initialize (spawn points, colliders, etc.)
            yield return null;
            yield return null;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) yield break;

            CacheSpawnPoints();
            if (cachedSpawns.Count == 0) yield break;

            nextIndex = 0;
            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                var t = cachedSpawns[nextIndex % cachedSpawns.Count];
                nextIndex++;

                var motor = player.GetComponent<ThirdPersonMotor>();
                if (motor != null)
                {
                    motor.ServerTeleport(t.position, t.rotation);
                    Debug.Log($"[Spawn] Scene-load reposition: teleported client {kvp.Key} to {t.position}");
                }
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            StartCoroutine(PositionClientWhenReady(clientId));
        }

        private IEnumerator PositionClientWhenReady(ulong clientId)
        {
            var nm = NetworkManager.Singleton;

            // Wait up to 5 seconds for PlayerObject to become available
            float timeout = 5f;
            float elapsed = 0f;
            NetworkClient client = null;

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
                Debug.LogWarning($"[Spawn] Timed out waiting for PlayerObject for client {clientId}.");
                yield break;
            }

            var player = client.PlayerObject;

            // Always re-cache when positioning (scene may have changed)
            CacheSpawnPoints();

            Vector3 pos;
            Quaternion rot;

            if (cachedSpawns.Count == 0)
            {
                pos = Vector3.zero;
                rot = Quaternion.identity;
            }
            else
            {
                var t = cachedSpawns[nextIndex % cachedSpawns.Count];
                nextIndex++;
                pos = t.position;
                rot = t.rotation;
            }

            // Use ThirdPersonMotor.ServerTeleport to handle owner-authoritative NetworkTransform.
            var motor = player.GetComponent<ThirdPersonMotor>();
            if (motor != null)
            {
                motor.ServerTeleport(pos, rot);
                Debug.Log($"[Spawn] Teleported client {clientId} to {pos}");
            }
            else
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(pos, rot);
                GroundSnap.SnapTransform(player.transform, cc);
                if (cc != null) cc.enabled = true;
                Debug.LogWarning($"[Spawn] No ThirdPersonMotor on client {clientId} — used fallback");
            }
        }
    }
}
