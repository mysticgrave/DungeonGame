using DungeonGame.Combat;
using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Server-side: tracks player metrics and updates DungeonMood.
    /// Place in the dungeon scene (Spire_Slice). Resets mood when scene loads.
    /// </summary>
    public class DungeonMoodTracker : MonoBehaviour
    {
        [Header("Thresholds")]
        [Tooltip("Health % below this → Chaotic")]
        [Range(0f, 1f)] public float lowHealthThreshold = 0.35f;
        [Tooltip("Kills per minute above this → Chaotic")]
        public float highKillRateThreshold = 8f;
        [Tooltip("Deaths to trigger Desperate")]
        public int desperateDeathCount = 2;
        [Tooltip("How often to recompute mood (seconds)")]
        public float updateInterval = 2f;

        private float _lastUpdate;
        private float _runStartTime;
        private int _totalKills;
        private int _totalDeaths;
        private float _moodSwingMultiplier = 1f;

        private void Start()
        {
            DungeonMood.Reset();
            _runStartTime = Time.time;
            _lastUpdate = 0f;
            _totalKills = 0;
            _totalDeaths = 0;

            var run = FindFirstObjectByType<SpireRunState>();
            if (run != null)
            {
                foreach (var mod in run.GetActiveCards())
                    _moodSwingMultiplier *= mod.moodSwingMultiplier;
            }
        }

        private void Update()
        {
            if (!NetworkManager.Singleton?.IsServer ?? true) return;

            _lastUpdate += Time.deltaTime;
            if (_lastUpdate < updateInterval) return;
            _lastUpdate = 0f;

            var mood = ComputeMood();
            if (DungeonMood.Current != mood)
                DungeonMood.Set(mood);
        }

        private void OnEnable()
        {
            NetworkHealth.OnAnyDiedStatic += OnEnemyDied;
            PlayerHealth.OnAnyPlayerDied += OnPlayerDied;
        }

        private void OnDisable()
        {
            NetworkHealth.OnAnyDiedStatic -= OnEnemyDied;
            PlayerHealth.OnAnyPlayerDied -= OnPlayerDied;
        }

        private void OnEnemyDied(NetworkHealth _, ulong _1)
        {
            if (!NetworkManager.Singleton?.IsServer ?? true) return;
            _totalKills++;
        }

        private void OnPlayerDied(PlayerHealth _)
        {
            if (!NetworkManager.Singleton?.IsServer ?? true) return;
            _totalDeaths++;
        }

        private DungeonMoodType ComputeMood()
        {
            float runMinutes = (Time.time - _runStartTime) / 60f;
            float killsPerMin = runMinutes > 0.1f ? _totalKills / runMinutes : 0f;

            float lowestHealthPct = 1f;
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                foreach (var kvp in nm.ConnectedClients)
                {
                    var go = kvp.Value?.PlayerObject;
                    if (go == null) continue;
                    var ph = go.GetComponent<PlayerHealth>();
                    if (ph != null && ph.MaxHp > 0)
                    {
                        float pct = (float)ph.Hp / ph.MaxHp;
                        if (pct < lowestHealthPct) lowestHealthPct = pct;
                    }
                }
            }

            // moodSwingMultiplier: >1 = chaos triggers easier, <1 = harder
            float swing = Mathf.Max(0.5f, _moodSwingMultiplier);
            float effectiveLowHealth = lowHealthThreshold / swing;
            float effectiveKillRate = highKillRateThreshold * swing;
            int effectiveDeaths = Mathf.Max(1, Mathf.RoundToInt(desperateDeathCount / swing));

            // Desperate: multiple deaths or very low health
            if (_totalDeaths >= effectiveDeaths)
                return DungeonMoodType.Desperate;
            if (lowestHealthPct < effectiveLowHealth * 0.5f)
                return DungeonMoodType.Desperate;

            // Chaotic: low health or high kill rate
            if (lowestHealthPct < effectiveLowHealth)
                return DungeonMoodType.Chaotic;
            if (killsPerMin >= effectiveKillRate)
                return DungeonMoodType.Chaotic;

            // Calm: high health, low activity
            if (lowestHealthPct > 0.85f && killsPerMin < 2f / swing)
                return DungeonMoodType.Calm;

            return DungeonMoodType.Tense;
        }
    }
}
