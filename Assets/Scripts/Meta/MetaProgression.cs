using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Meta
{
    /// <summary>
    /// Persistent meta progression: gold, per-class EXP (levels), and cosmetics.
    /// Saved per-player on their machine (PlayerPrefs). Persists across sessions and across lobbies —
    /// the same player keeps their gold/EXP/cosmetics no matter who hosts or how many times they quit.
    /// The spire run itself wipes on end (evac/victory/wipe); only meta progression carries over.
    /// </summary>
    public class MetaProgression : MonoBehaviour
    {
        public const string PrefsKeyGold = "DungeonGame_Gold";
        public const string PrefsKeyExpPrefix = "DungeonGame_ClassExp_";
        public const int ExpPerLevel = 100;

        private static MetaProgression _instance;
        public static MetaProgression Instance => _instance;

        private int _gold;
        private readonly Dictionary<string, int> _classExp = new();
        private readonly HashSet<string> _unlockedWeaponIds = new();
        private string _equippedWeaponId;

        public int Gold => _gold;
        public event Action<int> OnGoldChanged;
        public event Action<string, int> OnClassExpChanged;
        public event Action OnWeaponsChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            SaveGold();
            OnGoldChanged?.Invoke(_gold);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0 || _gold < amount) return false;
            _gold -= amount;
            SaveGold();
            OnGoldChanged?.Invoke(_gold);
            return true;
        }

        public void AddClassExp(string classId, int amount)
        {
            if (string.IsNullOrEmpty(classId) || amount <= 0) return;
            if (!_classExp.TryGetValue(classId, out var cur)) cur = 0;
            _classExp[classId] = cur + amount;
            SaveClassExp(classId);
            OnClassExpChanged?.Invoke(classId, _classExp[classId]);
        }

        public int GetClassExp(string classId)
        {
            return _classExp.TryGetValue(classId, out var exp) ? exp : 0;
        }

        /// <summary>
        /// Level from EXP: 1-based (100 exp = level 2).
        /// </summary>
        public int GetClassLevel(string classId)
        {
            int exp = GetClassExp(classId);
            return Mathf.Max(1, 1 + exp / ExpPerLevel);
        }

        /// <summary>
        /// Apply end-of-run rewards. Call from client when RunEndedClientRpc is received; classId = this client's class for the run.
        /// </summary>
        public void ApplyRunReward(int gold, int exp, string classId)
        {
            AddGold(gold);
            if (!string.IsNullOrEmpty(classId))
                AddClassExp(classId, exp);
        }

        /// <summary>
        /// Last run result for the run-summary UI. Set when RunEndedClientRpc runs; cleared when summary is dismissed.
        /// </summary>
        public void SetLastRunResult(int gold, int exp, RunOutcome outcome, string classId)
        {
            _lastRunResult = new LastRunResult
            {
                gold = gold,
                exp = exp,
                outcome = outcome,
                classId = classId ?? "",
                classLevel = string.IsNullOrEmpty(classId) ? 0 : GetClassLevel(classId)
            };
        }

        public LastRunResult? GetLastRunResult() => _lastRunResult;

        public void ClearLastRunResult() => _lastRunResult = null;

        private LastRunResult? _lastRunResult;

        public struct LastRunResult
        {
            public int gold;
            public int exp;
            public RunOutcome outcome;
            public string classId;
            public int classLevel;
        }

        /// <summary>
        /// Class index for the next run (used by class-select UI and applied when player spawns).
        /// </summary>
        public int GetSelectedClassIndex() => _selectedClassIndex;

        public void SetSelectedClassForNextRun(int classIndex)
        {
            _selectedClassIndex = Mathf.Max(-1, classIndex);
            SaveSelectedClass();
        }

        private int _selectedClassIndex = 0;
        private const string PrefsKeySelectedClass = "DungeonGame_SelectedClass";

        private void SaveSelectedClass()
        {
            PlayerPrefs.SetInt(PrefsKeySelectedClass, _selectedClassIndex);
            PlayerPrefs.Save();
        }

        public bool IsWeaponUnlocked(string weaponId) => !string.IsNullOrEmpty(weaponId) && _unlockedWeaponIds.Contains(weaponId);

        public string GetEquippedWeaponId() => _equippedWeaponId;

        public void SetEquippedWeaponId(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) { _equippedWeaponId = null; SaveWeapons(); return; }
            if (!_unlockedWeaponIds.Contains(weaponId)) return;
            _equippedWeaponId = weaponId;
            SaveWeapons();
            OnWeaponsChanged?.Invoke();
        }

        /// <summary>
        /// Unlock a weapon by spending gold. Returns true if unlocked (or already unlocked).
        /// </summary>
        public bool UnlockWeapon(string weaponId, int costGold)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            if (_unlockedWeaponIds.Contains(weaponId)) return true;
            if (costGold > 0 && !SpendGold(costGold)) return false;
            _unlockedWeaponIds.Add(weaponId);
            if (string.IsNullOrEmpty(_equippedWeaponId)) _equippedWeaponId = weaponId;
            SaveWeapons();
            OnWeaponsChanged?.Invoke();
            return true;
        }

        private void Load()
        {
            _gold = PlayerPrefs.GetInt(PrefsKeyGold, 0);
            _classExp.Clear();
            // PlayerPrefs doesn't support arbitrary keys easily for dict; we use known class ids or iterate. For MVP we save each known class.
            // Load known keys: we could save a list of class ids, or use a single JSON. Simple: load by iterating PrefsKeyExpPrefix_* via GetAll? Unity doesn't give GetAll. So we persist differently: one key "DungeonGame_ClassExp" = JSON object of { "warrior": 120, "mage": 50 }. That way one key.
            string json = PlayerPrefs.GetString(PrefsKeyClassExpJson, "{}");
            try
            {
                var wrapper = JsonUtility.FromJson<ClassExpWrapper>(json);
                if (wrapper?.entries != null)
                {
                    foreach (var e in wrapper.entries)
                        _classExp[e.key] = e.value;
                }
            }
            catch
            {
                // ignore
            }

            _unlockedWeaponIds.Clear();
            string weaponsJson = PlayerPrefs.GetString(PrefsKeyWeaponsJson, "{}");
            try
            {
                var w = JsonUtility.FromJson<WeaponsWrapper>(weaponsJson);
                if (w?.unlocked != null) foreach (var id in w.unlocked) _unlockedWeaponIds.Add(id);
                _equippedWeaponId = w?.equipped;
            }
            catch { /* ignore */ }

            _selectedClassIndex = PlayerPrefs.GetInt(PrefsKeySelectedClass, 0);
        }

        private const string PrefsKeyClassExpJson = "DungeonGame_ClassExpJson";
        private const string PrefsKeyWeaponsJson = "DungeonGame_WeaponsJson";

        private void SaveGold()
        {
            PlayerPrefs.SetInt(PrefsKeyGold, _gold);
            PlayerPrefs.Save();
        }

        private void SaveClassExp(string classId)
        {
            var entries = new List<ClassExpEntry>();
            foreach (var kvp in _classExp)
                entries.Add(new ClassExpEntry { key = kvp.Key, value = kvp.Value });
            var wrapper = new ClassExpWrapper { entries = entries };
            PlayerPrefs.SetString(PrefsKeyClassExpJson, JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }

        private void SaveWeapons()
        {
            var list = new List<string>(_unlockedWeaponIds);
            var w = new WeaponsWrapper
            {
                unlocked = list.Count > 0 ? list.ToArray() : System.Array.Empty<string>(),
                equipped = _equippedWeaponId
            };
            PlayerPrefs.SetString(PrefsKeyWeaponsJson, JsonUtility.ToJson(w));
            PlayerPrefs.Save();
        }

        [Serializable]
        private class WeaponsWrapper
        {
            public string[] unlocked;
            public string equipped;
        }

        [Serializable]
        private class ClassExpEntry
        {
            public string key;
            public int value;
        }

        [Serializable]
        private class ClassExpWrapper
        {
            public List<ClassExpEntry> entries;
        }
    }
}
