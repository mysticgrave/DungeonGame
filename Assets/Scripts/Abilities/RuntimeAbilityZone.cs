using System.Collections.Generic;
using DungeonGame.Combat;
using DungeonGame.Enemies;
using UnityEngine;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Server-only damage zone that doesn't require a NetworkObject or registered prefab.
    /// Used as a fallback when an AreaZone ability has no zonePrefab assigned.
    /// Created at runtime by AbilityExecutor — handles damage ticks and self-destructs.
    /// </summary>
    public class RuntimeAbilityZone : MonoBehaviour
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
        }

        private void Update()
        {
            if (Time.time >= _despawnTime)
            {
                Destroy(gameObject);
                return;
            }

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
                var no = col.GetComponentInParent<Unity.Netcode.NetworkObject>();
                if (no == null) continue;
                if (no.IsPlayerObject) continue;
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
