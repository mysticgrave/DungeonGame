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
            _agent.stoppingDistance = 0.2f;

            _attackExec.Init(config.attacks, config.chaseSpeed);

            if (_statusFx != null)
                _statusFx.SetImmunities(config.immunities);
        }

        private void Update()
        {
            if (!IsServer) return;
            if (config == null) return;
            if (_sm.IsDead) return;
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

            // Chase
            if (_sm.Current != EnemyState.Chase)
                _sm.TransitionTo(EnemyState.Chase);

            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.speed = config.chaseSpeed * speedMult;
                _agent.isStopped = false;
                _agent.SetDestination(_target.position);
            }
        }

        /// <summary>Call from external systems (traps, player attacks) to ragdoll this enemy.</summary>
        public void Ragdoll(Vector3 impulse)
        {
            if (!IsServer) return;
            if (config != null && !config.canBeRagdolled) return;

            float dur = config != null ? config.ragdollDuration : 2f;
            _sm.TransitionTo(EnemyState.Ragdoll, dur);

            var hipsRb = GetRagdollHips();
            if (hipsRb != null)
                hipsRb.AddForce(impulse, ForceMode.Impulse);
        }

        public void Stun(float duration)
        {
            if (!IsServer) return;
            _sm.TransitionTo(EnemyState.Stunned, duration);
        }

        public void Freeze(float duration)
        {
            if (!IsServer) return;
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
            if (config.attacks != null && config.attacks.Length > 0)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, config.attacks[0].range);
            }
        }
    }
}
