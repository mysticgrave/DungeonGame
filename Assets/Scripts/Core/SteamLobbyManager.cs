#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.Core
{
    /// <summary>
    /// Manages Steam lobbies for hosting, joining, inviting, and listing.
    /// Works with FacepunchTransport on the NetworkManager.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        /// <summary>The current lobby we're in (default if none).</summary>
        public Lobby CurrentLobby { get; private set; }
        public bool InLobby { get; private set; }

        public event Action OnLobbyCreated;
        public event Action OnLobbyJoined;
        public event Action OnLobbyLeft;
        public event Action<List<Lobby>> OnLobbyListReceived;
        /// <summary>Fired when joining a Steam lobby but the game cannot connect (e.g. wrong transport). Passes a short reason for the UI.</summary>
        public event Action<string> OnSteamJoinFailed;

        private const string HostSteamIdKey = "HostSteamId";
        private const string GameNameKey = "GameName";
        private const string GameNameValue = "DungeonGame";
        private const string LobbyNameKey = "LobbyName";

        [Tooltip("Scene to load when hosting via Steam (e.g. Town). Must match LobbyMenuController's game scene.")]
        [SerializeField] private string gameSceneName = "Town";

        private bool _pendingHostAfterLobbyCreated;
        private int _pendingHostFramesRemaining;
        private const int HostDeferFrames = 30; // Wait for Steam relay to initialize before StartHost
        private ulong _pendingClientConnectHostId;
        private int _pendingClientConnectFramesRemaining = 15; // Frames to wait before StartClient (Steam relay + host scene stability)
        private bool _checkedLaunchCommandLine;

        private System.Collections.IEnumerator DeferredLoadGameScene()
        {
            yield return null; // Wait one frame so StartHost is fully ready
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && nm.SceneManager != null && !string.IsNullOrEmpty(gameSceneName))
            {
                nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                Debug.Log($"[SteamLobby] Loaded game scene: {gameSceneName}");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += HandleMemberLeft;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
        }

        private void OnDisable()
        {
            SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= HandleMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleMemberLeft;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;

            // Check command line once: when game is launched by Steam via "Join Game" invite
            if (!_checkedLaunchCommandLine && SteamManager.Initialized)
            {
                _checkedLaunchCommandLine = true;
                TryJoinLobbyFromCommandLine();
            }

            if (nm == null) return;

            // Don't attempt to start host/client while networking is still active.
            // This prevents race conditions when leaving a session and immediately rehosting.
            if (nm.IsListening || nm.ShutdownInProgress) return;

            if (_pendingHostAfterLobbyCreated && CurrentLobby.Id != 0)
            {
                // Defer StartHost by several frames to let Steam relay fully initialize.
                // CreateRelaySocket throws "Invalid Socket" if called too soon after
                // InitRelayNetworkAccess / lobby creation.
                if (_pendingHostFramesRemaining > 0)
                {
                    _pendingHostFramesRemaining--;
                    return;
                }

                _pendingHostAfterLobbyCreated = false;
                try
                {
                    var fp = nm.gameObject.GetComponent<FacepunchTransport>();
                    if (fp != null)
                    {
                        nm.NetworkConfig.NetworkTransport = fp;
                        Debug.Log("[SteamLobby] Using FacepunchTransport for host (Steam relay).");
                    }
                    else
                    {
                        if (nm.NetworkConfig.NetworkTransport is UnityTransport utp)
                        {
                            ushort port = utp.ConnectionData.Port;
                            if (port == 0) port = 7777;
                            utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
                        }
                    }
                    var hostConfigHash = nm.NetworkConfig.GetConfig(false);
                    Debug.Log($"[SteamLobby] HOST ConfigHash={hostConfigHash} — must match client.");

                    // ForceSamePrefabs causes NGO to hash-check the NetworkPrefabs list before
                    // connection approval runs. Any mismatch (stale GUIDs, editor vs build, etc.)
                    // causes an immediate ClosedByPeer/App_Min disconnect. Disable it — both sides
                    // ship the same build so the check adds no safety value.
                    nm.NetworkConfig.ForceSamePrefabs = false;

                    // Disable ConnectionApproval — with a custom transport the
                    // ConnectionRequestMessage may not arrive before the approval timeout,
                    // causing spurious disconnects. The callback is still set as a safety net
                    // in case it's toggled on in the inspector.
                    nm.NetworkConfig.ConnectionApproval = false;
                    nm.ConnectionApprovalCallback = (request, response) =>
                    {
                        Debug.Log($"[SteamLobby] Approving connection from clientId={request.ClientNetworkId}");
                        response.Approved = true;
                        response.CreatePlayerObject = true;
                    };

                    nm.StartHost();

                    // Defer Town load by one frame so StartHost is fully processed before scene change.
                    // Helps clients that connect quickly get correct scene sync.
                    if (nm.IsServer && nm.SceneManager != null && !string.IsNullOrEmpty(gameSceneName))
                    {
                        Instance.StartCoroutine(DeferredLoadGameScene());
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                return;
            }

            if (_pendingClientConnectHostId != 0 && InLobby)
            {
                if (_pendingClientConnectFramesRemaining > 0)
                {
                    _pendingClientConnectFramesRemaining--;
                    if (_pendingClientConnectFramesRemaining == 0)
                        Debug.Log("[SteamLobby] Defer complete, starting client connect now.");
                    return;
                }
                ulong hostId = _pendingClientConnectHostId;
                _pendingClientConnectHostId = 0;
                try
                {
                    if (SetTargetSteamId(hostId))
                    {
                        // Must match host — disable so editor/build GUID differences don't block connections.
                        nm.NetworkConfig.ForceSamePrefabs = false;
                        var clientConfigHash = nm.NetworkConfig.GetConfig(false);
                        Debug.Log($"[SteamLobby] CLIENT ConfigHash={clientConfigHash} — must match host.");
                        Debug.Log($"[SteamLobby] Calling StartClient (host Steam ID {hostId})...");
                        nm.StartClient();
                        Debug.Log("[SteamLobby] StartClient returned; waiting for transport/Netcode connection.");
                    }
                    else
                    {
                        Debug.LogError("[SteamLobby] SetTargetSteamId failed.");
                        OnSteamJoinFailed?.Invoke("Could not set target Steam ID. Add FacepunchTransport to NetworkManager.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    string msg = hostId == SteamClient.SteamId
                        ? "Cannot connect to yourself. For local testing, use two different Steam accounts (e.g. ParrelSync) or two PCs."
                        : "Steam relay connection failed. Try again or use direct IP join.";
                    OnSteamJoinFailed?.Invoke(msg);
                }
            }
        }

        // --- Public API ---

        /// <summary>Create a lobby and start as Host. If publicLobby is true, the lobby appears in the public lobby list (for matchmaking boards).</summary>
        public async void HostLobby(int maxPlayers = 4, bool publicLobby = false)
        {
            if (!SteamManager.Initialized) { Debug.LogError("[SteamLobby] Steam not initialized."); return; }

            var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (!lobby.HasValue)
            {
                Debug.LogError("[SteamLobby] Failed to create lobby.");
                return;
            }

            CurrentLobby = lobby.Value;
            if (publicLobby)
                CurrentLobby.SetPublic();
            else
                CurrentLobby.SetFriendsOnly();

            CurrentLobby.SetData(HostSteamIdKey, SteamClient.SteamId.ToString());
            CurrentLobby.SetData(GameNameKey, GameNameValue);
            CurrentLobby.SetData(LobbyNameKey, SteamClient.Name + "'s Game");
            CurrentLobby.SetGameServer(SteamClient.SteamId);
            InLobby = true;

            // Defer StartHost to allow Steam relay to fully initialize.
            // SteamNetworkingSockets.CreateRelaySocket throws "Invalid Socket" when called
            // too soon after InitRelayNetworkAccess or from a worker thread.
            _pendingHostAfterLobbyCreated = true;
            _pendingHostFramesRemaining = HostDeferFrames;

            Debug.Log($"[SteamLobby] Created lobby {CurrentLobby.Id} — hosting as {SteamClient.Name}" + (publicLobby ? " (public)" : " (friends only)"));
            OnLobbyCreated?.Invoke();
        }

        /// <summary>Join an existing lobby by its ID.</summary>
        public async void JoinLobby(SteamId lobbyId)
        {
            if (!SteamManager.Initialized) return;

            var lobby = new Lobby(lobbyId);
            await lobby.Join();
        }

        /// <summary>Leave the current lobby and shut down networking.</summary>
        public void LeaveLobby()
        {
            Debug.Log("[SteamLobby] LeaveLobby called.");

            // Reset ALL pending state first — prevents Update() from trying to StartHost/StartClient
            _pendingHostAfterLobbyCreated = false;
            _pendingHostFramesRemaining = 0;
            _pendingClientConnectHostId = 0;
            _pendingClientConnectFramesRemaining = 0;

            if (InLobby)
            {
                try { CurrentLobby.Leave(); }
                catch (System.Exception ex) { Debug.LogWarning($"[SteamLobby] Lobby.Leave error: {ex.Message}"); }
                InLobby = false;
            }
            CurrentLobby = default; // Reset so stale lobby ID doesn't persist

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                nm.Shutdown();

            OnLobbyLeft?.Invoke();
            Debug.Log("[SteamLobby] Left lobby — all state reset.");
        }

        /// <summary>Open the Steam overlay invite dialog for the current lobby.</summary>
        public void InviteFriends()
        {
            if (!InLobby) return;
            SteamFriends.OpenGameInviteOverlay(CurrentLobby.Id);
        }

        private void TryJoinLobbyFromCommandLine()
        {
            if (InLobby || NetworkManager.Singleton?.IsListening == true) return;

            var cmd = Steamworks.SteamApps.CommandLine;
            if (string.IsNullOrWhiteSpace(cmd)) return;

            // Parse +connect_lobby <id> (Steam passes this when game is launched via "Join Game" invite)
            var match = System.Text.RegularExpressions.Regex.Match(cmd, @"[+\s]connect_lobby\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) return;

            if (ulong.TryParse(match.Groups[1].Value, out ulong lobbyId) && lobbyId != 0)
            {
                Debug.Log($"[SteamLobby] Joining lobby {lobbyId} from launch command line.");
                JoinLobby((SteamId)lobbyId);
            }
        }

        /// <summary>Request a list of public/friends lobbies for this game.</summary>
        public async void RequestLobbyList()
        {
            if (!SteamManager.Initialized) return;

            var lobbies = await SteamMatchmaking.LobbyList
                .WithKeyValue(GameNameKey, GameNameValue)
                .RequestAsync();

            var results = new List<Lobby>();
            if (lobbies != null)
            {
                foreach (var l in lobbies)
                    results.Add(l);
            }

            Debug.Log($"[SteamLobby] Found {results.Count} lobbies.");
            OnLobbyListReceived?.Invoke(results);
        }

        /// <summary>Number of members in the current lobby.</summary>
        public int MemberCount => InLobby ? CurrentLobby.MemberCount : 0;

        /// <summary>Display name for a lobby in the list (used by LobbyListUI).</summary>
        public static string GetLobbyDisplayName(Lobby lobby)
        {
            var name = lobby.GetData(LobbyNameKey);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            try { return lobby.Owner.Name + "'s Lobby"; } catch { return "Lobby"; }
        }

        // --- Steam Callbacks ---

        private void HandleLobbyCreated(Result result, Lobby lobby)
        {
            if (result != Result.OK)
                Debug.LogError($"[SteamLobby] Lobby creation failed: {result}");
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;
            InLobby = true;
            var nm = NetworkManager.Singleton;
            Debug.Log($"[SteamLobby] HandleLobbyEntered: lobby={lobby.Id}, owner={lobby.Owner.Id}, members={lobby.MemberCount}, nm.IsListening={nm?.IsListening}, ShutdownInProgress={nm?.ShutdownInProgress}");

            if (nm != null && !nm.IsListening && !nm.ShutdownInProgress)
            {
                // Prefer lobby owner (creator) — authoritative. Lobby data can be stale/wrong.
                ulong hostId = lobby.Owner.Id > 0 ? lobby.Owner.Id : 0;
                if (hostId > 0)
                    Debug.Log($"[SteamLobby] Using lobby owner as host ID: {hostId}");
                else
                {
                    var hostIdStr = lobby.GetData(HostSteamIdKey);
                    if (!string.IsNullOrEmpty(hostIdStr) && ulong.TryParse(hostIdStr, out hostId))
                        Debug.Log($"[SteamLobby] Got host ID from lobby data (owner missing): {hostId}");
                }

                // "Are we the host?" = lobby owner (creator). Owner is authoritative; lobby data can be stale.
                if (lobby.Owner.Id > 0 && lobby.Owner.Id == SteamClient.SteamId)
                {
                    Debug.Log("[SteamLobby] We are the lobby owner (host); skipping client connect.");
                    return; // Don't fire OnLobbyJoined — host shouldn't see "Connecting to host..."
                }

                if (hostId == 0)
                {
                    Debug.LogError("[SteamLobby] Could not get host Steam ID from lobby.");
                    OnSteamJoinFailed?.Invoke("Could not get host Steam ID from lobby.");
                    return;
                }
                else
                {
                    var fp = nm.gameObject.GetComponent<FacepunchTransport>();
                    if (fp == null)
                    {
                        Debug.LogError("[SteamLobby] Add FacepunchTransport to the NetworkManager to join via Steam lobby.");
                        OnSteamJoinFailed?.Invoke("Add FacepunchTransport to the NetworkManager GameObject in MainMenu to join via Steam lobby.");
                    }
                    else
                    {
                        nm.NetworkConfig.NetworkTransport = fp;
                        _pendingClientConnectHostId = hostId;
                        _pendingClientConnectFramesRemaining = 15; // Steam relay propagation + host scene stability
                        Debug.Log($"[SteamLobby] Joined lobby as client, will StartClient in {_pendingClientConnectFramesRemaining} frames (host Steam ID: {hostId}).");
                    }
                }
            }
            else
            {
                Debug.Log("[SteamLobby] HandleLobbyEntered: skipping client connect (IsListening or no NM).");
            }

            OnLobbyJoined?.Invoke();
        }

        private void HandleMemberJoined(Lobby lobby, Friend friend)
        {
            Debug.Log($"[SteamLobby] {friend.Name} joined the lobby.");
        }

        private void HandleMemberLeft(Lobby lobby, Friend friend)
        {
            Debug.Log($"[SteamLobby] {friend.Name} left the lobby.");
        }

        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            Debug.Log($"[SteamLobby] Join requested via Steam friends list — lobby {lobby.Id}");
            JoinLobby(lobby.Id);
        }

        // --- Helpers ---

        /// <summary>
        /// Set the target Steam ID on the FacepunchTransport so the client knows who to connect to.
        /// Returns true if the transport accepted the Steam ID (e.g. FacepunchTransport).
        /// </summary>
        private static bool SetTargetSteamId(ulong steamId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;

            var transport = nm.NetworkConfig.NetworkTransport;
            if (transport is FacepunchTransport fp)
            {
                fp.targetSteamId = steamId;
                Debug.Log($"[SteamLobby] Set FacepunchTransport.targetSteamId = {steamId}");
                return true;
            }

            var targetField = transport.GetType().GetField("targetSteamId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (targetField != null)
            {
                targetField.SetValue(transport, steamId);
                return true;
            }
            var prop = transport.GetType().GetProperty("targetSteamId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(transport, steamId);
                return true;
            }
            Debug.LogWarning("[SteamLobby] Current transport has no targetSteamId (use FacepunchTransport for Steam lobby join).");
            return false;
        }
    }
}
#endif
