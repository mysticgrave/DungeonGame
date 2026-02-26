#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

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

        private const string HostSteamIdKey = "HostSteamId";
        private const string GameNameKey = "GameName";
        private const string GameNameValue = "DungeonGame";
        private const string LobbyNameKey = "LobbyName";

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

            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsListening)
                nm.StartHost();

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
            if (InLobby)
            {
                CurrentLobby.Leave();
                InLobby = false;
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                nm.Shutdown();

            OnLobbyLeft?.Invoke();
            Debug.Log("[SteamLobby] Left lobby.");
        }

        /// <summary>Open the Steam overlay invite dialog for the current lobby.</summary>
        public void InviteFriends()
        {
            if (!InLobby) return;
            SteamFriends.OpenGameInviteOverlay(CurrentLobby.Id);
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

            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                var hostIdStr = lobby.GetData(HostSteamIdKey);
                if (ulong.TryParse(hostIdStr, out ulong hostId))
                {
                    SetTargetSteamId(hostId);
                    NetworkManager.Singleton.StartClient();
                    Debug.Log($"[SteamLobby] Joined lobby {lobby.Id}, connecting to host {hostId}");
                }
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
        /// </summary>
        private static void SetTargetSteamId(ulong steamId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var transport = nm.NetworkConfig.NetworkTransport;
            var targetField = transport.GetType().GetField("targetSteamId");
            if (targetField != null)
            {
                targetField.SetValue(transport, steamId);
            }
            else
            {
                var prop = transport.GetType().GetProperty("targetSteamId");
                if (prop != null) prop.SetValue(transport, steamId);
            }
        }
    }
}
#endif
