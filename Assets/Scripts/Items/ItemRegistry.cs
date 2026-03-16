using System.Collections.Generic;
using DungeonGame.Weapons;
using UnityEngine;

namespace DungeonGame.Items
{
    /// <summary>
    /// Registry of ALL holdable items in the game (weapons, torches, potions, shields, etc.).
    /// Separate from WeaponRegistry which is only for the shop/MetaProgression system.
    /// Populate via inspector with every WeaponConfig that can exist as a WorldItem.
    /// HandSystem uses this for network sync (item identity = index in this list).
    /// </summary>
    public class ItemRegistry : MonoBehaviour
    {
        private static ItemRegistry _instance;
        public static ItemRegistry Instance => _instance;

        [Tooltip("Every holdable item in the game. Order matters — indexes are synced over the network.")]
        [SerializeField] private List<WeaponConfig> items = new();
        private readonly Dictionary<string, WeaponConfig> _byId = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Rebuild()
        {
            _byId.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null && !string.IsNullOrEmpty(item.weaponId))
                    _byId[item.weaponId] = item;
            }
        }

        /// <summary>Look up an item by its weaponId / itemId string.</summary>
        public static WeaponConfig Get(string itemId)
        {
            if (Instance == null) return null;
            return Instance._byId.TryGetValue(itemId ?? "", out var config) ? config : null;
        }

        /// <summary>Get the network-sync index for a config. Returns -1 if not registered.</summary>
        public static int IndexOf(WeaponConfig config)
        {
            if (Instance == null || config == null) return -1;
            for (int i = 0; i < Instance.items.Count; i++)
                if (Instance.items[i] == config) return i;
            return -1;
        }

        /// <summary>Get the config at a given network-sync index.</summary>
        public static WeaponConfig GetByIndex(int index)
        {
            if (Instance == null || index < 0 || index >= Instance.items.Count) return null;
            return Instance.items[index];
        }

        public static IReadOnlyList<WeaponConfig> GetAll()
        {
            if (Instance == null) return System.Array.Empty<WeaponConfig>();
            return Instance.items;
        }

        private void OnValidate()
        {
            Rebuild();
        }
    }
}
