using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Combat
{
    /// <summary>
    /// Networked health component for enemies and destructible objects.
    /// Server-authoritative health; clients read via NetworkVariable.
    /// </summary>
    public class NetworkHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private int maxHp = 2;
        [Tooltip("If true, despawn this object when HP reaches 0.")]
        [SerializeField] private bool despawnOnDeath = true;
        [Tooltip("Seconds to wait before despawning on death. 0 = instant.")]
        [Min(0f)] [SerializeField] private float despawnDelayOnDeath;

        public int MaxHp => maxHp;
        public int Hp => hpNet.Value;

        public event Action<int, int> OnHealthChanged;
        /// <summary>Fired server-side when damage is applied. Carries full DamageInfo.</summary>
        public event Action<DamageInfo> OnDamaged;
        public event Action OnDied;

        /// <summary>
        /// Static event fired on ALL clients when any NetworkHealth takes damage.
        /// Used by FloatingDamageNumber and CombatDebugHUD to catch all hits globally.
        /// </summary>
        public static event Action<Vector3, DamageInfo> OnAnyDamaged;

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

        /// <summary>Server-only. Primary damage method with rich metadata.</summary>
        public void TakeDamage(DamageInfo info)
        {
            if (!IsServer) return;
            if (info.Amount <= 0) return;
            if (hpNet.Value <= 0) return;

            hpNet.Value = Mathf.Max(0, hpNet.Value - info.Amount);
            OnDamaged?.Invoke(info);

            // Notify all clients for visual feedback (floating numbers, effects)
            var pos = info.HitPosition == Vector3.zero ? transform.position : info.HitPosition;
            NotifyHitClientRpc(info.Amount, pos.x, pos.y, pos.z,
                (int)info.Type, info.IsCrit, info.AttackerClientId);

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

        /// <summary>Server-only. Backward-compatible int overload.</summary>
        public void TakeDamage(int amount)
        {
            TakeDamage(new DamageInfo(amount));
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

        [Rpc(SendTo.Everyone)]
        private void NotifyHitClientRpc(int amount, float hx, float hy, float hz,
            int damageType, bool isCrit, ulong attackerClientId)
        {
            var pos = new Vector3(hx, hy, hz);
            var info = new DamageInfo(amount)
            {
                Type = (DamageType)damageType,
                IsCrit = isCrit,
                HitPosition = pos,
                AttackerClientId = attackerClientId
            };
            OnAnyDamaged?.Invoke(pos, info);
        }
    }
}
