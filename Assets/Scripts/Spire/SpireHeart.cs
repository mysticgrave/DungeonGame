using DungeonGame.Combat;
using DungeonGame.Meta;
using DungeonGame.Run;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Spire
{
    /// <summary>
    /// "Boss" at the top of the Spire. When its NetworkHealth reaches 0, run is Victory and rewards are granted.
    /// Place in the final room; assign runState and ensure it has NetworkObject + NetworkHealth.
    /// </summary>
    [RequireComponent(typeof(NetworkHealth))]
    [RequireComponent(typeof(NetworkObject))]
    public class SpireHeart : NetworkBehaviour
    {
        [SerializeField] private SpireRunState runState;
        [Tooltip("If true, only the server needs to reference runState; it will be found if null.")]
        [SerializeField] private bool findRunStateIfNull = true;

        private NetworkHealth _health;
        private bool _victoryTriggered;

        private void Awake()
        {
            _health = GetComponent<NetworkHealth>();
            if (runState == null && findRunStateIfNull)
                runState = FindFirstObjectByType<SpireRunState>();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.OnDied += OnHeartDied;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnDied -= OnHeartDied;
        }

        private void OnHeartDied()
        {
            if (!IsServer) return;
            if (_victoryTriggered) return;

            _victoryTriggered = true;
            if (runState == null)
                runState = FindFirstObjectByType<SpireRunState>();

            if (runState != null)
            {
                Debug.Log("[Spire] Spire Heart defeated — Victory!");
                runState.EndRunAndReturnToTown(RunOutcome.Victory);
            }
        }
    }
}
