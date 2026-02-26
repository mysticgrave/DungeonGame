using DungeonGame.Meta;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Server-only: when all connected players are at 0 HP, end the run as Wipe and return to Town.
    /// Add to the same GameObject as SpireRunState (e.g. RunState prefab).
    /// </summary>
    public class RunWipeDetector : MonoBehaviour
    {
        [SerializeField] private SpireRunState runState;
        [SerializeField] private float checkInterval = 0.5f;

        private float _nextCheck;
        private bool _wipeTriggered;

        private void Start()
        {
            if (runState == null) runState = GetComponent<SpireRunState>();
            if (runState == null) runState = FindFirstObjectByType<SpireRunState>();
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.ConnectedClients.Count == 0) return;
            if (runState == null || _wipeTriggered) return;
            if (Time.time < _nextCheck) return;

            _nextCheck = Time.time + checkInterval;

            bool allDead = true;
            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                var health = player.GetComponent<PlayerHealth>();
                if (health == null) { allDead = false; break; }
                if (health.Hp > 0) { allDead = false; break; }
            }

            if (allDead)
            {
                _wipeTriggered = true;
                Debug.Log("[Run] Wipe: all players dead.");
                runState.EndRunAndReturnToTown(RunOutcome.Wipe);
            }
        }
    }
}
