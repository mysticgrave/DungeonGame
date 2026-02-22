using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DungeonGame.Enemies
{
    /// <summary>
    /// MVP server-authoritative runner ghoul.
    /// - NavMeshAgent driven
    /// - Chases nearest player
    /// - Performs a simple lunge (speed burst) when in range
    /// 
    /// Notes:
    /// - Keep it ugly and scary first; animation can come later.
    /// - Requires a baked navmesh (we'll build one at runtime after layout generation).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GhoulRunnerAI : NetworkBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float aggroRange = 18f;
        [SerializeField] private float giveUpRange = 28f;

        [Header("Movement")]
        [SerializeField] private float chaseSpeed = 6.5f;
        [SerializeField] private float chaseAcceleration = 40f;

        [Header("Lunge")]
        [SerializeField] private float lungeRange = 2.3f;
        [SerializeField] private float lungeSpeed = 12.5f;
        [SerializeField] private float lungeDuration = 0.35f;
        [SerializeField] private float lungeCooldown = 2.5f;

        private NavMeshAgent agent;
        private float lungeUntil;
        private float nextLungeAt;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Server owns AI.
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            agent.speed = chaseSpeed;
            agent.acceleration = chaseAcceleration;
            agent.stoppingDistance = 0.2f;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (agent == null) return;

            var target = FindNearestPlayer();
            if (target == null)
            {
                if (agent.hasPath) agent.ResetPath();
                return;
            }

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > giveUpRange)
            {
                if (agent.hasPath) agent.ResetPath();
                return;
            }

            // Lunge burst
            if (Time.time < lungeUntil)
            {
                agent.speed = lungeSpeed;
            }
            else
            {
                agent.speed = chaseSpeed;
            }

            agent.SetDestination(target.position);

            if (dist <= lungeRange && Time.time >= nextLungeAt)
            {
                lungeUntil = Time.time + lungeDuration;
                nextLungeAt = Time.time + lungeCooldown;
            }
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

                var t = player.transform;
                float d = Vector3.Distance(transform.position, t.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }

            if (best != null && bestDist <= aggroRange) return best;
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, lungeRange);
        }
    }
}
