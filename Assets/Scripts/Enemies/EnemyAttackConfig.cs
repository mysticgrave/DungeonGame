using UnityEngine;

namespace DungeonGame.Enemies
{
    public enum EnemyAttackType
    {
        Melee,
        Ranged,
        AreaOfEffect,
    }

    /// <summary>
    /// Defines a single attack an enemy can perform.
    /// Stored inside EnemyConfig.attacks[].
    /// </summary>
    [System.Serializable]
    public class EnemyAttackConfig
    {
        public string attackName = "Attack";
        public EnemyAttackType type = EnemyAttackType.Melee;

        [Header("Damage")]
        [Min(1)] public int damage = 1;

        [Header("Range & Timing")]
        public float range = 2.3f;
        public float cooldown = 2.5f;

        [Header("Melee")]
        [Tooltip("Speed multiplier during melee lunge.")]
        public float lungeSpeedMultiplier = 2f;
        public float lungeDuration = 0.35f;

        [Header("Ranged")]
        [Tooltip("Projectile prefab (must have NetworkObject). Ignored for melee.")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 15f;

        [Header("AoE")]
        public float aoeRadius = 3f;

        [Header("Status Effect")]
        [Tooltip("Optional status effect applied on hit.")]
        public StatusEffectType appliesEffect = StatusEffectType.None;
        public float effectDuration = 3f;
        [Tooltip("Force applied when the effect is Ragdoll. Direction is computed from attacker to target.")]
        public float effectImpulseForce = 8f;

        [Header("Animation")]
        [Tooltip("Animator trigger name for this attack.")]
        public string animTrigger = "Attack";
    }
}
