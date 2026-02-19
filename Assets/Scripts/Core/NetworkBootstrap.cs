using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private NetworkManager nm;

        private void Awake()
        {
            // NetworkManager.Singleton may not be initialized yet depending on script execution order,
            // so we also search the scene.
            nm = NetworkManager.Singleton;
            if (nm == null)
            {
                nm = FindFirstObjectByType<NetworkManager>();
            }

            if (nm == null)
            {
                EnsureNetworkManager();
                return;
            }

            DontDestroyOnLoad(nm.gameObject);
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
            // Quick iteration hotkeys (Input System)
            if (Keyboard.current == null) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame) StartHost();
            if (Keyboard.current.f2Key.wasPressedThisFrame) StartClient();
            if (Keyboard.current.f3Key.wasPressedThisFrame) Shutdown();
        }

        private static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            Debug.LogError("[Net] No NetworkManager found in scene. " +
                           "Create a GameObject named 'NetworkManager' in the Town scene with: " +
                           "NetworkManager + UnityTransport, then assign the Player Prefab in the inspector.");
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
