using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Central registry of all available DungeonConfig assets.
    /// Order matters — indexes are synced over the network via SpireRunState.
    /// Place on the Bootstrap GameObject alongside ItemRegistry / ClassRegistry.
    /// </summary>
    public class DungeonRegistry : MonoBehaviour
    {
        private static DungeonRegistry _instance;
        public static DungeonRegistry Instance => _instance;

        [Tooltip("All available dungeon configurations. Order matters — indexes are synced over the network.")]
        [SerializeField] private List<DungeonConfig> dungeons = new();
        private readonly Dictionary<string, DungeonConfig> _byId = new();

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
            for (int i = 0; i < dungeons.Count; i++)
            {
                var cfg = dungeons[i];
                if (cfg != null && !string.IsNullOrEmpty(cfg.dungeonId))
                    _byId[cfg.dungeonId] = cfg;
            }
        }

        /// <summary>Look up a dungeon config by its dungeonId string.</summary>
        public static DungeonConfig Get(string dungeonId)
        {
            if (Instance == null) return null;
            return Instance._byId.TryGetValue(dungeonId ?? "", out var config) ? config : null;
        }

        /// <summary>Get the network-sync index for a config. Returns -1 if not registered.</summary>
        public static int IndexOf(DungeonConfig config)
        {
            if (Instance == null || config == null) return -1;
            for (int i = 0; i < Instance.dungeons.Count; i++)
                if (Instance.dungeons[i] == config) return i;
            return -1;
        }

        /// <summary>Get the config at a given network-sync index.</summary>
        public static DungeonConfig GetByIndex(int index)
        {
            if (Instance == null || index < 0 || index >= Instance.dungeons.Count) return null;
            return Instance.dungeons[index];
        }

        /// <summary>Get all registered dungeon configs.</summary>
        public static IReadOnlyList<DungeonConfig> GetAll()
        {
            if (Instance == null) return System.Array.Empty<DungeonConfig>();
            return Instance.dungeons;
        }

        private void OnValidate()
        {
            Rebuild();
        }
    }
}
