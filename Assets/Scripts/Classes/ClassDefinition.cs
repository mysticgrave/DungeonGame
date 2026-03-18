using UnityEngine;

namespace DungeonGame.Classes
{
    /// <summary>
    /// ScriptableObject defining a playable class: stats, combat modifiers, and abilities.
    /// Create assets in ScriptableObjects/Classes/ (e.g. Warrior.asset, Mage.asset).
    /// </summary>
    [CreateAssetMenu(fileName = "NewClass", menuName = "DungeonGame/Class Definition", order = 0)]
    public class ClassDefinition : ScriptableObject
    {
        [Tooltip("Unique id used in save data and networking (e.g. warrior, mage).")]
        public string classId = "warrior";

        public string displayName = "Warrior";
        [TextArea(2, 4)]
        public string description = "Melee fighter with high HP.";

        [Header("Base Stats (run start)")]
        [Min(1)] public int baseMaxHp = 12;
        [Min(0.1f)] public float baseMoveSpeed = 5f;
        [Min(0.1f)] public float baseSprintSpeed = 7.5f;

        [Header("Combat Stats")]
        [Tooltip("Damage multiplier applied to all attacks. 1.0 = normal.")]
        [Min(0.1f)] public float baseDamageMultiplier = 1f;
        [Tooltip("Incoming damage reduction. 0.0 = none, 0.15 = 15% less damage taken.")]
        [Range(0f, 0.9f)] public float baseDamageReduction = 0f;

        [Header("Weapon")]
        [Tooltip("Default weapon for this class. WeaponController uses this (or player's equipped unlock) for the run.")]
        public DungeonGame.Weapons.WeaponConfig defaultWeapon;

        [Header("Abilities")]
        [Tooltip("Ability bound to Q key.")]
        public DungeonGame.Abilities.AbilityConfig abilityQ;
        [Tooltip("Ability bound to E key.")]
        public DungeonGame.Abilities.AbilityConfig abilityE;
        [Tooltip("Ability bound to R key.")]
        public DungeonGame.Abilities.AbilityConfig abilityR;

        [Header("Optional")]
        public Sprite icon;
        [Tooltip("Preferred weapon type for default loadout (e.g. melee, ranged).")]
        public string preferredWeaponType = "melee";

        public void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(classId) && !string.IsNullOrWhiteSpace(displayName))
                classId = displayName.ToLowerInvariant().Replace(" ", "_");
        }
    }
}
