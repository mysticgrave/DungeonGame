using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Weapons
{
    /// <summary>
    /// Resolves weaponId to WeaponConfig. Populate via inspector (same pattern as ClassRegistry).
    /// Used by WeaponController and shop UI to list / resolve weapons.
    /// </summary>
    public class WeaponRegistry : MonoBehaviour
    {
        private static WeaponRegistry _instance;
        public static WeaponRegistry Instance => _instance;

        [SerializeField] private List<WeaponConfig> weapons = new();
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
            foreach (var w in weapons)
            {
                if (w != null && !string.IsNullOrEmpty(w.weaponId))
                    _byId[w.weaponId] = w;
            }
        }

        public static WeaponConfig Get(string weaponId)
        {
            if (Instance == null) return null;
            return Instance._byId.TryGetValue(weaponId ?? "", out var config) ? config : null;
        }

        public static int IndexOf(WeaponConfig config)
        {
            if (Instance == null || config == null) return -1;
            for (int i = 0; i < Instance.weapons.Count; i++)
                if (Instance.weapons[i] == config) return i;
            return -1;
        }

        public static WeaponConfig GetByIndex(int index)
        {
            if (Instance == null || index < 0 || index >= Instance.weapons.Count) return null;
            return Instance.weapons[index];
        }

        public static IReadOnlyList<WeaponConfig> GetAll()
        {
            if (Instance == null) return System.Array.Empty<WeaponConfig>();
            return Instance.weapons;
        }

        private void OnValidate()
        {
            Rebuild();
        }
    }
}
