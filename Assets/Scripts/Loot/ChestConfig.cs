using DungeonGame.Weapons;
using UnityEngine;

namespace DungeonGame.Loot
{
    /// <summary>
    /// Defines a chest tier/type: what prefab to spawn and what loot it can drop.
    /// Create via Assets → Create → DungeonGame → Chest Config.
    /// </summary>
    [CreateAssetMenu(fileName = "NewChest", menuName = "DungeonGame/Chest Config")]
    public class ChestConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in UI or debug logs.")]
        public string chestName = "Chest";

        [Header("Prefab")]
        [Tooltip("The chest prefab to instantiate. Must have NetworkObject + LootChest components.")]
        public GameObject prefab;

        [Header("Spawn Weight")]
        [Tooltip("Relative weight when this chest type is picked from a spawn point's pool. Higher = more common.")]
        [Min(0.01f)] public float spawnWeight = 1f;

        [Header("Loot Rolls")]
        [Tooltip("Minimum number of item picks when this chest is opened.")]
        [Min(1)] public int minItemRolls = 1;
        [Tooltip("Maximum number of item picks when this chest is opened.")]
        [Min(1)] public int maxItemRolls = 3;

        [Header("Loot Table")]
        [Tooltip("Possible items that can drop from this chest.")]
        public LootEntry[] lootTable;

        /// <summary>
        /// Pick a random item from the loot table using weighted selection.
        /// Returns null if the table is empty or all items are null.
        /// </summary>
        public LootEntry? PickLoot()
        {
            if (lootTable == null || lootTable.Length == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < lootTable.Length; i++)
            {
                if (lootTable[i].item != null)
                    totalWeight += lootTable[i].dropWeight;
            }

            if (totalWeight <= 0f) return null;

            float roll = Random.Range(0f, totalWeight);
            for (int i = 0; i < lootTable.Length; i++)
            {
                if (lootTable[i].item == null) continue;
                roll -= lootTable[i].dropWeight;
                if (roll <= 0f) return lootTable[i];
            }

            // Floating-point fallback
            return lootTable[lootTable.Length - 1];
        }
    }

    /// <summary>
    /// A single entry in a chest's loot table.
    /// Weight is relative (not a 0-1 probability) — normalized at runtime.
    /// </summary>
    [System.Serializable]
    public struct LootEntry
    {
        [Tooltip("The item that can drop.")]
        public WeaponConfig item;

        [Tooltip("Relative drop weight. Higher = more likely compared to other entries.")]
        [Min(0.01f)] public float dropWeight;

        [Tooltip("Minimum copies of this item per pick.")]
        [Min(1)] public int minCount;

        [Tooltip("Maximum copies of this item per pick.")]
        [Min(1)] public int maxCount;
    }
}
