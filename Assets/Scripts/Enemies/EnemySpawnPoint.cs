using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Place in room prefabs or scenes to define where enemies spawn, what type, how many,
    /// and whether they respawn. The EnemySpawner (central controller) auto-collects these
    /// and manages the actual spawning/despawning.
    /// </summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        public enum SpawnKind
        {
            Default = 0,
            Ambush = 1,
            RestHorde = 2,
        }

        [Header("Classification")]
        [Tooltip("Category tag so spawners can filter which points they manage.")]
        public SpawnKind kind = SpawnKind.Default;

        [Header("What to Spawn")]
        [Tooltip("Enemy types that can spawn here. If multiple are assigned, one is picked at random per slot.")]
        public EnemyConfig[] enemyPool;

        [Header("How Many")]
        [Tooltip("Number of enemies alive at this point at once.")]
        [Min(1)] public int maxAlive = 1;

        [Header("Respawn")]
        public bool respawnEnabled;
        [Tooltip("Seconds after an enemy dies before a replacement spawns. Ignored if respawnEnabled is false.")]
        [Min(0f)] public float respawnDelay = 15f;
        [Tooltip("Total respawns allowed (0 = unlimited). Only matters if respawnEnabled is true.")]
        [Min(0)] public int maxRespawns = 0;

        [Header("Spawn Radius")]
        [Tooltip("Random jitter radius around this point.")]
        [Min(0f)] public float jitterRadius = 1.5f;

        // --- Runtime state (set by EnemySpawner, not serialized) ---
        [System.NonSerialized] public List<GameObject> alive = new();
        [System.NonSerialized] public int totalRespawnsUsed;
        [System.NonSerialized] public List<float> pendingRespawnTimers = new();

        /// <summary>True when this point can still produce more enemies.</summary>
        public bool CanSpawnMore
        {
            get
            {
                if (enemyPool == null || enemyPool.Length == 0) return false;
                if (alive.Count >= maxAlive && pendingRespawnTimers.Count == 0) return false;
                return true;
            }
        }

        public bool CanRespawn
        {
            get
            {
                if (!respawnEnabled) return false;
                if (maxRespawns > 0 && totalRespawnsUsed >= maxRespawns) return false;
                return true;
            }
        }

        /// <summary>Pick a random enemy config from the pool.</summary>
        public EnemyConfig PickEnemy()
        {
            if (enemyPool == null || enemyPool.Length == 0) return null;
            return enemyPool[Random.Range(0, enemyPool.Length)];
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind switch
            {
                SpawnKind.Ambush => new Color(1f, 0.2f, 0.2f),
                SpawnKind.RestHorde => new Color(0.7f, 0.3f, 1f),
                _ => new Color(0.2f, 1f, 0.2f)
            };

            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);

            if (jitterRadius > 0f)
            {
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.15f);
                Gizmos.DrawWireSphere(transform.position, jitterRadius);
            }
        }
    }
}
