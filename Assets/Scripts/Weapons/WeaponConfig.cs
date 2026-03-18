using UnityEngine;

namespace DungeonGame.Weapons
{
    public enum WeaponAttackType
    {
        None,   // Non-weapon items (torches, potions, etc.) — no combat, use for light/consume/etc.
        Melee,
        Ranged,
        Magic
    }

    public enum ItemGrip
    {
        OneHanded,
        TwoHanded
    }

    /// <summary>
    /// ScriptableObject defining an item/weapon's stats and hand system properties.
    /// Create in ScriptableObjects/Weapons/.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "DungeonGame/Weapon Config", order = 1)]
    public class WeaponConfig : ScriptableObject
    {
        [Tooltip("Display name and id for loadout.")]
        public string weaponId = "sword";

        public string displayName = "Sword";
        [Tooltip("Icon shown in the hand-slot HUD. Optional.")]
        public Sprite icon;
        public WeaponAttackType attackType = WeaponAttackType.Melee;

        [Header("Hand System")]
        [Tooltip("OneHanded = occupies one hand. TwoHanded = occupies both hands.")]
        public ItemGrip grip = ItemGrip.OneHanded;

        [Header("World Item")]
        [Tooltip("Prefab spawned in world when dropped. Must have WorldItem + NetworkObject + Rigidbody.")]
        public GameObject worldPrefab;
        [Tooltip("Visual-only prefab parented to hand bone when held.")]
        public GameObject heldVisualPrefab;

        [Header("Right Hand Held Transform")]
        [Tooltip("Local position offset when held in right hand.")]
        public Vector3 heldPositionOffset;
        [Tooltip("Local euler rotation offset when held in right hand.")]
        public Vector3 heldRotationOffset;
        [Tooltip("Local scale when held in right hand. Defaults to (1,1,1).")]
        public Vector3 heldScale = Vector3.one;

        [Header("Left Hand Held Transform")]
        [Tooltip("Local position offset when held in left hand. If zero, uses right hand values.")]
        public Vector3 leftHeldPositionOffset;
        [Tooltip("Local euler rotation offset when held in left hand. If zero, uses right hand values.")]
        public Vector3 leftHeldRotationOffset;
        [Tooltip("Local scale when held in left hand. If zero, uses right hand values.")]
        public Vector3 leftHeldScale;

        [Header("Damage")]
        [Min(1)] public int damage = 2;
        [Min(0.1f)] public float range = 2.5f;
        [Min(0.01f)] public float cooldown = 0.6f;

        [Header("Heavy Attack")]
        [Tooltip("Damage for charged/heavy attack.")]
        [Min(1)] public int heavyDamage = 5;
        [Tooltip("How long to hold the attack button to trigger a heavy attack.")]
        [Min(0.1f)] public float heavyChargeTime = 0.6f;
        [Tooltip("Cooldown after a heavy attack.")]
        [Min(0.1f)] public float heavyCooldown = 1.2f;
        [Tooltip("Multiplier applied to enemy knockback on heavy hits.")]
        [Min(1f)] public float heavyKnockMultiplier = 2f;
        [Tooltip("Animator trigger for heavy attack.")]
        public string heavyAttackTrigger = "attack_heavy_01";

        [Header("Melee (overlap sphere)")]
        [Tooltip("Radius of sphere in front of player for melee hit.")]
        [Min(0.1f)] public float hitRadius = 0.5f;

        [Header("Ranged / Magic (raycast)")]
        [Tooltip("Layers to hit. -1 = everything.")]
        public LayerMask hitLayers = -1;

        [Header("Animation (Hand System)")]
        [Tooltip("Animator trigger for primary attack/use.")]
        public string primaryAttackTrigger = "attack_sword_01";
        [Tooltip("Animator trigger for secondary action (two-handed RMB).")]
        public string secondaryActionTrigger;
        [Tooltip("If true, disable SwordArm/SwordHand layers during attack (for two-handed full-body anims).")]
        public bool useFullBodyAttack;

        [Header("Throw")]
        public bool canBeThrown = true;
        [Min(1f)] public float throwForce = 12f;
        [Range(0f, 45f)] public float throwUpAngle = 15f;
        [Tooltip("Damage dealt on impact after being thrown. 0 = no damage.")]
        [Min(0)] public int throwDamage;

        [Header("Unlock (meta)")]
        [Tooltip("Gold cost to unlock this weapon in town. 0 = already unlocked / starter.")]
        [Min(0)] public int unlockCostGold = 0;
    }
}
