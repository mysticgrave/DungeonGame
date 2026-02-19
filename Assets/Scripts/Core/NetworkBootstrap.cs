using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Minimal Netcode bootstrap.
    /// - Ensures a NetworkManager + UnityTransport exist.
    /// - Lets you start Host/Client from keyboard for quick iteration.
    /// 
    /// MVP intent: get into Play Mode with 2 instances quickly.
    /// Later: replace with proper main menu + connection flow.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrap : MonoBehaviour
    {
        [Header("Quick Start")]
        [Tooltip("If true, automatically starts as Host when entering Play Mode.")]
        [SerializeField] private bool autoStartHost;

        [Tooltip("Address for client to connect to (UnityTransport).")]
        [SerializeField] private string address = "127.0.0.1";

        [Tooltip("Port for host/server to listen on and clients to connect to.")]
        [SerializeField] private ushort port = 7777;

        private void Awake()
        {
            EnsureNetworkManager();
        }

        private void Start()
        {
            if (autoStartHost)
            {
                StartHost();
            }
        }

        private void Update()
        {
            // Quick iteration hotkeys
            if (Input.GetKeyDown(KeyCode.F1)) StartHost();
            if (Input.GetKeyDown(KeyCode.F2)) StartClient();
            if (Input.GetKeyDown(KeyCode.F3)) Shutdown();
        }

        private static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go);

            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                EnableSceneManagement = true,
                // You can add player prefab later in the inspector.
            };
            nm.NetworkConfig.NetworkTransport = transport;

            // Basic tickrate defaults are fine for now.
        }

        private void ApplyTransport()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null) return;

            utp.SetConnectionData(address, port);
        }

        public void StartHost()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsListening) return;

            ApplyTransport();
            NetworkManager.Singleton.StartHost();
            Debug.Log("[Net] Started Host");
        }

        public void StartClient()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsListening) return;

            ApplyTransport();
            NetworkManager.Singleton.StartClient();
            Debug.Log("[Net] Started Client");
        }

        public void Shutdown()
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.IsListening) return;

            NetworkManager.Singleton.Shutdown();
            Debug.Log("[Net] Shutdown");
        }
    }
}
