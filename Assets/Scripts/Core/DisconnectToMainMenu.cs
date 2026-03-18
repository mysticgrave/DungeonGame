using DungeonGame.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.Core
{
    /// <summary>
    /// Sends the client back to the main menu when they disconnect from the host.
    /// Attach to the NetworkManager GameObject (which is DontDestroyOnLoad).
    /// Also handles the host shutting down — loads main menu for them too.
    /// </summary>
    public class DisconnectToMainMenu : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private NetworkManager _nm;
        private bool _wasConnected;
        private bool _returningToMenu; // prevent re-entrant calls

        private void OnEnable()
        {
            _nm = GetComponent<NetworkManager>();
            if (_nm == null) _nm = NetworkManager.Singleton;
            if (_nm == null) return;

            _nm.OnClientDisconnectCallback += OnClientDisconnect;
            _nm.OnServerStopped += OnServerStopped;
            _nm.OnClientStopped += OnClientStopped;
        }

        private void OnDisable()
        {
            if (_nm == null) return;
            _nm.OnClientDisconnectCallback -= OnClientDisconnect;
            _nm.OnServerStopped -= OnServerStopped;
            _nm.OnClientStopped -= OnClientStopped;
        }

        private void Update()
        {
            if (_nm != null && _nm.IsListening)
            {
                _wasConnected = true;
                _returningToMenu = false;
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (_nm == null) return;

            // Server receives this for every client that leaves — ignore unless it's us.
            if (_nm.IsServer) return;

            if (clientId == _nm.LocalClientId)
            {
                var reason = _nm.DisconnectReason ?? "(no reason given)";
                Debug.Log($"[Disconnect] Lost connection to host — returning to main menu. Reason: {reason}");
                ReturnToMainMenu();
            }
        }

        private void OnServerStopped(bool wasHost)
        {
            if (!_wasConnected) return;
            _wasConnected = false;
            Debug.Log("[Disconnect] Server stopped — returning to main menu.");
            ReturnToMainMenu();
        }

        /// <summary>
        /// Fires when the local client transport disconnects (covers cases where
        /// OnClientDisconnect doesn't fire, e.g. transport-level failures).
        /// </summary>
        private void OnClientStopped(bool wasHost)
        {
            if (!_wasConnected) return;
            if (wasHost) return; // OnServerStopped already handles host-side
            _wasConnected = false;
            Debug.Log("[Disconnect] Client stopped — returning to main menu.");
            ReturnToMainMenu();
        }

        private void ReturnToMainMenu()
        {
            if (_returningToMenu) return;
            _returningToMenu = true;

            var activeScene = SceneManager.GetActiveScene().name;
            if (activeScene == mainMenuSceneName) return;

            LoadingScreenManager.Instance?.ShowWithMessage("Returning to main menu...", showCancelButton: false);

#if !DISABLESTEAMWORKS
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.LeaveLobby();
            else
#endif
            if (_nm != null && _nm.IsListening)
                _nm.Shutdown();

            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
