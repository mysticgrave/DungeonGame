using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonGame.Core
{
    /// <summary>
    /// Marks a transform as a player spawn location. Position so the gizmo feet
    /// sit on the floor — the spawn system will snap the player's CharacterController
    /// bottom to this height automatically.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Order when assigning spawns to players (lower = earlier). Leave 0 for default order.")]
        [SerializeField] private int spawnIndex;

        [Header("Gizmo (matches Player prefab CharacterController)")]
        [Tooltip("CharacterController radius on the player prefab.")]
        [SerializeField] private float capsuleRadius = 0.35f;
        [Tooltip("CharacterController height on the player prefab.")]
        [SerializeField] private float capsuleHeight = 1.8f;

        /// <summary>
        /// Optional order for this spawn when multiple exist. Lower values are used first when cycling.
        /// </summary>
        public int SpawnIndex => spawnIndex;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawCapsuleGizmo(new Color(0.2f, 0.9f, 0.3f, 0.25f), new Color(0.2f, 0.9f, 0.3f, 0.6f));

            // Forward arrow
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);

            // Arrow head
            var tip = transform.position + transform.forward * 1.5f;
            Gizmos.DrawLine(tip, tip - transform.forward * 0.3f + transform.right * 0.15f);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.3f - transform.right * 0.15f);
        }

        private void OnDrawGizmosSelected()
        {
            DrawCapsuleGizmo(new Color(0.2f, 0.9f, 0.3f, 0.15f), new Color(0.2f, 0.9f, 0.3f, 1f));

            Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.15f);
            Handles.DrawSolidDisc(transform.position, Vector3.up, capsuleRadius + 0.3f);
            Handles.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            Handles.Label(transform.position + Vector3.up * (capsuleHeight + 0.3f),
                $"Spawn #{spawnIndex}");
        }

        private void DrawCapsuleGizmo(Color fillColor, Color wireColor)
        {
            float r = capsuleRadius;
            float h = capsuleHeight;
            float halfH = h * 0.5f;

            // Capsule is drawn with feet at transform.position.
            // Center of capsule = transform.position + up * halfH
            Vector3 feetPos = transform.position;
            Vector3 center = feetPos + Vector3.up * halfH;
            Vector3 topSphere = center + Vector3.up * (halfH - r);
            Vector3 botSphere = center - Vector3.up * (halfH - r);

            // Solid fill
            Gizmos.color = fillColor;
            Gizmos.DrawSphere(topSphere, r);
            Gizmos.DrawSphere(botSphere, r);

            // Wire outline
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(topSphere, r);
            Gizmos.DrawWireSphere(botSphere, r);

            // Vertical lines connecting the two hemispheres
            Gizmos.DrawLine(topSphere + Vector3.forward * r, botSphere + Vector3.forward * r);
            Gizmos.DrawLine(topSphere - Vector3.forward * r, botSphere - Vector3.forward * r);
            Gizmos.DrawLine(topSphere + Vector3.right * r, botSphere + Vector3.right * r);
            Gizmos.DrawLine(topSphere - Vector3.right * r, botSphere - Vector3.right * r);

            // Foot disc on the ground
            Handles.color = wireColor;
            Handles.DrawWireDisc(feetPos, Vector3.up, r);
        }
#endif
    }
}
