using System.Collections.Generic;
using DungeonGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// When the local player levels up, shows 3 cards. Player picks 1.
    /// Subscribe to DungeonRunExp.OnLevelUpRequestCards.
    /// </summary>
    public class LevelUpCardPickerUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [Tooltip("Optional title label shown above the cards")]
        [SerializeField] private Text titleLabel;
        [SerializeField] private string titleText = "Level Up! Choose one:";

        private DungeonRunExp _runExp;
        private List<DungeonCardConfig> _currentCards;

        private void Start()
        {
            _runExp = FindFirstObjectByType<DungeonRunExp>();
            if (_runExp != null)
            {
                _runExp.OnLevelUpRequestCards += OnLevelUpCards;
                _runExp.OnCardPicked += OnCardPicked;
            }

            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_runExp != null)
            {
                _runExp.OnLevelUpRequestCards -= OnLevelUpCards;
                _runExp.OnCardPicked -= OnCardPicked;
            }
        }

        private void OnLevelUpCards(List<DungeonCardConfig> cards)
        {
            _currentCards = cards ?? new List<DungeonCardConfig>();
            Show();
        }

        private void OnCardPicked()
        {
            Hide();
        }

        private void Show()
        {
            if (panel != null) panel.SetActive(true);
            if (titleLabel != null) titleLabel.text = titleText;

            if (cardContainer == null || cardPrefab == null) return;

            foreach (Transform c in cardContainer)
                Destroy(c.gameObject);

            for (int i = 0; i < _currentCards.Count; i++)
            {
                var cfg = _currentCards[i];
                var go = Instantiate(cardPrefab, cardContainer);

                var cardDisplay = go.GetComponent<LevelUpCardDisplay>();
                if (cardDisplay != null && cfg != null)
                    cardDisplay.Populate(cfg);
                else if (cfg != null)
                {
                    var label = go.GetComponentInChildren<Text>();
                    if (label != null) label.text = cfg.displayName;
                }

                var btn = go.GetComponent<Button>();
                int idx = i;
                if (btn != null)
                    btn.onClick.AddListener(() => OnPickCard(idx));
            }
        }

        private void OnPickCard(int index)
        {
            if (_runExp == null || _currentCards == null) return;
            if (index < 0 || index >= _currentCards.Count) return;

            var cfg = _currentCards[index];
            if (cfg == null) return;

            _runExp.ChooseCardRpc(new Unity.Collections.FixedString32Bytes(cfg.id));
            Hide();
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
            _currentCards = null;
        }
    }
}
