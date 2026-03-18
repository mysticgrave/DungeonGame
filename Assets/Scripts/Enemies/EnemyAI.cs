using DungeonGame.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// Data-driven enemy AI. Reads all stats from EnemyConfig.
    /// Server-authoritative: clients only see the NetworkTransform sync.
    /// Replaces per-type AI scripts (GhoulRunnerAI, etc.).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyStateMachine))]
    public class EnemyAI : NetworkBehaviour
    {
        [Header("Config")]
        [Tooltip("Assign in the prefab or set at runtime via Init().")]
        [SerializeField] private EnemyConfig config;

        private NavMeshAgent _agent;
        private EnemyStateMachine _sm;
        private EnemyAttackExecutor _attackExec;
        private StatusEffectController _statusFx;
        private NetworkHealth _health;

        private Transform _target;
        private float _lastStaggerTime = -100f;
        private float _circleDirection = 1f; // 1 = clockwise, -1 = counter-clockwise

        public EnemyConfig Config => config;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            _agent = GetComponent<NavMeshAgent>();
            _sm = GetComponent<EnemyStateMachine>();
            _health = GetComponent<NetworkHealth>();
            _statusFx = GetComponent<StatusEffectController>();

            _attackExec = GetComponent<EnemyAttackExecutor>();
            if (_attackExec == null)
                _attackExec = gameObject.AddComponent<EnemyAttackExecutor>();

            // Randomize circle direction so groups of enemies don't all strafe the same way
            _circleDirection = Random.value > 0.5f ? 1f : -1f;

            if (config != null) ApplyConfig();

            if (_health != null)
                _health.OnDied += HandleDeath;
        }

        public override void OnNetworkDespawn()
        {
            if (_health != null)
                _health.OnDied -= HandleDeath;
            base.OnNetworkDespawn();
        }

        /// <summary>Set config at runtime (e.g. from spawner). Call after Spawn.</summary>
        public void Init(EnemyConfig cfg)
        {
            config = cfg;
            ApplyConfig();
        }

        private void ApplyConfig()
        {
            if (config == null) return;

            _agent.speed = config.chaseSpeed;
            _agent.acceleration = config.acceleration;
            _agent.stoppingDistance = config.preferredDistance;

            _attackExec.Init(config.attacks, config.chaseSpeed);

            if (_statusFx != null)
                _statusFx.SetImmunities(config.immunities);
        }

        private void Update()
        {
            if (!IsServer) return;
            if (config == null) return;
            if (_sm.IsDead) return;

            // Retreat and circle states manage their own movement
            if (_sm.Current == EnemyState.Retreat)
            {
                UpdateRetreat();
                return;
            }

            if (_sm.IsMovementDisabled) return;

            float speedMult = _statusFx != null ? _statusFx.SpeedMultiplier : 1f;
            if (speedMult <= 0f)
            {
                if (_agent.enabled) _agent.isStopped = true;
                return;
            }

            _target = FindNearestPlayer();

            if (_target == null)
            {
                if (_sm.Current == EnemyState.Chase)
                    _sm.TransitionTo(EnemyState.Idle);
                if (_agent.enabled && _agent.hasPath)
                    _agent.ResetPath();
                return;
            }

            float dist = Vector3.Distance(transform.position, _target.position);

            if (dist > config.giveUpRange)
            {
                _sm.TransitionTo(EnemyState.Idle);
                if (_agent.enabled && _agent.hasPath)
                    _agent.ResetPath();
                _target = null;
                return;
            }

            // Try attack first
            if (_attackExec.TryAttack(_target, dist))
                return;

            // Within preferred distance — hold position and face target
            if (dist <= config.preferredDistance)
            {
                if (_sm.Current != EnemyState.Chase)
                    _sm.TransitionTo(EnemyState.Chase);

                FaceTarget();

                if (_agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = true;
                return;
            }

            // Chase to preferred distance
            if (_sm.Current != EnemyState.Chase)
                _sm.TransitionTo(EnemyState.Chase);

            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.speed = config.chaseSpeed * speedMult;
                _agent.isStopped = false;
                _agent.SetDestination(_target.position);
            }
        }

        /// <summary>Called by EnemyAttackExecutor when an attack finishes to trigger post-attack behavior.</summary>
        public void OnAttackFinished()
        {
            if (!IsServer) return;
            if (_sm.IsDead) return;
            if (_target == null) { _target = FindNearestPlayer(); }
            if (_target == null) return;

            bool shouldCircle = Random.value < config.circleChance;
            if (shouldCircle)
            {
                _sm.TransitionTo(EnemyState.Retreat, config.retreatDuration);
                // Circle movement handled in UpdateRetreat
            }
            else
            {
                _sm.TransitionTo(EnemyState.Retreat, config.retreatDuration);
            }
        }

        private void UpdateRetreat()
        {
            if (_target == null) { _target = FindNearestPlayer(); }
            if (_target == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            float speedMult = _statusFx != null ? _statusFx.SpeedMultiplier : 1f;
            if (speedMult <= 0f)
            {
                _agent.isStopped = true;
                return;
            }

            float dist = Vector3.Distance(transform.position, _target.position);
            Vector3 dirToTarget = (_target.position - transform.position).normalized;

            if (dist < config.retreatDistance)
            {
                // Back away from player
                Vector3 retreatDir = -dirToTarget;
                // Add perpendicular component for circle-strafe feel
                Vector3 perpendicular = Vector3.Cross(Vector3.up, dirToTarget) * _circleDirection;
                Vector3 moveDir = (retreatDir + perpendicular * 0.5f).normalized;

                Vector3 retreatPos = transform.position + moveDir * 3f;
                _agent.speed = config.circleSpeed * speedMult;
                _agent.isStopped = false;
                _agent.SetDestination(retreatPos);
            }
            else
            {
                // Far enough, circle-strafe at current distance
                Vector3 perpendicular = Vector3.Cross(Vector3.up, dirToTarget) * _circleDirection;
                Vector3 circlePos = transform.position + perpendicular * 3f;
                _agent.speed = config.circleSpeed * speedMult;
                _agent.isStopped = false;
                _agent.SetDestination(circlePos);
            }

            FaceTarget();
        }

        private void FaceTarget()
        {
            if (_target == null) return;
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    720f * Time.deltaTime);
        }

        /// <summary>
        /// Stagger this enemy (brief interrupt). Respects stagger cooldown to prevent stun-lock.
        /// Call from ItemAttackExecutor when a light attack lands.
        /// </summary>
        public void Stagger()
        {
            if (!IsServer) return;
            if (config == null || !config.canBeStaggered) return;
            if (Time.time < _lastStaggerTime + config.staggerCooldown) return;

            _lastStaggerTime = Time.time;
            _attackExec.CancelPendingAttack();
            _sm.TransitionTo(EnemyState.Staggered, config.staggerDuration);
        }

        /// <summary>Call from external systems (traps, player attacks) to ragdoll this enemy.</summary>
        public void Ragdoll(Vector3 impulse)
        {
            if (!IsServer) return;
            if (config != null && !config.canBeRagdolled) return;

            _attackExec.CancelPendingAttack();
            float dur = config != null ? config.ragdollDuration : 2f;
            _sm.TransitionTo(EnemyState.Ragdoll, dur);
            ApplyHitImpulse(impulse);
        }

        /// <summary>Apply impulse to ragdoll hips. Use when already ragdolling (e.g. on death).</summary>
        public void ApplyHitImpulse(Vector3 impulse)
        {
            if (!IsServer) return;
            float resist = config != null ? config.ragdollResistance : 1f;
            Vector3 scaled = impulse / Mathf.Max(0.1f, resist);
            var hipsRb = GetRagdollHips();
            if (hipsRb != null)
                hipsRb.AddForce(scaled, ForceMode.Impulse);
        }

        public void Stun(float duration)
        {
            if (!IsServer) return;
            _attackExec.CancelPendingAttack();
            _sm.TransitionTo(EnemyState.Stunned, duration);
        }

        public void Freeze(float duration)
        {
            if (!IsServer) return;
            _attackExec.CancelPendingAttack();
            _sm.TransitionTo(EnemyState.Frozen, duration);
        }

        private void HandleDeath()
        {
            _sm.TransitionTo(EnemyState.Dead);
        }

        private Transform FindNearestPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;

            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                float d = Vector3.Distance(transform.position, player.transform.position);
                if (d < bestDist && d <= config.aggroRange)
                {
                    bestDist = d;
                    best = player.transform;
                }
            }

            return best;
        }

        private Rigidbody GetRagdollHips()
        {
            var rbs = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                if (rb.gameObject == gameObject) continue;
                if (rb.GetComponent<CharacterJoint>() == null)
                    return rb;
            }
            return rbs.Length > 1 ? rbs[1] : null;
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null) return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, config.aggroRange);

            // Preferred distance ring
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, config.preferredDistance);

            if (config.attacks != null && config.attacks.Length > 0)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, config.attacks[0].range);
            }
        }
    }
}
