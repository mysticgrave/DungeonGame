using System;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Pick-3 / Run Modifiers")]
        [Tooltip("Card pool for Pick-3 before dungeon. Also used to resolve modifier IDs.")]
        [SerializeField] private DungeonCardConfig[] cardPool = Array.Empty<DungeonCardConfig>();

        private readonly List<string> _activeModifierIds = new();

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

        // ─── Dungeon Config ─────────────────────────────────────────

        private readonly NetworkVariable<int> DungeonConfigIndexNet = new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>The DungeonConfig selected for the current run, or null if none (use legacy defaults).</summary>
        public DungeonConfig ActiveDungeonConfig =>
            DungeonConfigIndexNet.Value >= 0 ? DungeonRegistry.GetByIndex(DungeonConfigIndexNet.Value) : null;

        /// <summary>Scene name from the active config, or fallback "Spire_Slice".</summary>
        public string DungeonSceneName =>
            ActiveDungeonConfig != null ? ActiveDungeonConfig.sceneName : "Spire_Slice";

        /// <summary>Server: set the dungeon config for the next run. Call before loading the dungeon scene.</summary>
        public void SetDungeonConfig(DungeonConfig config)
        {
            if (!IsServer) return;
            DungeonConfigIndexNet.Value = config != null ? DungeonRegistry.IndexOf(config) : -1;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            FloorNet.OnValueChanged += (_, _) => OnChanged?.Invoke();
            HighestUnlockedSegmentNet.OnValueChanged += (_, _) => OnChanged?.Invoke();
            DungeonConfigIndexNet.OnValueChanged += (_, _) => OnChanged?.Invoke();

            OnChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            FloorNet.OnValueChanged -= (_, _) => { };
            HighestUnlockedSegmentNet.OnValueChanged -= (_, _) => { };
            DungeonConfigIndexNet.OnValueChanged -= (_, _) => { };
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
            DungeonConfigIndexNet.Value = -1;
            _activeModifierIds.Clear();

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

        // --- Pick-3 / Run modifiers ---

        /// <summary>Shuffled cards from pool for Pick-3 UI. Returns up to count cards.</summary>
        public List<DungeonCardConfig> GetShuffledPool(int count)
        {
            var valid = cardPool != null ? cardPool.Where(c => c != null && c.IsValid()).ToList() : new List<DungeonCardConfig>();
            if (valid.Count == 0) return new List<DungeonCardConfig>();
            var shuffled = valid.OrderBy(_ => UnityEngine.Random.value).Take(count).ToList();
            return shuffled;
        }

        /// <summary>Server: set run modifiers from Pick-3 selection. Pass null to clear/skip.</summary>
        public void SetRunModifiers(List<string> ids)
        {
            if (!IsServer) return;
            _activeModifierIds.Clear();
            if (ids != null) _activeModifierIds.AddRange(ids);
        }

        /// <summary>Active modifier cards for this run (Pick-3 + level-up picks).</summary>
        public List<DungeonCardConfig> GetActiveCards()
        {
            var result = new List<DungeonCardConfig>();
            if (cardPool == null) return result;
            foreach (string id in _activeModifierIds)
            {
                var c = cardPool.FirstOrDefault(x => x != null && x.id == id);
                if (c != null) result.Add(c);
            }
            return result;
        }

        /// <summary>Server: add a level-up card choice to the run.</summary>
        public void AddLevelUpModifier(string id)
        {
            if (!IsServer) return;
            if (string.IsNullOrEmpty(id)) return;
            if (!_activeModifierIds.Contains(id)) _activeModifierIds.Add(id);
        }
    }
}
