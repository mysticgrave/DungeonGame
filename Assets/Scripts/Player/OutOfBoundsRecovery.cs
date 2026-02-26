using System.Linq;
using DungeonGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Detects when the local player falls below a Y threshold (clipped through geometry)
    /// and teleports them back to the nearest spawn point. Server-authoritative.
    /// Attach to the Player prefab.
    /// </summary>
    public class OutOfBoundsRecovery : NetworkBehaviour
    {
        [Tooltip("If the player falls below this Y, they are considered out of bounds.")]
        [SerializeField] private float killPlaneY = -50f;

        [Tooltip("Seconds between OOB checks (saves performance).")]
        [SerializeField] private float checkInterval = 0.5f;

        private CharacterController _cc;
        private float _nextCheckAt;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Time.time < _nextCheckAt) return;
            _nextCheckAt = Time.time + checkInterval;

            if (transform.position.y > killPlaneY) return;

            Debug.LogWarning($"[OOB] Player {OwnerClientId} fell below kill plane (Y={transform.position.y:F1}). Recovering.");
            RecoverPlayer();
        }

        private void RecoverPlayer()
        {
            Vector3 safePos = FindSafePosition();

            if (_cc != null) _cc.enabled = false;
            transform.position = safePos;
            GroundSnap.SnapTransform(transform, _cc);
            if (_cc != null) _cc.enabled = true;

            RecoverClientRpc(transform.position);
        }

        [ClientRpc]
        private void RecoverClientRpc(Vector3 safePos)
        {
            if (IsServer) return;

            if (_cc != null) _cc.enabled = false;
            transform.position = safePos;
            if (_cc != null) _cc.enabled = true;
        }

        private Vector3 FindSafePosition()
        {
            var spawns = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (spawns != null && spawns.Length > 0)
            {
                var nearest = spawns
                    .Where(s => s != null && s.gameObject.scene == gameObject.scene)
                    .OrderBy(s => Vector3.Distance(s.transform.position, transform.position))
                    .FirstOrDefault();

                if (nearest != null)
                    return nearest.transform.position;
            }

            return Vector3.up * 5f;
        }
    }
}
