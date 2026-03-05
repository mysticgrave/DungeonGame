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
        [Tooltip("If true, despawn this object when HP reaches 0. If false, object stays (e.g. enemy corpses remain as ragdoll).")]
        [SerializeField] private bool despawnOnDeath = true;
        [Tooltip("Seconds to wait before despawning on death. Only used when despawnOnDeath is true. 0 = instant despawn.")]
        [Min(0f)] [SerializeField] private float despawnDelayOnDeath;

        public int MaxHp => maxHp;
        public int Hp => hpNet.Value;

        public event Action<int, int> OnHealthChanged;
        /// <summary>Fired when this object takes damage (after HP is reduced). Amount is the damage dealt.</summary>
        public event Action<int> OnDamaged;
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
            OnDamaged?.Invoke(amount);

            if (hpNet.Value <= 0 && despawnOnDeath)
            {
                var no = GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned)
                {
                    if (despawnDelayOnDeath > 0f)
                        Invoke(nameof(DespawnSelf), despawnDelayOnDeath);
                    else
                        no.Despawn(true);
                }
            }
        }

        private void DespawnSelf()
        {
            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
                no.Despawn(true);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageRpc(int amount)
        {
            TakeDamage(amount);
        }
    }
}
