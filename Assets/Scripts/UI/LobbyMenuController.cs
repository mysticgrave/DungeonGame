using DungeonGame.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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

        [Header("Title (auto-populated if empty)")]
        [SerializeField] private Text gameTitleText;

        private Canvas _canvas;
        private Font _font;

        private void Awake()
        {
            EnsureCursorVisible();
            if (titlePanel == null) EnsureUI();
        }

        private void Start()
        {
            EnsureCursorVisible();
            ShowPanel(titlePanel);
            LoadingScreenManager.OnCancelRequested += OnCancelConnect;

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
            LoadingScreenManager.OnCancelRequested -= OnCancelConnect;
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
            ShowLoadingScreen("Creating lobby...");
#if !DISABLESTEAMWORKS
            if (_useSteam)
            {
                SteamLobbyManager.Instance?.HostLobby(maxPlayers, publicLobby: true);
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

            ShowLoadingScreen($"Connecting to {address}:{port}...");
        }

        private void OnCancelConnect()
        {
#if !DISABLESTEAMWORKS
            if (_useSteam)
                SteamLobbyManager.Instance?.LeaveLobby();
#endif
            ShutdownAndReturn();
            // Ensure title panel is shown (OnLobbyLeft may fire async or order can vary)
            ShowPanel(titlePanel != null ? titlePanel : (joinPanel != null ? joinPanel : connectingPanel));
        }

        // --- Steam / Host flow ---

        private void OnHostLobbyCreated()
        {
            LoadGameScene();
        }

        private void OnClientJoinedLobby()
        {
            Debug.Log("[Lobby] OnClientJoinedLobby: Showing loading screen 'Connecting to host...'");
            ShowLoadingScreen("Connecting to host...");
        }

        private void OnSteamJoinFailed(string reason)
        {
            HideLoadingScreen();
            ShowPanel(connectingPanel);
            if (connectingStatusText != null) connectingStatusText.text = reason + " Click Cancel to go back.";
        }

        private void OnLobbyLeft()
        {
            HideLoadingScreen();
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
            HideLoadingScreen();
            ShowPanel(titlePanel != null ? titlePanel : joinPanel);
        }

        private void ShowLoadingScreen(string message)
        {
            if (titlePanel != null) titlePanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(false);
            if (connectingPanel != null) connectingPanel.SetActive(false);
            LoadingScreenManager.Instance?.ShowWithMessage(message, showCancelButton: true);
        }

        private void HideLoadingScreen()
        {
            LoadingScreenManager.Instance?.Hide();
        }

        private void OnClientConnected(ulong clientId)
        {
            var nm = _nm != null ? _nm : NetworkManager.Singleton;
            var isLocal = nm != null && clientId == nm.LocalClientId;
            Debug.Log($"[Lobby] OnClientConnected: clientId={clientId} isLocal={isLocal} IsServer={nm?.IsServer} IsClient={nm?.IsClient}");
            // Nothing to do here for the client — the host's NetworkManager.SceneManager
            // automatically syncs the client into the correct scene. Loading it manually
            // would destroy all synced NetworkObjects and break everything.
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_nm == null) return;
            var isLocal = clientId == _nm.LocalClientId;
            var reason = _nm.DisconnectReason ?? "(none)";
            Debug.Log($"[Lobby] OnClientDisconnected: clientId={clientId} isLocal={isLocal} Reason={reason}");
            if (!_nm.IsServer && isLocal)
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

        // ────────────── Procedural UI ──────────────

        private void EnsureUI()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Canvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                var canvasGo = new GameObject("MainMenuCanvas");
                canvasGo.transform.SetParent(transform);
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            if (EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
            }

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_canvas.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 1f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            // ── Title Panel ──
            titlePanel = MakePanel("TitlePanel");

            // Game title
            var titleGo = MakeText(titlePanel.transform, "GameTitle", "DUNGEON GAME",
                new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.9f), 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            gameTitleText = titleGo.GetComponent<Text>();
            gameTitleText.color = new Color(1f, 0.85f, 0.2f);

            // Subtitle
            var subGo = MakeText(titlePanel.transform, "Subtitle", "Cooperative Dungeon Crawler",
                new Vector2(0.2f, 0.55f), new Vector2(0.8f, 0.65f), 18, FontStyle.Italic, TextAnchor.MiddleCenter);
            subGo.GetComponent<Text>().color = new Color(0.7f, 0.7f, 0.8f);

            // Title buttons
            MakeMenuButton(titlePanel.transform, "Host Game", new Vector2(0.35f, 0.38f), new Vector2(0.65f, 0.48f), HostAndPlay);
            MakeMenuButton(titlePanel.transform, "Join Game", new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.35f), ShowJoinPanel);
            MakeMenuButton(titlePanel.transform, "Quit",      new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.22f), Quit);

            // ── Join Panel ──
            joinPanel = MakePanel("JoinPanel");
            joinPanel.SetActive(false);

            MakeText(joinPanel.transform, "JoinTitle", "Join Game",
                new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.95f), 32, FontStyle.Bold, TextAnchor.MiddleCenter)
                .GetComponent<Text>().color = new Color(1f, 0.85f, 0.2f);

            // Address label + field
            MakeText(joinPanel.transform, "AddrLabel", "IP Address:",
                new Vector2(0.2f, 0.58f), new Vector2(0.38f, 0.66f), 16, FontStyle.Normal, TextAnchor.MiddleRight);
            joinAddressField = MakeInputField(joinPanel.transform, "AddressField",
                new Vector2(0.4f, 0.58f), new Vector2(0.75f, 0.66f), "127.0.0.1");

            // Port label + field
            MakeText(joinPanel.transform, "PortLabel", "Port:",
                new Vector2(0.2f, 0.47f), new Vector2(0.38f, 0.55f), 16, FontStyle.Normal, TextAnchor.MiddleRight);
            joinPortField = MakeInputField(joinPanel.transform, "PortField",
                new Vector2(0.4f, 0.47f), new Vector2(0.55f, 0.55f), defaultPort.ToString());

            // Join + Back buttons
            joinDirectButton = MakeMenuButton(joinPanel.transform, "Connect", new Vector2(0.35f, 0.30f), new Vector2(0.65f, 0.40f), OnJoinDirect).GetComponent<Button>();
            MakeMenuButton(joinPanel.transform, "Back", new Vector2(0.35f, 0.17f), new Vector2(0.65f, 0.27f), BackToTitle);

            // ── Connecting Panel ──
            connectingPanel = MakePanel("ConnectingPanel");
            connectingPanel.SetActive(false);

            var statusGo = MakeText(connectingPanel.transform, "Status", "Connecting...",
                new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f), 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            connectingStatusText = statusGo.GetComponent<Text>();

            cancelConnectButton = MakeMenuButton(connectingPanel.transform, "Cancel",
                new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.4f), OnCancelConnect).GetComponent<Button>();

            // Version text (bottom right)
            var versionGo = MakeText(_canvas.transform, "Version", "v0.1",
                new Vector2(0.85f, 0f), new Vector2(1f, 0.04f), 12, FontStyle.Normal, TextAnchor.LowerRight);
            versionGo.GetComponent<Text>().color = new Color(0.4f, 0.4f, 0.5f);
        }

        private GameObject MakePanel(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        private GameObject MakeText(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, FontStyle style, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = _font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4, 2);
            rt.offsetMax = new Vector2(-4, -2);
            return go;
        }

        private GameObject MakeMenuButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.2f, 0.3f, 0.9f);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.2f, 0.35f, 0.5f);
            colors.pressedColor = new Color(0.1f, 0.25f, 0.4f);
            btn.colors = colors;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            MakeText(go.transform, "Text", label,
                Vector2.zero, Vector2.one, 20, FontStyle.Bold, TextAnchor.MiddleCenter);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return go;
        }

        private InputField MakeInputField(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string defaultText)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var textGo = MakeText(go.transform, "Text", "",
                Vector2.zero, Vector2.one, 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            var placeholderGo = MakeText(go.transform, "Placeholder", defaultText,
                Vector2.zero, Vector2.one, 16, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholderGo.GetComponent<Text>().color = new Color(0.5f, 0.5f, 0.6f);

            var inputField = go.AddComponent<InputField>();
            inputField.textComponent = textGo.GetComponent<Text>();
            inputField.placeholder = placeholderGo.GetComponent<Text>();
            inputField.text = defaultText;

            return inputField;
        }

        private void EnsureCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ShowPanel(GameObject panel)
        {
            if (panel == null)
                panel = titlePanel != null ? titlePanel : joinPanel;
            if (titlePanel != null) titlePanel.SetActive(panel == titlePanel);
            if (joinPanel != null) joinPanel.SetActive(panel == joinPanel);
            if (connectingPanel != null) connectingPanel.SetActive(panel == connectingPanel);
        }
    }
}
