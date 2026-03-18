using System.Collections.Generic;
using DungeonGame.Combat;
using DungeonGame.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Networked damage zone placed on the ground. Ticks damage to enemies inside.
    /// Must have: NetworkObject, SphereCollider (trigger).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class AbilityZone : NetworkBehaviour
    {
        private int _damagePerTick;
        private float _tickInterval;
        private DamageType _damageType;
        private StatusEffectType _statusEffect;
        private float _statusDuration;
        private ulong _ownerClientId;
        private float _despawnTime;
        private float _nextTickTime;
        private float _radius;
        private bool _initialized;

        /// <summary>
        /// Server-only. Called by AbilityExecutor after spawning.
        /// </summary>
        public void Initialize(int damagePerTick, float tickInterval, DamageType damageType,
            ulong ownerClientId, StatusEffectType statusEffect, float statusDuration,
            float duration, float radius)
        {
            _damagePerTick = damagePerTick;
            _tickInterval = tickInterval;
            _damageType = damageType;
            _ownerClientId = ownerClientId;
            _statusEffect = statusEffect;
            _statusDuration = statusDuration;
            _despawnTime = Time.time + duration;
            _nextTickTime = Time.time + tickInterval;
            _radius = radius;
            _initialized = true;

            // Set collider radius to match
            var sphereCol = GetComponent<SphereCollider>();
            if (sphereCol != null)
                sphereCol.radius = radius;
        }

        private void Update()
        {
            if (!IsServer || !_initialized) return;

            // Despawn check
            if (Time.time >= _despawnTime)
            {
                NetworkObject.Despawn(true);
                return;
            }

            // Tick damage
            if (Time.time >= _nextTickTime)
            {
                _nextTickTime = Time.time + _tickInterval;
                TickDamage();
            }
        }

        private void TickDamage()
        {
            var hits = Physics.OverlapSphere(transform.position, _radius, ~0, QueryTriggerInteraction.Ignore);
            var processed = new HashSet<ulong>();

            foreach (var col in hits)
            {
                var no = col.GetComponentInParent<NetworkObject>();
                if (no == null) continue;
                if (no.IsPlayerObject) continue; // Don't damage players
                if (!processed.Add(no.NetworkObjectId)) continue;

                var health = col.GetComponentInParent<NetworkHealth>();
                if (health == null) continue;

                Vector3 hitPoint = col.ClosestPoint(transform.position);
                Vector3 dir = (col.transform.position - transform.position).normalized;

                var info = new DamageInfo(_damagePerTick)
                {
                    Type = _damageType,
                    AttackerClientId = _ownerClientId,
                    HitPosition = hitPoint,
                };
                health.TakeDamage(info);

                // Status effect per tick
                if (_statusEffect != StatusEffectType.None)
                {
                    var sec = col.GetComponentInParent<StatusEffectController>();
                    if (sec != null)
                        sec.ApplyEffect(_statusEffect, _statusDuration, dir * 2f);
                }
            }
        }
    }
}
