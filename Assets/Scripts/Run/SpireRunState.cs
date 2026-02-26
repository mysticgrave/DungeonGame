using System;
using DungeonGame.Meta;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.Run
{
    /// <summary>
    /// Persistent run state for the current host session.
    /// Handles floor/segment progression, evac, and end-of-run rewards (gold, EXP) then return to Town.
    /// </summary>
    public class SpireRunState : NetworkBehaviour
    {
        [Header("Progression")]
        [SerializeField, Min(1)] private int floorsPerSegment = 5;

        [Header("Scenes")]
        [SerializeField] private string townSceneName = "Town";

        public int FloorsPerSegment => floorsPerSegment;

        public int Floor => FloorNet.Value;
        public int Segment => SegmentFromFloor(FloorNet.Value);
        public int HighestUnlockedSegment => HighestUnlockedSegmentNet.Value;

        public event Action OnChanged;

        private readonly NetworkVariable<int> FloorNet = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> HighestUnlockedSegmentNet = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            FloorNet.OnValueChanged += (_, _) => OnChanged?.Invoke();
            HighestUnlockedSegmentNet.OnValueChanged += (_, _) => OnChanged?.Invoke();

            OnChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            FloorNet.OnValueChanged -= (_, _) => { };
            HighestUnlockedSegmentNet.OnValueChanged -= (_, _) => { };
            base.OnNetworkDespawn();
        }

        public int SegmentFromFloor(int floor)
        {
            if (floorsPerSegment <= 0) return 0;
            if (floor < 0) floor = 0;
            return floor / floorsPerSegment;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void AddFloorRpc(int delta = 1)
        {
            if (delta == 0) return;

            FloorNet.Value = Mathf.Max(0, FloorNet.Value + delta);

            var seg = SegmentFromFloor(FloorNet.Value);
            if (seg > HighestUnlockedSegmentNet.Value)
            {
                HighestUnlockedSegmentNet.Value = seg;
            }

            Debug.Log($"[Run] Floor={FloorNet.Value} Segment={seg} Unlocked={HighestUnlockedSegmentNet.Value}");
        }

        /// <summary>
        /// Server: end the run with the given outcome, grant rewards to all clients, wipe run state, then load Town.
        /// The spire run is reset so the next time they enter the Spire it's a fresh run.
        /// </summary>
        public void EndRunAndReturnToTown(RunOutcome outcome)
        {
            if (!IsServer) return;

            int floorsReached = FloorNet.Value;
            var result = RunRewardsCalculator.Compute(floorsReached, outcome);
            RunEndedClientRpc(result.Gold, result.Exp, (int)result.Outcome);

            // Wipe run state so the next Spire entry is a fresh run (fail/evac/victory = run over).
            FloorNet.Value = 0;
            HighestUnlockedSegmentNet.Value = 0;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null
                && SceneManager.GetActiveScene().name != townSceneName)
            {
                Debug.Log($"[Run] End run outcome={outcome} gold={result.Gold} exp={result.Exp}; wiping run; loading {townSceneName}");
                NetworkManager.Singleton.SceneManager.LoadScene(townSceneName, LoadSceneMode.Single);
            }
        }

        [ClientRpc]
        private void RunEndedClientRpc(int gold, int exp, int outcome)
        {
            string classId = null;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            {
                var pc = nm.LocalClient.PlayerObject.GetComponent<DungeonGame.Classes.PlayerClass>();
                if (pc != null) classId = pc.ClassId;
            }

            var meta = Meta.MetaProgression.Instance;
            if (meta != null)
            {
                meta.ApplyRunReward(gold, exp, classId ?? "");
                meta.SetLastRunResult(gold, exp, (RunOutcome)outcome, classId ?? "");
            }

            Debug.Log($"[Run] Client: applied reward gold={gold} exp={exp} class={classId} outcome={(RunOutcome)outcome}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EvacRpc()
        {
            if (!IsServer) return;
            EndRunAndReturnToTown(RunOutcome.Evac);
        }
    }
}
