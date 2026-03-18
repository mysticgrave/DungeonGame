using DungeonGame.Combat;
using DungeonGame.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// ScriptableObject defining a single ability. Assigned to ClassDefinition ability slots (Q/E/R).
    /// Create via Assets → Create → DungeonGame → Ability Config.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "DungeonGame/Ability Config", order = 3)]
    public class AbilityConfig : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId = "ground_slam";
        public string displayName = "Ground Slam";
        [TextArea(1, 3)]
        public string description = "Slams the ground, damaging nearby enemies.";
        public Sprite icon;

        [Header("Type")]
        public AbilityType abilityType = AbilityType.MeleeAoE;

        [Header("Cast Mode")]
        [Tooltip("Instant = fires on key press. HoldTarget = hold key to show ground reticle, release to cast at location.")]
        public CastMode castMode = CastMode.Instant;

        [Tooltip("Shape of the targeting reticle. Auto picks based on ability type (Ring for MeleeAoE, Circle for AreaZone).")]
        public ReticleShape reticleShape = ReticleShape.Auto;

        [Header("Core Stats")]
        [Min(0)] public int damage = 5;
        [Min(0.1f)] public float cooldown = 8f;
        [Tooltip("Range for projectile travel or AoE center offset.")]
        [Min(0f)] public float range = 4f;
        [Tooltip("Radius for MeleeAoE / AreaZone effects.")]
        [Min(0.1f)] public float radius = 3f;

        [Header("Damage Type")]
        public DamageType damageType = DamageType.Physical;

        [Header("Status Effect on Hit")]
        [Tooltip("Applied to enemies hit. None = no effect.")]
        public StatusEffectType statusEffect = StatusEffectType.None;
        [Min(0f)] public float statusDuration = 3f;
        [Min(0f)] public float statusImpulseForce = 5f;

        [Header("Projectile (Projectile type only)")]
        [Tooltip("Networked projectile prefab (needs NetworkObject + Rigidbody + AbilityProjectile).")]
        public GameObject projectilePrefab;
        [Tooltip("If true, projectile is affected by gravity (arcing shot).")]
        public bool projectileUseGravity = false;
        [Min(1f)] public float projectileSpeed = 15f;
        [Tooltip("Number of projectiles to fire (e.g. Multishot = 3).")]
        [Min(1)] public int projectileCount = 1;
        [Tooltip("Spread angle between multiple projectiles.")]
        [Range(0f, 90f)] public float projectileSpreadAngle = 15f;

        [Header("Self-Buff (SelfBuff type only)")]
        [Min(0)] public int healAmount = 0;
        [Tooltip("Speed multiplier during buff. 0 = no speed change.")]
        [Min(0f)] public float speedBoostMultiplier = 0f;
        [Tooltip("Duration of the buff in seconds.")]
        [Min(0f)] public float buffDuration = 5f;

        [Header("Dash Strike (DashStrike type only)")]
        [Min(1f)] public float dashDistance = 6f;
        [Min(0.05f)] public float dashDuration = 0.3f;

        [Header("Area Zone (AreaZone type only)")]
        [Tooltip("Networked zone prefab (needs NetworkObject + Collider trigger + AbilityZone).")]
        public GameObject zonePrefab;
        [Min(0.1f)] public float zoneDuration = 5f;
        [Min(0)] public int zoneDamagePerTick = 1;
        [Min(0.1f)] public float zoneTickInterval = 1f;

        [Header("Visual / Animation")]
        [Tooltip("VFX prefab spawned at cast point (non-networked, spawned via ClientRpc).")]
        public GameObject vfxPrefab;
        [Tooltip("Animator trigger name to play on cast.")]
        public string animationTrigger = "";
        [Tooltip("Sound effect on cast.")]
        public AudioClip castSound;
    }
}
