using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Persistent run state for the current host session.
    /// MVP: used to prototype segment progression (5 floors per segment) and evac.
    /// 
    /// Attach to the persistent NetworkManager GameObject.
    /// </summary>
    public class SpireRunState : NetworkBehaviour
    {
        [Header("Progression")]
        [SerializeField, Min(1)] private int floorsPerSegment = 5;

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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void EvacRpc()
        {
            // MVP: evacuation knocks you back one segment (min 0).
            int seg = SegmentFromFloor(FloorNet.Value);
            int knocked = Mathf.Max(0, seg - 1);
            FloorNet.Value = knocked * floorsPerSegment;

            Debug.Log($"[Run] EVAC: returning to segment {knocked} (floor {FloorNet.Value})");
        }
    }
}
