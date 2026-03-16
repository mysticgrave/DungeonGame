using UnityEngine;

namespace DungeonGame.Weapons
{
    /// <summary>
    /// Holdable non-weapon items (torches, potions, shields, etc.).
    /// Inherits from WeaponConfig; use attackType = None for items that don't deal damage.
    /// Create via Assets → Create → DungeonGame → Item Config.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "DungeonGame/Item Config", order = 2)]
    public class ItemConfig : WeaponConfig
    {
        // Empty subclass — use for torches, potions, shields, etc.
        // Set attackType = None for items that don't deal damage.
    }
}
