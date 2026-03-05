using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Logs NGO connection/disconnect reasons and config hash to help debug transport issues.
    /// Attach to the NetworkManager GameObject.
    /// </summary>
    public class NetDiagnostics : MonoBehaviour
    {
        private NetworkManager nm;

        private void Awake()
        {
            nm = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            if (nm == null) nm = GetComponent<NetworkManager>();
            if (nm == null) return;

            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDisable()
        {
            if (nm == null) return;
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            var hash = nm.NetworkConfig != null ? nm.NetworkConfig.GetConfig(false).ConfigHash : 0UL;
            Debug.Log($"[NetDiag] Connected clientId={clientId} IsHost={nm.IsHost} IsServer={nm.IsServer} IsClient={nm.IsClient} ConfigHash={hash}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var reason = nm.DisconnectReason;
            var hash = nm.NetworkConfig != null ? nm.NetworkConfig.GetConfig(false).ConfigHash : 0UL;
            Debug.LogWarning($"[NetDiag] Disconnected clientId={clientId} IsHost={nm.IsHost} IsServer={nm.IsServer} IsClient={nm.IsClient} Reason='{reason}' ConfigHash={hash}");
        }
    }
}
