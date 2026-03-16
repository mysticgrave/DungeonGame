using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using DungeonGame.Combat;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// In-dungeon only EXP: shared across all players, resets when run ends.
    /// Server awards EXP on any player kill to the shared pool. Level-up triggers a card pick (3 random, pick 1).
    /// All players see the same EXP bar; first to pick chooses for the team. Place in dungeon scene.
    /// </summary>
    public class DungeonRunExp : NetworkBehaviour
    {
        [Header("EXP")]
        [Tooltip("EXP awarded per enemy kill (goes to shared pool)")]
        [Min(0)] public int expPerKill = 8;
        [Tooltip("EXP needed for level 2. Each level adds this much more.")]
        [Min(1)] public int expPerLevelBase = 25;
        [Tooltip("Extra EXP needed per level (e.g. 5 = 25, 30, 35, 40...)")]
        [Min(0)] public int expPerLevelIncrement = 5;

        [Header("Card Pool")]
        [Tooltip("Dungeon cards offered on level-up. Shuffled, 3 drawn.")]
        [SerializeField] private DungeonCardConfig[] cardPool = Array.Empty<DungeonCardConfig>();

        public event Action<int, int> OnExpChanged; // shared exp, level
        public event Action<List<DungeonCardConfig>> OnLevelUpRequestCards; // 3 cards to pick from (all players see)
        public event Action OnCardPicked; // someone picked; hide panel for everyone

        private int _runExp;
        private int _runLevel;
        private List<string> _pendingLevelUpCardIds;
        private SpireRunState _runState;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            _runState = FindFirstObjectByType<SpireRunState>();
            NetworkHealth.OnAnyDiedStatic += OnEnemyDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                NetworkHealth.OnAnyDiedStatic -= OnEnemyDied;
            base.OnNetworkDespawn();
        }

        private void OnEnemyDied(NetworkHealth health, ulong killerClientId)
        {
            if (!IsServer) return;

            if (killerClientId == 0) return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.ConnectedClients.ContainsKey(killerClientId)) return;

            _runExp += expPerKill;
            int level = GetLevelForExp(_runExp);
            int prevLevel = _runLevel;
            _runLevel = level;

            ExpChangedClientRpc(_runExp, level);

            if (level > prevLevel)
            {
                var cards = GetRandomCards(3);
                _pendingLevelUpCardIds = cards.Select(c => c.id).ToList();
                var joined = string.Join("|", _pendingLevelUpCardIds);
                LevelUpCardsClientRpc(joined.Length <= 128 ? joined : joined.Substring(0, 128));
            }
        }

        private int GetLevelForExp(int totalExp)
        {
            int lvl = 1;
            int needed = expPerLevelBase;
            while (totalExp >= needed)
            {
                totalExp -= needed;
                lvl++;
                needed += expPerLevelIncrement;
            }
            return lvl;
        }

        public int GetExpToNextLevel(int totalExp)
        {
            int lvl = 1;
            int needed = expPerLevelBase;
            while (totalExp >= needed)
            {
                totalExp -= needed;
                lvl++;
                needed += expPerLevelIncrement;
            }
            return needed;
        }

        private List<DungeonCardConfig> GetRandomCards(int count)
        {
            if (cardPool == null || cardPool.Length == 0) return new List<DungeonCardConfig>();
            var valid = cardPool.Where(c => c != null && c.IsValid()).ToList();
            if (valid.Count == 0) return new List<DungeonCardConfig>();

            var pool = new List<DungeonCardConfig>(valid);
            var result = new List<DungeonCardConfig>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                float total = pool.Sum(c => c.GetPickWeight());
                float roll = UnityEngine.Random.Range(0f, total);
                foreach (var card in pool)
                {
                    roll -= card.GetPickWeight();
                    if (roll <= 0f)
                    {
                        result.Add(card);
                        pool.Remove(card);
                        break;
                    }
                }
            }
            return result;
        }

        [ClientRpc]
        private void ExpChangedClientRpc(int exp, int level)
        {
            OnExpChanged?.Invoke(exp, level);
        }

        [ClientRpc]
        private void LevelUpCardsClientRpc(FixedString128Bytes cardIds)
        {
            var ids = cardIds.ToString().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var cards = new List<DungeonCardConfig>();
            if (cardPool != null)
            {
                foreach (var id in ids)
                {
                    var c = cardPool.FirstOrDefault(x => x != null && x.id == id.Trim());
                    if (c != null) cards.Add(c);
                }
            }
            OnLevelUpRequestCards?.Invoke(cards);
        }

        [ClientRpc]
        private void CardPickedClientRpc()
        {
            OnCardPicked?.Invoke();
        }

        /// <summary>Server: record a player's card choice. First valid pick applies for the whole team.</summary>
        [Rpc(SendTo.Server)]
        public void ChooseCardRpc(FixedString32Bytes chosenModId, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (_pendingLevelUpCardIds == null || _pendingLevelUpCardIds.Count == 0) return;
            string id = chosenModId.ToString().Trim();
            if (!_pendingLevelUpCardIds.Contains(id)) return;
            if (cardPool == null || !cardPool.Any(c => c != null && c.id == id)) return;

            _runState?.AddLevelUpModifier(id);
            _pendingLevelUpCardIds = null;
            CardPickedClientRpc();
        }

        /// <summary>Reset all run EXP when returning to town.</summary>
        public void ResetAll()
        {
            if (!IsServer) return;
            _runExp = 0;
            _runLevel = 0;
            _pendingLevelUpCardIds = null;
        }

        public int GetExp() => _runExp;
        public int GetLevel() => _runLevel;

        public int GetExpInCurrentLevel() => GetExpInCurrentLevel(_runExp);

        /// <summary>Exp within current level. Pass totalExp for client-side use.</summary>
        public int GetExpInCurrentLevel(int totalExp)
        {
            int needed = expPerLevelBase;
            while (totalExp >= needed)
            {
                totalExp -= needed;
                needed += expPerLevelIncrement;
            }
            return totalExp;
        }
    }
}
