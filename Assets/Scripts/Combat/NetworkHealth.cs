using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Combat
{
    /// <summary>
    /// Minimal networked health component.
    /// Server-authoritative health; clients read.
    /// </summary>
    public class NetworkHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private int maxHp = 2;

        public int MaxHp => maxHp;
        public int Hp => hpNet.Value;

        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;

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

            hpNet.OnValueChanged += HandleHpChanged;
            HandleHpChanged(0, hpNet.Value);
        }

        public override void OnNetworkDespawn()
        {
            hpNet.OnValueChanged -= HandleHpChanged;
            base.OnNetworkDespawn();
        }

        private void HandleHpChanged(int prev, int cur)
        {
            OnHealthChanged?.Invoke(prev, cur);
            if (cur <= 0)
            {
                OnDied?.Invoke();
            }
        }

        public void TakeDamage(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0) return;
            if (hpNet.Value <= 0) return;

            hpNet.Value = Mathf.Max(0, hpNet.Value - amount);

            if (hpNet.Value <= 0)
            {
                // Default: despawn if this is on a NetworkObject.
                var no = GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned)
                {
                    no.Despawn(true);
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageRpc(int amount)
        {
            TakeDamage(amount);
        }
    }
}
