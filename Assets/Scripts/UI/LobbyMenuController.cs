using DungeonGame.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Streamlined main menu: Host goes straight to Town (the lobby). Join shows a lobby list (Steam) or direct IP.
    /// Town is the lobby area — no separate lobby panel. Public Steam lobbies appear in the list so players can join random groups.
    /// </summary>
    public class LobbyMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("Title: Host, Join, Settings, Quit.")]
        [SerializeField] private GameObject titlePanel;
        [Tooltip("Join: lobby list (and optional direct IP when not using Steam).")]
        [SerializeField] private GameObject joinPanel;
        [Tooltip("Shown while connecting to a lobby.")]
        [SerializeField] private GameObject connectingPanel;

        [Header("Join (Direct IP fallback)")]
        [SerializeField] private InputField joinAddressField;
        [SerializeField] private InputField joinPortField;
        [Tooltip("Click to connect using the IP and port entered above.")]
        [SerializeField] private Button joinDirectButton;
        [Tooltip("Optional second Join button (e.g. next to the IP field). Leave empty if you only use one.")]
        [SerializeField] private Button joinDirectButton2;

        [Header("Connecting")]
        [SerializeField] private Text connectingStatusText;
        [SerializeField] private Button cancelConnectButton;

        [Header("Config")]
        [SerializeField] private string gameSceneName = "Town";
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;

        private NetworkManager _nm;
        private bool _useSteam;

        private void Start()
        {
            ShowPanel(titlePanel);

#if !DISABLESTEAMWORKS
            _useSteam = SteamManager.Initialized;
#else
            _useSteam = false;
#endif

            if (joinPortField != null) joinPortField.text = defaultPort.ToString();
            if (joinAddressField != null) joinAddressField.text = "127.0.0.1";

            if (joinDirectButton != null) joinDirectButton.onClick.AddListener(OnJoinDirect);
            if (cancelConnectButton != null) cancelConnectButton.onClick.AddListener(OnCancelConnect);

            _nm = NetworkManager.Singleton;
            if (_nm != null)
            {
                UnityTransportQueueFix.ApplyIfNeeded(_nm, 512);
                _nm.OnClientConnectedCallback += OnClientConnected;
                _nm.OnClientDisconnectCallback += OnClientDisconnected;
            }

#if !DISABLESTEAMWORKS
            if (_useSteam && SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.OnLobbyCreated += OnHostLobbyCreated;
                SteamLobbyManager.Instance.OnLobbyJoined += OnClientJoinedLobby;
                SteamLobbyManager.Instance.OnLobbyLeft += OnLobbyLeft;
                SteamLobbyManager.Instance.OnSteamJoinFailed += OnSteamJoinFailed;
            }
#endif
        }

        private void OnDestroy()
        {
            if (_nm != null)
            {
                _nm.OnClientConnectedCallback -= OnClientConnected;
                _nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
#if !DISABLESTEAMWORKS
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.OnLobbyCreated -= OnHostLobbyCreated;
                SteamLobbyManager.Instance.OnLobbyJoined -= OnClientJoinedLobby;
                SteamLobbyManager.Instance.OnLobbyLeft -= OnLobbyLeft;
                SteamLobbyManager.Instance.OnSteamJoinFailed -= OnSteamJoinFailed;
            }
#endif
        }

        // --- Button handlers (wire these in the Inspector) ---

        /// <summary>Host & Play: create lobby and load Town immediately. Town is the lobby.</summary>
        public void HostAndPlay()
        {
#if !DISABLESTEAMWORKS
            if (_useSteam)
            {
                SteamLobbyManager.Instance?.HostLobby(maxPlayers, publicLobby: true);
                if (connectingStatusText != null) connectingStatusText.text = "Creating lobby...";
                ShowPanel(connectingPanel);
                return;
            }
#endif
            StartDirectHostAndLoadTown();
        }

        /// <summary>Show the Join panel (lobby list or direct IP).</summary>
        public void ShowJoinPanel() => ShowPanel(joinPanel);

        /// <summary>Back from Join panel to title.</summary>
        public void BackToTitle() => ShowPanel(titlePanel != null ? titlePanel : joinPanel);

        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Update()
        {
            if (!_useSteam && joinPanel != null && joinPanel.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                OnJoinDirect();
        }

        private void OnJoinDirect()
        {
            _nm = NetworkManager.Singleton;
            if (_nm == null || _nm.IsListening) return;

            string address = joinAddressField != null ? joinAddressField.text.Trim() : "127.0.0.1";
            if (string.IsNullOrEmpty(address)) address = "127.0.0.1";
            ushort port = ParsePort(joinPortField, defaultPort);
            SetTransportData(address, port);

            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            _nm.StartClient();

            ShowPanel(connectingPanel);
            if (connectingStatusText != null) connectingStatusText.text = $"Connecting to {address}:{port}...";
        }

        private void OnCancelConnect()
        {
#if !DISABLESTEAMWORKS
            if (_useSteam)
                SteamLobbyManager.Instance?.LeaveLobby();
#endif
            ShutdownAndReturn();
        }

        // --- Steam / Host flow ---

        private void OnHostLobbyCreated()
        {
            LoadGameScene();
        }

        private void OnClientJoinedLobby()
        {
            ShowPanel(connectingPanel);
            if (connectingStatusText != null) connectingStatusText.text = "Connecting to host...";
        }

        private void OnSteamJoinFailed(string reason)
        {
            ShowPanel(connectingPanel);
            if (connectingStatusText != null) connectingStatusText.text = reason + " Click Cancel to go back.";
        }

        private void OnLobbyLeft()
        {
            ShowPanel(titlePanel != null ? titlePanel : joinPanel);
        }

        private void StartDirectHostAndLoadTown()
        {
            _nm = NetworkManager.Singleton;
            if (_nm == null || _nm.IsListening) return;

            ushort port = ParsePort(null, defaultPort);
            SetTransportDataForHost(port);

            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            _nm.StartHost();

            Debug.Log("[Lobby] Started Host — loading Town.");
            LoadGameScene();
        }

        private void LoadGameScene()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        private void ShutdownAndReturn()
        {
            if (_nm != null)
            {
                _nm.OnClientConnectedCallback -= OnClientConnected;
                _nm.OnClientDisconnectCallback -= OnClientDisconnected;
                if (_nm.IsListening) _nm.Shutdown();
            }
            ShowPanel(titlePanel != null ? titlePanel : joinPanel);
        }

        private void OnClientConnected(ulong clientId)
        {
            // Nothing to do here for the client — the host's NetworkManager.SceneManager
            // automatically syncs the client into the correct scene. Loading it manually
            // would destroy all synced NetworkObjects and break everything.
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_nm == null) return;
            if (!_nm.IsServer && clientId == _nm.LocalClientId)
                ShutdownAndReturn();
        }

        private void SetTransportData(string address, ushort port)
        {
            if (_nm == null) _nm = NetworkManager.Singleton;
            if (_nm == null) return;
            var utp = _nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp != null) utp.SetConnectionData(address, port);
        }

        private void SetTransportDataForHost(ushort port)
        {
            if (_nm == null) _nm = NetworkManager.Singleton;
            if (_nm == null) return;
            var utp = _nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null) return;
            utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            var data = utp.ConnectionData;
            Debug.Log($"[Lobby] Host transport: listen {data.ServerListenAddress ?? "(null)"}:{data.Port} (Address={data.Address})");
        }

        private static ushort ParsePort(InputField field, ushort fallback)
        {
            if (field == null) return fallback;
            if (ushort.TryParse(field.text.Trim(), out ushort p)) return p;
            return fallback;
        }

        private void ShowPanel(GameObject panel)
        {
            if (titlePanel != null) titlePanel.SetActive(panel == titlePanel);
            if (joinPanel != null) joinPanel.SetActive(panel == joinPanel);
            if (connectingPanel != null) connectingPanel.SetActive(panel == connectingPanel);
        }
    }
}
