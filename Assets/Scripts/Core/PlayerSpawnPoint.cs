using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [Tooltip("Radius of the debug disc drawn in the Scene view (editor only).")]
        [SerializeField] private float debugRadius = 0.8f;

        /// <summary>
        /// Optional order for this spawn when multiple exist. Lower values are used first when cycling.
        /// </summary>
        public int SpawnIndex => spawnIndex;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.6f);
            Gizmos.DrawSphere(transform.position, 0.2f);
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, debugRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.15f);
            Handles.DrawSolidDisc(transform.position, Vector3.up, debugRadius);
            Handles.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            Handles.DrawWireDisc(transform.position, Vector3.up, debugRadius);
            Handles.Label(transform.position + Vector3.up * (debugRadius + 0.3f), $"Player Spawn (index {spawnIndex})");
        }
#endif
    }
}
