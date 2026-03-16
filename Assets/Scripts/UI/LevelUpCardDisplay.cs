using DungeonGame.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Attach to the card prefab. Populates name, description, stats, and icon from DungeonCardConfig.
    /// Assign UI elements in the Inspector; LevelUpCardPickerUI will call Populate(config).
    /// </summary>
    public class LevelUpCardDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Card title (e.g. 'Horde')")]
        [SerializeField] private Text nameText;
        [Tooltip("Card description (what it does)")]
        [SerializeField] private Text descriptionText;
        [Tooltip("Stat lines (e.g. '+50% Max Enemies'). Uses first Text if multiple, or joins lines.")]
        [SerializeField] private Text statsText;
        [Tooltip("Optional icon image")]
        [SerializeField] private Image iconImage;
        [Tooltip("Optional rarity label")]
        [SerializeField] private Text rarityText;
        [Tooltip("Optional: tint border/frame by rarity")]
        [SerializeField] private Image rarityBorderImage;
        [Tooltip("Rarity colors. Index 0=Common, 1=Uncommon, 2=Rare, 3=Legendary")]
        [SerializeField] private Color[] rarityColors = { new(0.6f, 0.6f, 0.6f), new(0.2f, 0.8f, 0.2f), new(0.2f, 0.4f, 1f), new(1f, 0.6f, 0f) };
        [Tooltip("Separator between stat lines. Default: newline.")]
        [SerializeField] private string statSeparator = "\n";

        /// <summary>Populate this card from the Dungeon Card config. Call after instantiating the prefab.</summary>
        public void Populate(DungeonCardConfig config)
        {
            if (config == null) return;

            if (nameText != null)
                nameText.text = config.displayName;

            if (descriptionText != null)
                descriptionText.text = config.description;

            if (statsText != null)
            {
                var lines = config.GetDisplayStatLines();
                statsText.text = string.Join(statSeparator, lines);
                statsText.gameObject.SetActive(lines.Count > 0);
            }

            if (iconImage != null)
            {
                if (config.icon != null)
                {
                    iconImage.sprite = config.icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            int rarityIdx = (int)config.rarity;
            if (rarityText != null)
                rarityText.text = config.rarity.ToString();
            if (rarityBorderImage != null && rarityIdx >= 0 && rarityIdx < rarityColors.Length)
                rarityBorderImage.color = rarityColors[rarityIdx];
        }
    }
}
