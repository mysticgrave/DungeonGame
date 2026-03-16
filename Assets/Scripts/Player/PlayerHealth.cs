using System;
using DungeonGame.Combat;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Player HP (does NOT despawn on 0; later can enter Downed).
    /// Server authoritative. Implements IDamageable for consistency.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private int maxHp = 10;

        public int MaxHp => maxHp;
        public int Hp => hpNet.Value;

        /// <summary>Fired server-side after damage is applied.</summary>
        public event Action<DamageInfo> OnDamaged;
        /// <summary>Fired server-side when HP reaches 0.</summary>
        public event Action OnDied;
        /// <summary>Static event fired on ALL clients when any player takes damage (for UI).</summary>
        public static event Action<ulong, DamageInfo> OnAnyPlayerDamaged;

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

        /// <summary>Server-only. Primary damage method with rich metadata.</summary>
        public void TakeDamage(DamageInfo info)
        {
            if (!IsServer) return;
            if (info.Amount <= 0) return;
            if (hpNet.Value <= 0) return;

            hpNet.Value = Mathf.Max(0, hpNet.Value - info.Amount);
            OnDamaged?.Invoke(info);

            // Notify all clients for visual feedback (damage flash, etc.)
            NotifyPlayerHitClientRpc(info.Amount, (int)info.Type, info.IsCrit);

            if (hpNet.Value == 0)
            {
                OnDied?.Invoke();
                Debug.Log($"[PlayerHealth] Player {OwnerClientId} HP=0 (downed system later)");
            }
        }

        /// <summary>Server-only. Backward-compatible int overload.</summary>
        public void TakeDamage(int amount)
        {
            TakeDamage(new DamageInfo(amount));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DamageRpc(int amount)
        {
            TakeDamage(amount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void HealRpc(int amount)
        {
            if (amount <= 0) return;
            if (hpNet.Value <= 0) return;

            hpNet.Value = Mathf.Min(maxHp, hpNet.Value + amount);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyPlayerHitClientRpc(int amount, int damageType, bool isCrit)
        {
            var info = new DamageInfo(amount)
            {
                Type = (DamageType)damageType,
                IsCrit = isCrit
            };
            OnAnyPlayerDamaged?.Invoke(OwnerClientId, info);
        }
    }
}
