using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Data-driven enemy definition. Create one per enemy type (e.g. "GhoulRunner", "Skeleton Archer").
    /// Referenced by EnemySpawner and EnemyAI at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "DungeonGame/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName = "Enemy";
        [Tooltip("The prefab to spawn. Must have NetworkObject + EnemyAI.")]
        public GameObject prefab;

        [Header("Health")]
        [Min(1)] public int maxHp = 3;

        [Header("Detection")]
        public float aggroRange = 18f;
        public float giveUpRange = 28f;

        [Header("Movement")]
        public float moveSpeed = 4.5f;
        public float chaseSpeed = 6.5f;
        public float acceleration = 40f;

        [Header("Attacks")]
        [Tooltip("Attack definitions this enemy can use. Evaluated in priority order.")]
        public EnemyAttackConfig[] attacks;

        [Header("Ragdoll")]
        [Tooltip("Can this enemy be ragdolled by player knockback?")]
        public bool canBeRagdolled = true;
        public float ragdollDuration = 2f;

        [Header("Status Effects")]
        [Tooltip("Innate immunities — this enemy ignores these status effects.")]
        public StatusEffectType[] immunities;

        [Header("Loot / XP")]
        [Min(0)] public int xpReward = 10;
    }
}
