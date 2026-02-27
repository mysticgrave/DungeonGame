using UnityEngine;

namespace DungeonGame.Weapons
{
    public enum WeaponAttackType
    {
        Melee,
        Ranged,
        Magic
    }

    /// <summary>
    /// ScriptableObject defining a weapon's stats. Create in ScriptableObjects/Weapons/.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "DungeonGame/Weapon Config", order = 1)]
    public class WeaponConfig : ScriptableObject
    {
        [Tooltip("Display name and id for loadout.")]
        public string weaponId = "sword";

        public string displayName = "Sword";
        public WeaponAttackType attackType = WeaponAttackType.Melee;

        [Header("Damage")]
        [Min(1)] public int damage = 2;
        [Min(0.1f)] public float range = 2.5f;
        [Min(0.01f)] public float cooldown = 0.6f;

        [Header("Melee (overlap sphere)")]
        [Tooltip("Radius of sphere in front of player for melee hit.")]
        [Min(0.1f)] public float hitRadius = 0.5f;

        [Header("Ranged / Magic (raycast)")]
        [Tooltip("Layers to hit. -1 = everything.")]
        public LayerMask hitLayers = -1;

        [Header("Unlock (meta)")]
        [Tooltip("Gold cost to unlock this weapon in town. 0 = already unlocked / starter.")]
        [Min(0)] public int unlockCostGold = 0;
    }
}
