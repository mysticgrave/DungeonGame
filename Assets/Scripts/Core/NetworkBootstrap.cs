using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Core
{
    /// <summary>
    /// Minimal Netcode bootstrap.
    /// - Ensures a NetworkManager + UnityTransport exist (creates one if missing).
    /// - Lets you start Host/Client from keyboard for quick iteration.
    /// 
    /// Place in any scene. If a DontDestroyOnLoad NetworkManager already exists
    /// (e.g. came from MainMenu), it reuses that and skips creation.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrap : MonoBehaviour
    {
        [Header("Quick Start")]
        [Tooltip("If true, automatically starts as Host when entering Play Mode.")]
        [SerializeField] private bool autoStartHost = true;

        [Header("Player Prefab (for auto-created NetworkManager)")]
        [Tooltip("Assign the Player prefab here. Only used when no NetworkManager exists yet.")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Connection")]
        [Tooltip("Address for client to connect to (UnityTransport).")]
        [SerializeField] private string address = "127.0.0.1";

        [Tooltip("Port for host/server to listen on and clients to connect to.")]
        [SerializeField] private ushort port = 7777;

        private NetworkManager nm;

        private void Awake()
        {
            nm = NetworkManager.Singleton;
            if (nm == null)
                nm = FindFirstObjectByType<NetworkManager>();

            if (nm == null)
                nm = CreateNetworkManager();

            if (nm != null)
                DontDestroyOnLoad(nm.gameObject);
        }

        private void Start()
        {
            if (autoStartHost)
                StartHost();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame) StartHost();
            if (Keyboard.current.f2Key.wasPressedThisFrame) StartClient();
            if (Keyboard.current.f3Key.wasPressedThisFrame) Shutdown();
        }

        private NetworkManager CreateNetworkManager()
        {
            var go = new GameObject("NetworkManager (AutoCreated)");
            var manager = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();

            manager.NetworkConfig.NetworkTransport = utp;

            if (playerPrefab != null)
            {
                manager.NetworkConfig.PlayerPrefab = playerPrefab;
                Debug.Log($"[Net] Auto-created NetworkManager with player prefab '{playerPrefab.name}'");
            }
            else
            {
                Debug.LogWarning("[Net] Auto-created NetworkManager but no playerPrefab assigned on NetworkBootstrap. " +
                                 "Assign it in the inspector for player spawning to work.");
            }

            return manager;
        }

        private void ApplyTransport()
        {
            if (nm == null) return;

            var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null) return;

            utp.SetConnectionData(address, port);
        }

        public void StartHost()
        {
            if (nm == null) return;
            if (nm.IsListening) return;

            ApplyTransport();
            nm.StartHost();
            Debug.Log("[Net] Started Host");
        }

        public void StartClient()
        {
            if (nm == null) return;
            if (nm.IsListening) return;

            ApplyTransport();
            nm.StartClient();
            Debug.Log("[Net] Started Client");
        }

        public void Shutdown()
        {
            if (nm == null) return;
            if (!nm.IsListening) return;

            nm.Shutdown();
            Debug.Log("[Net] Shutdown");
        }
    }
}
