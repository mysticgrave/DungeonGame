using DungeonGame.Combat;
using DungeonGame.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Abilities
{
    /// <summary>
    /// Networked ability projectile. Deals damage on impact and despawns after lifetime.
    /// Must have: NetworkObject, Rigidbody, Collider (trigger).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class AbilityProjectile : NetworkBehaviour
    {
        private int _damage;
        private DamageType _damageType;
        private StatusEffectType _statusEffect;
        private float _statusDuration;
        private float _statusImpulseForce;
        private ulong _ownerClientId;
        private float _despawnTime;
        private float _graceUntil;
        private bool _initialized;
        private bool _hit;

        /// <summary>
        /// Server-only. Called by AbilityExecutor after spawning.
        /// </summary>
        public void Initialize(int damage, DamageType damageType, ulong ownerClientId,
            StatusEffectType statusEffect, float statusDuration, float statusImpulseForce,
            float lifetime)
        {
            _damage = damage;
            _damageType = damageType;
            _ownerClientId = ownerClientId;
            _statusEffect = statusEffect;
            _statusDuration = statusDuration;
            _statusImpulseForce = statusImpulseForce;
            _despawnTime = Time.time + lifetime;
            _graceUntil = Time.time + 0.15f; // ignore collisions briefly to clear caster
            _initialized = true;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!_initialized) return;

            if (Time.time >= _despawnTime)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || !_initialized || _hit) return;
            if (Time.time < _graceUntil) return;

            // Ignore other projectiles
            if (other.GetComponent<AbilityProjectile>() != null) return;
            if (other.GetComponent<AbilityZone>() != null) return;

            // Ignore the caster
            var no = other.GetComponentInParent<NetworkObject>();
            if (no != null && no.OwnerClientId == _ownerClientId && no.IsPlayerObject) return;

            // Try to damage enemy
            var health = other.GetComponentInParent<NetworkHealth>();
            if (health != null && no != null && !no.IsPlayerObject)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 dir = transform.forward;

                var info = new DamageInfo(_damage)
                {
                    Type = _damageType,
                    AttackerClientId = _ownerClientId,
                    HitPosition = hitPoint,
                    KnockImpulse = dir * 4f + Vector3.up * 1f
                };
                health.TakeDamage(info);

                // Status effect
                if (_statusEffect != StatusEffectType.None)
                {
                    var sec = other.GetComponentInParent<StatusEffectController>();
                    if (sec != null)
                        sec.ApplyEffect(_statusEffect, _statusDuration, dir * _statusImpulseForce);
                }

                // Stagger
                var enemyAi = other.GetComponentInParent<EnemyAI>();
                if (enemyAi != null)
                {
                    if (health.Hp <= 0)
                        enemyAi.ApplyHitImpulse(info.KnockImpulse);
                    else
                        enemyAi.Stagger();
                }

                _hit = true;
                NetworkObject.Despawn(true);
                return;
            }

            // Hit a wall/environment — despawn
            if (health == null && no == null)
            {
                _hit = true;
                NetworkObject.Despawn(true);
            }
        }
    }
}
