using System;
using DungeonGame.Core;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonGame.Enemies
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Ragdoll,
        Stunned,
        Frozen,
        Dead,
    }

    /// <summary>
    /// Server-authoritative state machine for enemies.
    /// Manages transitions, timers, and NavMeshAgent control.
    /// Driven by EnemyAI which decides *when* to transition; this class handles
    /// the enter/exit logic for each state.
    /// </summary>
    public class EnemyStateMachine : MonoBehaviour
    {
        public EnemyState Current { get; private set; } = EnemyState.Idle;
        public EnemyState Previous { get; private set; } = EnemyState.Idle;

        public event Action<EnemyState, EnemyState> OnStateChanged;

        public bool IsMovementDisabled =>
            Current is EnemyState.Attack or EnemyState.Ragdoll or EnemyState.Stunned or EnemyState.Frozen or EnemyState.Dead;

        public bool IsDead => Current == EnemyState.Dead;

        private NavMeshAgent _agent;
        private Animator _animator;
        private float _stateTimer;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimDead = Animator.StringToHash("Dead");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>(true);
        }

        public void TransitionTo(EnemyState newState, float duration = -1f)
        {
            if (Current == newState) return;
            if (Current == EnemyState.Dead) return;

            var prev = Current;
            ExitState(prev);
            Previous = prev;
            Current = newState;
            _stateTimer = duration > 0 ? duration : -1f;
            EnterState(newState);
            OnStateChanged?.Invoke(newState, prev);
        }

        private void Update()
        {
            if (_stateTimer > 0f)
            {
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    OnTimerExpired();
                }
            }

            if (_animator != null && _agent != null)
            {
                float speed = _agent.enabled && _agent.hasPath ? _agent.velocity.magnitude : 0f;
                _animator.SetFloat(AnimSpeed, speed);
            }
        }

        private void OnTimerExpired()
        {
            switch (Current)
            {
                case EnemyState.Ragdoll:
                case EnemyState.Stunned:
                case EnemyState.Frozen:
                case EnemyState.Attack:
                    TransitionTo(EnemyState.Idle);
                    break;
            }
        }

        private void EnterState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                case EnemyState.Patrol:
                    EnableNavAgent(true);
                    break;

                case EnemyState.Chase:
                    EnableNavAgent(true);
                    break;

                case EnemyState.Attack:
                    EnableNavAgent(false);
                    break;

                case EnemyState.Ragdoll:
                    EnableNavAgent(false);
                    EnableRagdoll(true);
                    break;

                case EnemyState.Stunned:
                case EnemyState.Frozen:
                    EnableNavAgent(false);
                    break;

                case EnemyState.Dead:
                    EnableNavAgent(false);
                    if (_animator != null)
                        _animator.SetBool(AnimDead, true);
                    break;
            }
        }

        private void ExitState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Ragdoll:
                    EnableRagdoll(false);
                    SnapToGround();
                    break;
            }
        }

        private void EnableNavAgent(bool enable)
        {
            if (_agent == null) return;
            if (enable && !_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
                    transform.position = hit.position;
            }
            _agent.enabled = enable;
            if (enable)
            {
                _agent.isStopped = false;
            }
        }

        private void EnableRagdoll(bool enable)
        {
            var rbs = GetComponentsInChildren<Rigidbody>(true);
            var cols = GetComponentsInChildren<Collider>(true);

            foreach (var rb in rbs)
            {
                if (rb.gameObject == gameObject) continue;
                rb.isKinematic = !enable;
            }

            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                col.enabled = enable;
            }

            if (_animator != null)
                _animator.enabled = !enable;
        }

        private void SnapToGround()
        {
            GroundSnap.SnapTransform(transform);

            if (NavMesh.SamplePosition(transform.position, out var navHit, 3f, NavMesh.AllAreas))
                transform.position = navHit.position;
        }
    }
}
