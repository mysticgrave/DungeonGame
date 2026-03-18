using UnityEngine;

namespace DungeonGame.Loot
{
    /// <summary>
    /// Marker component placed in room prefabs to define where chests can spawn.
    /// The ChestSpawner auto-collects these and handles instantiation.
    /// Analogous to EnemySpawnPoint.
    /// </summary>
    public class ChestSpawnPoint : MonoBehaviour
    {
        [Header("What Can Spawn")]
        [Tooltip("Chest types that can appear here. Selection is weighted by each config's spawnWeight.")]
        public ChestConfig[] chestPool;

        [Header("Spawn Chance")]
        [Tooltip("Probability that any chest at all spawns at this point. 0 = never, 1 = always.")]
        [Range(0f, 1f)] public float spawnChance = 0.7f;

        /// <summary>
        /// Weighted random selection from the chest pool.
        /// Returns null if the pool is empty.
        /// </summary>
        public ChestConfig PickChest()
        {
            if (chestPool == null || chestPool.Length == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < chestPool.Length; i++)
            {
                if (chestPool[i] != null)
                    totalWeight += chestPool[i].spawnWeight;
            }

            if (totalWeight <= 0f) return null;

            float roll = Random.Range(0f, totalWeight);
            for (int i = 0; i < chestPool.Length; i++)
            {
                if (chestPool[i] == null) continue;
                roll -= chestPool[i].spawnWeight;
                if (roll <= 0f) return chestPool[i];
            }

            return chestPool[chestPool.Length - 1];
        }

        private void OnDrawGizmosSelected()
        {
            // Yellow cube to distinguish from enemy spawn spheres
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.6f, 0.6f));

            // Filled cube with alpha based on spawn chance
            Gizmos.color = new Color(1f, 0.85f, 0.2f, spawnChance * 0.3f);
            Gizmos.DrawCube(transform.position, new Vector3(0.6f, 0.6f, 0.6f));

            // Forward direction
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
        }
    }
}
