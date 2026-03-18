using DungeonGame.Interaction;
using DungeonGame.Items;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Loot
{
    /// <summary>
    /// A loot chest in the world. Server-authoritative open state.
    /// Players interact with F key (via PlayerInteractor + IInteractable).
    /// When opened, rolls loot from its ChestConfig and scatters WorldItems.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class LootChest : NetworkBehaviour, IInteractable
    {
        [Header("Visuals")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _openTrigger = "Open";

        [Header("Loot Scatter")]
        [Tooltip("Outward horizontal force applied to spawned items.")]
        [SerializeField] private float itemScatterForce = 3f;
        [Tooltip("Upward force applied to spawned items.")]
        [SerializeField] private float itemUpForce = 4f;
        [Tooltip("Height offset above chest position where items spawn.")]
        [SerializeField] private float itemSpawnHeight = 1f;

        private readonly NetworkVariable<bool> _opened = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private ChestConfig _config;

        // ──────────────────── IInteractable ────────────────────

        public string InteractPrompt => _opened.Value ? "" : "Open";

        public bool CanInteract(ulong clientId)
        {
            return !_opened.Value;
        }

        public void Interact(ulong clientId)
        {
            if (!IsServer || _opened.Value) return;

            _opened.Value = true;
            SpawnLoot();

            Debug.Log($"[LootChest] '{(_config != null ? _config.chestName : "Unknown")}' opened by client {clientId}");
        }

        // ──────────────────── Init ────────────────────

        /// <summary>
        /// Called by ChestSpawner after network spawn to assign the config.
        /// </summary>
        public void Init(ChestConfig config)
        {
            _config = config;
        }

        // ──────────────────── Network Lifecycle ────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _opened.OnValueChanged += OnOpenedChanged;

            // Apply current state for late joiners
            ApplyVisualState(_opened.Value);
        }

        public override void OnNetworkDespawn()
        {
            _opened.OnValueChanged -= OnOpenedChanged;
            base.OnNetworkDespawn();
        }

        private void OnOpenedChanged(bool previousValue, bool newValue)
        {
            ApplyVisualState(newValue);
        }

        private void ApplyVisualState(bool opened)
        {
            if (opened && _animator != null)
            {
                _animator.SetTrigger(_openTrigger);
            }
        }

        // ──────────────────── Loot Spawning ────────────────────

        private void SpawnLoot()
        {
            if (!IsServer) return;

            if (_config == null)
            {
                Debug.LogWarning("[LootChest] No ChestConfig assigned — no loot to spawn.");
                return;
            }

            if (_config.lootTable == null || _config.lootTable.Length == 0)
            {
                Debug.LogWarning($"[LootChest] '{_config.chestName}' has an empty loot table.");
                return;
            }

            int rolls = Random.Range(_config.minItemRolls, _config.maxItemRolls + 1);
            int totalItemsSpawned = 0;

            // Pre-calculate how many total items we'll spawn for even fan-out
            int estimatedTotal = 0;
            for (int r = 0; r < rolls; r++) estimatedTotal++;

            for (int r = 0; r < rolls; r++)
            {
                var entry = _config.PickLoot();
                if (entry == null || entry.Value.item == null) continue;

                var loot = entry.Value;
                int count = Random.Range(loot.minCount, loot.maxCount + 1);

                for (int c = 0; c < count; c++)
                {
                    SpawnWorldItem(loot.item, totalItemsSpawned, rolls * count);
                    totalItemsSpawned++;
                }
            }

            Debug.Log($"[LootChest] '{_config.chestName}' spawned {totalItemsSpawned} items.");
        }

        private void SpawnWorldItem(Weapons.WeaponConfig itemConfig, int itemIndex, int totalItems)
        {
            if (itemConfig.worldPrefab == null)
            {
                Debug.LogWarning($"[LootChest] Item '{itemConfig.displayName}' has no worldPrefab assigned.");
                return;
            }

            Vector3 spawnPos = transform.position + Vector3.up * itemSpawnHeight;

            var go = Instantiate(itemConfig.worldPrefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[LootChest] worldPrefab for '{itemConfig.displayName}' is missing NetworkObject!");
                Destroy(go);
                return;
            }

            netObj.Spawn(true);

            // Calculate scatter direction — fan out evenly in a circle
            float angle = (360f / Mathf.Max(totalItems, 1)) * itemIndex;
            angle += Random.Range(-15f, 15f); // slight jitter
            Vector3 scatterDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 velocity = scatterDir * itemScatterForce + Vector3.up * itemUpForce;

            var worldItem = go.GetComponent<WorldItem>();
            if (worldItem != null)
            {
                worldItem.ServerDrop(spawnPos, velocity);
            }
            else
            {
                // Fallback: apply velocity directly to rigidbody
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = velocity;
                }
            }
        }
    }
}
