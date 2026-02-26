using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Marks a transform as a player spawn location. Add to empty GameObjects in your scene or prefab and position them where players should spawn.
    /// PlayerSpawnSystem uses these when present (and falls back to tag "PlayerSpawn" if none are found).
    /// Optional Spawn Index controls order when cycling through multiple spawns (lower = used first).
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Order when assigning spawns to players (lower = earlier). Leave 0 for default order.")]
        [SerializeField] private int spawnIndex;

        /// <summary>
        /// Optional order for this spawn when multiple exist. Lower values are used first when cycling.
        /// </summary>
        public int SpawnIndex => spawnIndex;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
#endif
    }
}
