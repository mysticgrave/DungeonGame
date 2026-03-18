using DungeonGame.Combat;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Enemies.Boss
{
    /// <summary>
    /// Controls the boss arena. Attach to the boss room.
    /// When a player enters the trigger zone, spawns the boss and seals the room.
    /// When the boss dies, unseals the room and optionally spawns rewards.
    /// </summary>
    public class BossArenaController : NetworkBehaviour
    {
        [Header("Boss")]
        [Tooltip("Boss prefab with NetworkObject + LichBossController.")]
        [SerializeField] private GameObject bossPrefab;
        [Tooltip("Where the boss spawns in the arena.")]
        [SerializeField] private Transform bossSpawnPoint;

        [Header("Arena")]
        [Tooltip("Objects to activate when the fight starts (e.g. wall blockers to seal the room).")]
        [SerializeField] private GameObject[] sealObjects;
        [Tooltip("Objects to activate when the boss dies (e.g. loot chest, portal).")]
        [SerializeField] private GameObject[] rewardObjects;

        private bool _bossSpawned;
        private NetworkObject _bossNetObj;
        private NetworkHealth _bossHealth;

        private void Awake()
        {
            // Seal objects start hidden, rewards start hidden
            foreach (var obj in sealObjects)
                if (obj != null) obj.SetActive(false);
            foreach (var obj in rewardObjects)
                if (obj != null) obj.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (_bossSpawned) return;

            // Only trigger for players
            var no = other.GetComponentInParent<NetworkObject>();
            if (no == null || !no.IsPlayerObject) return;

            SpawnBoss();
        }

        private void SpawnBoss()
        {
            if (bossPrefab == null)
            {
                Debug.LogError("[BossArena] No bossPrefab assigned!");
                return;
            }

            _bossSpawned = true;

            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
            Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity;

            var go = Instantiate(bossPrefab, spawnPos, spawnRot);
            _bossNetObj = go.GetComponent<NetworkObject>();
            if (_bossNetObj != null)
                _bossNetObj.Spawn(true);

            _bossHealth = go.GetComponent<NetworkHealth>();
            if (_bossHealth != null)
                _bossHealth.OnDied += OnBossDied;

            SealArenaClientRpc(true);
            Debug.Log("[BossArena] Boss fight started!");
        }

        private void OnBossDied()
        {
            if (_bossHealth != null)
                _bossHealth.OnDied -= OnBossDied;

            SealArenaClientRpc(false);
            ShowRewardsClientRpc();
            Debug.Log("[BossArena] Boss defeated! Arena unsealed.");
        }

        [Rpc(SendTo.Everyone)]
        private void SealArenaClientRpc(bool isSealed)
        {
            foreach (var obj in sealObjects)
                if (obj != null) obj.SetActive(isSealed);
        }

        [Rpc(SendTo.Everyone)]
        private void ShowRewardsClientRpc()
        {
            foreach (var obj in rewardObjects)
                if (obj != null) obj.SetActive(true);
        }
    }
}
