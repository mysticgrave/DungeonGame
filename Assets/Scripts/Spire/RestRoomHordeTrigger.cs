using System.Collections.Generic;
using DungeonGame.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Spire
{
    /// <summary>
    /// Server-side rest-room anti-turtle mechanic.
    /// 
    /// When any player stays inside the trigger long enough:
    /// - after warnSeconds: logs a warning
    /// - after spawnSeconds: spawns ONE horde wave (then cooldown)
    /// 
    /// Place this on a GameObject with a Trigger Collider inside a room prefab.
    /// Add EnemySpawnPoint markers of kind RestHorde to control spawn locations.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RestRoomHordeTrigger : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float warnSeconds = 90f;
        [SerializeField] private float spawnSeconds = 120f;
        [SerializeField] private float roomCooldownSeconds = 300f;

        [Header("Wave")]
        [SerializeField] private NetworkObject ghoulPrefab;
        [SerializeField] private int baseCount = 10;
        [SerializeField] private float minSpawnDistanceFromPlayers = 10f;

        private readonly HashSet<ulong> playersInside = new();

        private float enteredAt;
        private bool warned;
        private float cooldownUntil;

        private EnemySpawnPoint[] cachedPoints;

        private void Awake()
        {
            var c = GetComponent<Collider>();
            c.isTrigger = true;
        }

        private void Start()
        {
            // Cache points in this room only.
            cachedPoints = GetComponentsInChildren<EnemySpawnPoint>(true);
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            if (Time.time < cooldownUntil) return;

            if (playersInside.Count == 0)
            {
                enteredAt = 0f;
                warned = false;
                return;
            }

            if (enteredAt <= 0f)
            {
                enteredAt = Time.time;
                warned = false;
            }

            float t = Time.time - enteredAt;
            if (!warned && t >= warnSeconds)
            {
                warned = true;
                Debug.Log("[RestHorde] You feel watched. Something is coming...");
            }

            if (t >= spawnSeconds)
            {
                SpawnWave();
                cooldownUntil = Time.time + roomCooldownSeconds;
                enteredAt = 0f;
                warned = false;
            }
        }

        private void SpawnWave()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            if (ghoulPrefab == null)
            {
                Debug.LogError("[RestHorde] Missing ghoulPrefab on RestRoomHordeTrigger.");
                return;
            }

            // Gather rest-horde spawn points.
            var candidates = new List<Transform>();
            if (cachedPoints != null)
            {
                foreach (var p in cachedPoints)
                {
                    if (p == null) continue;
                    if (p.kind != EnemySpawnPoint.SpawnKind.RestHorde) continue;
                    candidates.Add(p.transform);
                }
            }

            if (candidates.Count == 0)
            {
                // Fallback: spawn around the trigger bounds.
                candidates.Add(transform);
            }

            int spawned = 0;
            for (int i = 0; i < baseCount; i++)
            {
                var pos = PickSpawnPos(candidates);
                var no = Instantiate(ghoulPrefab, pos, Quaternion.identity);
                no.Spawn(true);
                spawned++;
            }

            Debug.Log($"[RestHorde] Spawned wave: {spawned} ghouls");
        }

        private Vector3 PickSpawnPos(List<Transform> candidates)
        {
            var t = candidates[Random.Range(0, candidates.Count)];
            var basePos = t != null ? t.position : transform.position;
            var jitter2 = Random.insideUnitCircle * 1.25f;
            var candidate = basePos + new Vector3(jitter2.x, 0f, jitter2.y);

            // Snap to navmesh if possible.
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var navHit, 6f, UnityEngine.AI.NavMesh.AllAreas))
            {
                candidate = navHit.position;
            }

            // If too close to players, push away a bit.
            if (IsTooCloseToAnyPlayer(candidate))
            {
                // crude push away
                candidate += (Vector3.right + Vector3.forward) * 4f;
            }

            return candidate;
        }

        private bool IsTooCloseToAnyPlayer(Vector3 pos)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;

            foreach (var kvp in nm.ConnectedClients)
            {
                var player = kvp.Value?.PlayerObject;
                if (player == null) continue;

                if (Vector3.Distance(pos, player.transform.position) < minSpawnDistanceFromPlayers)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            var no = other.GetComponentInParent<NetworkObject>();
            if (no == null) return;

            // Player objects are the ones owned by clients.
            if (no.IsPlayerObject)
            {
                playersInside.Add(no.OwnerClientId);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            var no = other.GetComponentInParent<NetworkObject>();
            if (no == null) return;

            if (no.IsPlayerObject)
            {
                playersInside.Remove(no.OwnerClientId);
            }
        }
    }
}
