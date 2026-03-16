using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonGame.Run
{
    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }

    public enum CardEffectType
    {
        DamageDealt,
        DamageTaken,
        Gold,
        MaxEnemies,
        RespawnSpeed,
        MoodSwing
    }

    [Serializable]
    public struct CardEffect
    {
        [Tooltip("What this effect modifies")]
        public CardEffectType type;
        [Tooltip("Multiplier. >1 = buff, <1 = debuff. 1 = no change (don't add).")]
        [Range(0.25f, 4f)]
        public float multiplier;
        [Tooltip("Optional override for card display. Leave empty to auto-generate.")]
        public string customLabel;
    }

    /// <summary>
    /// Defines a Dungeon Card. Every card must have at least one buff (mult>1) and one debuff (mult&lt;1).
    /// Rarer cards have stronger effects; stronger upside should be paired with stronger downside.
    /// Create via Assets > Create > DungeonGame > Dungeon Card.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonCard", menuName = "DungeonGame/Dungeon Card", order = 1)]
    public class DungeonCardConfig : ScriptableObject
    {
        [Tooltip("Unique ID for network sync. Keep short and stable.")]
        public string id = "card_horde";

        [Tooltip("Display name on the card")]
        public string displayName = "Horde";

        [Tooltip("Short description (what it does, including the trade-off)")]
        [TextArea(2, 4)]
        public string description = "More enemies per spawn point, but respawns slower.";

        [Header("Rarity")]
        [Tooltip("Rarity affects how often this card appears in level-up picks. Rarer = less likely.")]
        public CardRarity rarity = CardRarity.Common;

        [Header("Display (optional)")]
        [Tooltip("Icon shown on the card. Leave empty for no icon.")]
        public Sprite icon;
        [Tooltip("Override all stat lines. If empty, stats are derived from effects below.")]
        [TextArea(1, 4)]
        public string customStatLines = "";

        [Header("Effects")]
        [Tooltip("Each card should have at least one buff (>1) and one debuff (<1). Stronger upside = stronger downside.")]
        [SerializeField] private CardEffect[] effects = Array.Empty<CardEffect>();

        /// <summary>Multiplier for a given effect type. Multiplies all effects of that type. Returns 1 if none.</summary>
        public float GetMultiplier(CardEffectType type)
        {
            if (effects == null || effects.Length == 0) return 1f;
            float m = 1f;
            foreach (var e in effects)
                if (e.type == type)
                    m *= e.multiplier;
            return Mathf.Clamp(m, 0.25f, 4f);
        }

        /// <summary>Legacy: spawn density (max enemies). Maps to MaxEnemies.</summary>
        public float GetSpawnDensityMultiplier() => GetMultiplier(CardEffectType.MaxEnemies);

        /// <summary>Legacy: respawn speed. &lt;1 = faster. Maps to RespawnSpeed.</summary>
        public float GetRespawnDelayMultiplier() => GetMultiplier(CardEffectType.RespawnSpeed);

        public float damageDealtMultiplier => GetMultiplier(CardEffectType.DamageDealt);
        public float damageTakenMultiplier => GetMultiplier(CardEffectType.DamageTaken);
        public float goldMultiplier => GetMultiplier(CardEffectType.Gold);
        public float moodSwingMultiplier => GetMultiplier(CardEffectType.MoodSwing);

        /// <summary>Weight for level-up pick. Rarer = lower weight.</summary>
        public float GetPickWeight()
        {
            return rarity switch
            {
                CardRarity.Common => 4f,
                CardRarity.Uncommon => 2f,
                CardRarity.Rare => 1f,
                CardRarity.Legendary => 0.5f,
                _ => 2f
            };
        }

        /// <summary>Stat lines for card display. Use customStatLines if set, otherwise from effects.</summary>
        public List<string> GetDisplayStatLines()
        {
            if (!string.IsNullOrWhiteSpace(customStatLines))
            {
                return customStatLines
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();
            }

            var stats = new List<string>();
            if (effects == null) return stats;

            foreach (var e in effects)
            {
                if (e.multiplier == 1f) continue;
                var label = !string.IsNullOrEmpty(e.customLabel)
                    ? e.customLabel
                    : FormatStat(GetEffectLabel(e.type), e.multiplier, e.type == CardEffectType.RespawnSpeed);
                stats.Add(label);
            }
            return stats;
        }

        private static string GetEffectLabel(CardEffectType t)
        {
            return t switch
            {
                CardEffectType.DamageDealt => "Damage Dealt",
                CardEffectType.DamageTaken => "Damage Taken",
                CardEffectType.Gold => "Gold",
                CardEffectType.MaxEnemies => "Max Enemies",
                CardEffectType.RespawnSpeed => "Respawn Speed",
                CardEffectType.MoodSwing => "Mood Swing",
                _ => t.ToString()
            };
        }

        private static string FormatStat(string label, float mult, bool invert = false)
        {
            float pct = (mult - 1f) * 100f;
            if (invert && mult != 1f)
                pct = (1f / mult - 1f) * 100f;
            string sign = pct >= 0 ? "+" : "";
            return $"{label}: {sign}{pct:F0}%";
        }

        /// <summary>True if card has at least one buff and one debuff.</summary>
        public bool IsValid()
        {
            if (effects == null || effects.Length == 0) return false;
            bool hasBuff = effects.Any(e => e.multiplier > 1f);
            bool hasDebuff = effects.Any(e => e.multiplier < 1f);
            return hasBuff && hasDebuff;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (effects != null && effects.Length > 0 && !IsValid())
                Debug.LogWarning($"[DungeonCard] '{displayName}' should have at least one buff (>1) and one debuff (<1). Stronger upside = stronger downside.", this);
        }
#endif
    }
}
