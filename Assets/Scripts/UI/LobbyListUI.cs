#if !DISABLESTEAMWORKS
using System.Collections.Generic;
using DungeonGame.Core;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Displays a list of Steam lobbies and lets the player join one.
    /// Use on the main menu Join panel or on an in-world "board" in Town.
    /// Assign a row prefab with LobbyListEntry and a content root (e.g. ScrollView content).
    /// </summary>
    public class LobbyListUI : MonoBehaviour
    {
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LobbyListEntry rowPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private GameObject emptyMessage;
        [SerializeField] private int maxPlayersDisplay = 4;

        private readonly List<LobbyListEntry> _rows = new();

        private void OnEnable()
        {
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.OnLobbyListReceived += OnLobbyListReceived;
            if (refreshButton != null)
                refreshButton.onClick.AddListener(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.OnLobbyListReceived -= OnLobbyListReceived;
            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(Refresh);
        }

        /// <summary>Request an updated lobby list from Steam.</summary>
        public void Refresh()
        {
            SteamLobbyManager.Instance?.RequestLobbyList();
        }

        private void OnLobbyListReceived(List<Lobby> lobbies)
        {
            ClearRows();
            if (emptyMessage != null)
                emptyMessage.SetActive(lobbies == null || lobbies.Count == 0);

            if (lobbies == null || contentRoot == null || rowPrefab == null) return;

            foreach (var lobby in lobbies)
            {
                var entry = Instantiate(rowPrefab, contentRoot);
                var displayName = SteamLobbyManager.GetLobbyDisplayName(lobby);
                var current = lobby.MemberCount;
                entry.SetLobby(lobby.Id, displayName, current, maxPlayersDisplay);
                _rows.Add(entry);
            }
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row != null && row.gameObject != null)
                    Destroy(row.gameObject);
            }
            _rows.Clear();
        }
    }

    /// <summary>
    /// Single row in the lobby list. Assign to a prefab with Text and Button.</summary>
    public class LobbyListEntry : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Button joinButton;

        private SteamId _lobbyId;

        public void SetLobby(SteamId lobbyId, string displayName, int memberCount, int maxPlayers)
        {
            _lobbyId = lobbyId;
            if (nameText != null)
                nameText.text = $"{displayName}  ({memberCount}/{maxPlayers})";
            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(OnJoinClicked);
            }
        }

        private void OnJoinClicked()
        {
            SteamLobbyManager.Instance?.JoinLobby(_lobbyId);
        }
    }
}
#endif
