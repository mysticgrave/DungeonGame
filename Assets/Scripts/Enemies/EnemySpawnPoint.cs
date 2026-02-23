using UnityEngine;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Marker component placed in room prefabs.
    /// Server spawners can collect these after procedural generation.
    /// </summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        public enum SpawnKind
        {
            Default = 0,
            Ambush = 1,
            RestHorde = 2,
        }

        public SpawnKind kind = SpawnKind.Default;

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
        }
    }
}
