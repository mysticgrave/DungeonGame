using System.Collections.Generic;
using DungeonGame.Core;
using DungeonGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Pick-3 modifier UI. Host selects 3 modifiers before entering the dungeon.
    /// Wire to a panel with a grid of toggles/buttons and a Confirm button.
    /// </summary>
    public class Pick3Controller : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform optionContainer;
        [SerializeField] private GameObject optionPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button skipButton;
        [Tooltip("Optional status text")]
        [SerializeField] private Text statusText;

        private readonly List<DungeonCardConfig> _pool = new();
        private readonly List<int> _selectedIndices = new();
        private const int PickCount = 3;

        private void Start()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkip);
        }

        /// <summary>Show the Pick-3 panel. Host fetches pool from SpireRunState.</summary>
        public void Show()
        {
            if (panel != null) panel.SetActive(true);

            _pool.Clear();
            _selectedIndices.Clear();

            var run = FindFirstObjectByType<SpireRunState>();
            if (run != null)
                _pool.AddRange(run.GetShuffledPool(9));
            else
                Debug.LogWarning("[Pick3] No SpireRunState — cannot get modifier pool.");

            RefreshOptionUI();
            UpdateStatus();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void RefreshOptionUI()
        {
            if (optionContainer == null || optionPrefab == null) return;

            foreach (Transform c in optionContainer)
                Destroy(c.gameObject);

            for (int i = 0; i < _pool.Count; i++)
            {
                var cfg = _pool[i];
                var go = Instantiate(optionPrefab, optionContainer);

                var cardDisplay = go.GetComponent<LevelUpCardDisplay>();
                if (cardDisplay != null && cfg != null)
                    cardDisplay.Populate(cfg);
                else if (cfg != null)
                {
                    var label = go.GetComponentInChildren<Text>();
                    if (label != null) label.text = cfg.displayName;
                }

                var toggle = go.GetComponent<Toggle>();
                var btn = go.GetComponent<Button>();
                int idx = i;

                if (toggle != null)
                {
                    toggle.isOn = _selectedIndices.Contains(idx);
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.onValueChanged.AddListener(on =>
                    {
                        if (on)
                        {
                            if (_selectedIndices.Count < PickCount)
                                _selectedIndices.Add(idx);
                            else
                                toggle.isOn = false;
                        }
                        else
                            _selectedIndices.Remove(idx);
                        UpdateStatus();
                    });
                }
                else if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        if (_selectedIndices.Contains(idx))
                            _selectedIndices.Remove(idx);
                        else if (_selectedIndices.Count < PickCount)
                            _selectedIndices.Add(idx);
                        UpdateStatus();
                        RefreshOptionUI();
                    });
                }
            }
        }

        private void UpdateStatus()
        {
            if (statusText != null)
                statusText.text = $"Pick {PickCount}: {_selectedIndices.Count}/{PickCount}";
            if (confirmButton != null)
                confirmButton.interactable = _selectedIndices.Count == PickCount;
        }

        private void OnConfirm()
        {
            if (_selectedIndices.Count != PickCount) return;

            var run = FindFirstObjectByType<SpireRunState>();
            if (run == null)
            {
                Debug.LogWarning("[Pick3] No SpireRunState — cannot set modifiers.");
                Hide();
                return;
            }

            if (!run.IsServer)
            {
                Debug.Log("[Pick3] Only host can set modifiers.");
                Hide();
                return;
            }

            var ids = new List<string>();
            foreach (int i in _selectedIndices)
            {
                if (i >= 0 && i < _pool.Count && _pool[i] != null)
                    ids.Add(_pool[i].id);
            }
            run.SetRunModifiers(ids);
            Hide();

            var townPlay = FindFirstObjectByType<TownPlayController>();
            if (townPlay != null)
                townPlay.LoadDungeonScene();
        }

        private void OnSkip()
        {
            var run = FindFirstObjectByType<SpireRunState>();
            if (run != null && run.IsServer)
                run.SetRunModifiers(null);

            Hide();
            var townPlay = FindFirstObjectByType<TownPlayController>();
            if (townPlay != null)
                townPlay.LoadDungeonScene();
        }
    }
}
