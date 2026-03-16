using System;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Minimal Netcode bootstrap.
    /// - Ensures a NetworkManager + UnityTransport exist (creates one if missing).
    /// - Auto-starts Host when autoStartHost is true.
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
        [Tooltip("Address for client to connect to (UnityTransport). Host should usually bind 0.0.0.0 for LAN.")]
        [SerializeField] private string address = "127.0.0.1";

        [Tooltip("Port for host/server to listen on and clients to connect to.")]
        [SerializeField] private ushort port = 7777;

        [Header("Steam / Facepunch Transport (optional)")]
        [Tooltip("If using FacepunchTransport, set this to the host's SteamId before starting client.")]
        [SerializeField] private ulong hostSteamId;

        private NetworkManager nm;

        private void Awake()
        {
            nm = NetworkManager.Singleton;
            if (nm == null)
                nm = FindFirstObjectByType<NetworkManager>();

            if (nm == null)
                nm = CreateNetworkManager();

            if (nm != null)
            {
                DontDestroyOnLoad(nm.gameObject);
                UnityTransportQueueFix.ApplyIfNeeded(nm, 512);
            }
        }

        private void Start()
        {
            if (autoStartHost)
                StartHost();
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

        private void ApplyTransportForStart(bool asHost)
        {
            if (nm == null) return;
            var t = nm.NetworkConfig.NetworkTransport;
            if (t == null) return;

            // Unity Transport (LAN/IP)
            if (t is UnityTransport utp)
            {
                // Host should bind to all interfaces for LAN.
                var bindAddr = asHost ? "0.0.0.0" : address;
                utp.SetConnectionData(bindAddr, port);
                Debug.Log($"[Net] Using UnityTransport: {(asHost ? "bind" : "connect")}={bindAddr}:{port}");
                return;
            }

            // Facepunch/Steam transport: try to configure host SteamId via reflection if provided.
            var typeName = t.GetType().FullName ?? t.GetType().Name;
            if (typeName.IndexOf("Facepunch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!asHost && hostSteamId != 0)
                {
                    TrySetSteamTarget(t, hostSteamId);
                }

                Debug.Log($"[Net] Using transport: {typeName}");
                return;
            }

            Debug.Log($"[Net] Using transport: {typeName} (no bootstrap config applied)");
        }

        private void LogTransportInfo()
        {
            if (nm == null) return;
            var t = nm.NetworkConfig.NetworkTransport;
            if (t == null)
            {
                Debug.LogWarning("[Net] NetworkManager has no transport assigned.");
                return;
            }

            Debug.Log($"[Net] Transport assigned: {t.GetType().FullName}");

            // Common misconfig: UnityTransport component present but not assigned.
            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null && t is not UnityTransport)
            {
                Debug.LogWarning("[Net] UnityTransport component is on NetworkManager but not assigned as active transport. " +
                                 "If you are using Facepunch/Steam transport, remove/disable UnityTransport to avoid confusion.");
            }
        }

        private static void TrySetSteamTarget(object transport, ulong steamId)
        {
            try
            {
                var type = transport.GetType();

                // Try common property names.
                foreach (var name in new[] { "TargetSteamId", "targetSteamId", "SteamId", "steamId", "HostSteamId", "hostSteamId" })
                {
                    var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanWrite && (prop.PropertyType == typeof(ulong) || prop.PropertyType == typeof(long)))
                    {
                        prop.SetValue(transport, prop.PropertyType == typeof(long) ? (long)steamId : steamId);
                        Debug.Log($"[Net] Set {type.Name}.{name} = {steamId}");
                        return;
                    }

                    var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null && (field.FieldType == typeof(ulong) || field.FieldType == typeof(long)))
                    {
                        field.SetValue(transport, field.FieldType == typeof(long) ? (long)steamId : steamId);
                        Debug.Log($"[Net] Set {type.Name}.{name} = {steamId}");
                        return;
                    }
                }

                Debug.LogWarning($"[Net] Could not set Steam target on transport {type.FullName}. " +
                                 "Configure the host SteamId/Lobby in your transport-specific flow.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Net] Failed to set Steam target: {ex.Message}");
            }
        }

        private void ApplyTransportForHost()
        {
            if (nm == null) return;
            var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null) return;
            utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            // Read back to verify (ConnectionData is a struct; SetConnectionData already sets it)
            var data = utp.ConnectionData;
            Debug.Log($"[Net] Host transport: listen {data.ServerListenAddress ?? "(null)"}:{data.Port} (Address={data.Address})");
        }

        public void StartHost()
        {
            if (nm == null) return;
            if (nm.IsListening) return;

            ApplyTransportForHost();
            nm.StartHost();
            Debug.Log("[Net] Started Host");
        }

        public void StartClient()
        {
            if (nm == null) return;
            if (nm.IsListening) return;

            ApplyTransportForStart(asHost: false);
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
