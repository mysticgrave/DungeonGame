using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Player HP (does NOT despawn on 0; later can enter Downed).
    /// Server authoritative.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        [SerializeField] private int maxHp = 10;

        public int MaxHp => maxHp;
        public int Hp => hpNet.Value;

        private readonly NetworkVariable<int> hpNet = new(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                hpNet.Value = Mathf.Max(1, maxHp);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DamageRpc(int amount)
        {
            if (amount <= 0) return;
            if (hpNet.Value <= 0) return;

            hpNet.Value = Mathf.Max(0, hpNet.Value - amount);

            if (hpNet.Value == 0)
            {
                Debug.Log($"[PlayerHealth] Player {OwnerClientId} HP=0 (downed system later)");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void HealRpc(int amount)
        {
            if (amount <= 0) return;
            if (hpNet.Value <= 0) return;

            hpNet.Value = Mathf.Min(maxHp, hpNet.Value + amount);
        }
    }
}
