using DungeonGame.Combat;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Executes attacks defined in EnemyAttackConfig[]. Server-only.
    /// Call TryAttack() from EnemyAI when a target is in range.
    /// Supports a wind-up telegraph phase before the actual attack.
    /// </summary>
    public class EnemyAttackExecutor : MonoBehaviour
    {
        private EnemyAttackConfig[] _attacks;
        private float[] _nextAttackAt;
        private NavMeshAgent _agent;
        private Animator _animator;
        private EnemyStateMachine _sm;
        private EnemyAI _ai;

        private int _activeAttackIndex = -1;
        private float _lungeUntil;
        private Transform _lungeTarget;
        private bool _lungeDamageApplied;
        private float _baseMoveSpeed;

        // Wind-up state
        private int _pendingAttackIndex = -1;
        private Transform _pendingTarget;
        private float _windUpUntil;

        public void Init(EnemyAttackConfig[] attacks, float baseMoveSpeed)
        {
            _attacks = attacks;
            _baseMoveSpeed = baseMoveSpeed;
            _nextAttackAt = new float[attacks != null ? attacks.Length : 0];
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>(true);
            _sm = GetComponent<EnemyStateMachine>();
            _ai = GetComponent<EnemyAI>();
        }

        /// <summary>
        /// Tries the highest-priority attack that is off cooldown and in range.
        /// Returns true if an attack wind-up was started.
        /// </summary>
        public bool TryAttack(Transform target, float distToTarget)
        {
            if (_attacks == null || _attacks.Length == 0) return false;
            if (_sm != null && _sm.IsMovementDisabled) return false;

            for (int i = 0; i < _attacks.Length; i++)
            {
                var atk = _attacks[i];
                if (atk == null) continue;
                if (Time.time < _nextAttackAt[i]) continue;
                if (distToTarget > atk.range) continue;

                StartWindUp(i, target);
                return true;
            }

            return false;
        }

        /// <summary>Cancel any pending wind-up or active attack. Called when staggered/ragdolled.</summary>
        public void CancelPendingAttack()
        {
            if (_pendingAttackIndex >= 0)
            {
                _pendingAttackIndex = -1;
                _pendingTarget = null;
            }
            if (_activeAttackIndex >= 0)
            {
                if (_agent != null && _agent.enabled)
                    _agent.speed = _baseMoveSpeed;
                _lungeTarget = null;
                _activeAttackIndex = -1;
            }
        }

        private void StartWindUp(int index, Transform target)
        {
            var atk = _attacks[index];
            _nextAttackAt[index] = Time.time + atk.cooldown;

            if (atk.windUpDuration > 0f)
            {
                _pendingAttackIndex = index;
                _pendingTarget = target;
                _windUpUntil = Time.time + atk.windUpDuration;

                if (_animator != null && _animator.runtimeAnimatorController != null
                        && _animator.isActiveAndEnabled && !string.IsNullOrEmpty(atk.windUpAnimTrigger))
                    _animator.SetTrigger(atk.windUpAnimTrigger);

                _sm?.TransitionTo(EnemyState.WindUp, atk.windUpDuration);
            }
            else
            {
                ExecuteAttack(index, target);
            }
        }

        private void ExecuteAttack(int index, Transform target)
        {
            var atk = _attacks[index];
            _activeAttackIndex = index;
            _pendingAttackIndex = -1;
            _pendingTarget = null;

            if (_animator != null && _animator.runtimeAnimatorController != null
                && _animator.isActiveAndEnabled && !string.IsNullOrEmpty(atk.animTrigger))
                _animator.SetTrigger(atk.animTrigger);

            switch (atk.type)
            {
                case EnemyAttackType.Melee:
                    StartMelee(atk, target);
                    break;
                case EnemyAttackType.Ranged:
                    FireProjectile(atk, target);
                    break;
                case EnemyAttackType.AreaOfEffect:
                    DoAoE(atk);
                    break;
            }

            float attackDur = atk.type == EnemyAttackType.Melee ? atk.lungeDuration : 0.5f;
            _sm?.TransitionTo(EnemyState.Attack, attackDur);
        }

        private void StartMelee(EnemyAttackConfig atk, Transform target)
        {
            _lungeUntil = Time.time + atk.lungeDuration;
            _lungeTarget = target;
            _lungeDamageApplied = false;
            if (_agent != null && _agent.enabled)
                _agent.speed = _baseMoveSpeed * atk.lungeSpeedMultiplier;
        }

        private void FireProjectile(EnemyAttackConfig atk, Transform target)
        {
            if (atk.projectilePrefab == null) return;

            var spawnPos = transform.position + Vector3.up * 1.2f;
            var dir = (target.position + Vector3.up * 1f - spawnPos).normalized;

            var go = Object.Instantiate(atk.projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            var no = go.GetComponent<NetworkObject>();
            if (no != null) no.Spawn(true);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = dir * atk.projectileSpeed;
        }

        private void DoAoE(EnemyAttackConfig atk)
        {
            var hits = Physics.OverlapSphere(transform.position, atk.aoeRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
            {
                var damageable = col.GetComponentInParent<IDamageable>();
                var playerHealth = col.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    var info = new DungeonGame.Combat.DamageInfo(atk.damage)
                    {
                        HitPosition = col.transform.position
                    };
                    playerHealth.TakeDamage(info);
                    TryApplyStatusEffect(col.transform, atk);
                }
                else if (damageable != null)
                {
                    damageable.TakeDamage(new DungeonGame.Combat.DamageInfo(atk.damage));
                }
            }
        }

        private void Update()
        {
            // Wind-up phase — wait then execute
            if (_pendingAttackIndex >= 0)
            {
                if (Time.time >= _windUpUntil)
                {
                    int idx = _pendingAttackIndex;
                    Transform tgt = _pendingTarget;
                    ExecuteAttack(idx, tgt);
                }
                return;
            }

            if (_activeAttackIndex < 0) return;

            var atk = _attacks[_activeAttackIndex];
            if (atk.type == EnemyAttackType.Melee)
                UpdateMelee(atk);
        }

        private void UpdateMelee(EnemyAttackConfig atk)
        {
            if (Time.time >= _lungeUntil)
            {
                if (_agent != null && _agent.enabled)
                    _agent.speed = _baseMoveSpeed;
                _lungeTarget = null;
                _activeAttackIndex = -1;
                _ai?.OnAttackFinished();
                return;
            }

            if (_lungeDamageApplied || _lungeTarget == null) return;

            float dist = Vector3.Distance(transform.position, _lungeTarget.position);
            if (dist <= atk.range * 1.3f)
            {
                var playerHealth = _lungeTarget.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    var info = new DungeonGame.Combat.DamageInfo(atk.damage)
                    {
                        HitPosition = _lungeTarget.position
                    };
                    playerHealth.TakeDamage(info);
                    TryApplyStatusEffect(_lungeTarget, atk);
                    _lungeDamageApplied = true;
                }
            }
        }

        private void TryApplyStatusEffect(Transform target, EnemyAttackConfig atk)
        {
            if (atk.appliesEffect == StatusEffectType.None) return;
            var sec = target.GetComponentInParent<StatusEffectController>();
            if (sec == null) return;

            Vector3 impulse = Vector3.zero;
            if (atk.appliesEffect == StatusEffectType.Ragdoll && atk.effectImpulseForce > 0f)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                dir.y = 0.3f;
                impulse = dir.normalized * atk.effectImpulseForce;
            }

            sec.ApplyEffect(atk.appliesEffect, atk.effectDuration, impulse);
        }
    }
}
