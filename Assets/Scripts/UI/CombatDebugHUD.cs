using System.Collections.Generic;
using DungeonGame.Combat;
using DungeonGame.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.UI
{
    /// <summary>
    /// Developer overlay showing combat stats: last hit, rolling DPS, hit/miss ratio.
    /// Place on the player prefab — only runs on the local owner client.
    /// Toggle with F3 in-game. Also enables ItemAttackExecutor.DebugDraw for attack gizmos.
    /// </summary>
    public class CombatDebugHUD : NetworkBehaviour
    {
        private bool _visible;
        private bool _isLocalOwner;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        // Tracking data
        private int _lastHitDamage;
        private float _lastHitTime;
        private int _hitCount;
        private int _missCount;

        // Rolling DPS window
        private readonly List<DamageEntry> _recentDamage = new();
        private const float DpsWindow = 5f; // seconds

        private struct DamageEntry
        {
            public float Time;
            public int Amount;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _isLocalOwner = IsOwner;

            if (_isLocalOwner)
            {
                NetworkHealth.OnAnyDamaged += HandleAnyDamaged;
                ItemAttackExecutor.OnAttackFired += HandleAttackFired;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_isLocalOwner)
            {
                NetworkHealth.OnAnyDamaged -= HandleAnyDamaged;
                ItemAttackExecutor.OnAttackFired -= HandleAttackFired;
            }
            base.OnNetworkDespawn();
        }

        private void HandleAnyDamaged(Vector3 pos, DamageInfo info)
        {
            if (NetworkManager.Singleton == null) return;
            if (info.AttackerClientId != NetworkManager.Singleton.LocalClientId) return;

            _lastHitDamage = info.Amount;
            _lastHitTime = Time.time;

            _recentDamage.Add(new DamageEntry
            {
                Time = Time.time,
                Amount = info.Amount
            });
        }

        private void HandleAttackFired(bool didHit)
        {
            if (didHit)
                _hitCount++;
            else
                _missCount++;
        }

        private void Update()
        {
            if (!_isLocalOwner) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                _visible = !_visible;
                ItemAttackExecutor.DebugDraw = _visible;
            }

            // Prune old DPS entries
            float cutoff = Time.time - DpsWindow;
            _recentDamage.RemoveAll(e => e.Time < cutoff);
        }

        private void OnGUI()
        {
            if (!_isLocalOwner) return;
            if (!_visible) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();

            float boxW = 220f;
            float boxH = 130f;
            float margin = 10f;
            var boxRect = new Rect(Screen.width - boxW - margin, margin, boxW, boxH);

            GUI.Box(boxRect, GUIContent.none, _boxStyle);

            float x = boxRect.x + 8f;
            float y = boxRect.y + 6f;
            float lineH = 20f;

            GUI.Label(new Rect(x, y, boxW, lineH), "COMBAT DEBUG (F3)", _labelStyle);
            y += lineH + 2f;

            // Last hit
            float timeSinceHit = Time.time - _lastHitTime;
            string lastHitStr = _lastHitDamage > 0
                ? $"{_lastHitDamage} dmg ({timeSinceHit:F1}s ago)"
                : "—";
            GUI.Label(new Rect(x, y, boxW, lineH), $"Last Hit: {lastHitStr}", _labelStyle);
            y += lineH;

            // Rolling DPS
            int totalDmg = 0;
            foreach (var e in _recentDamage)
                totalDmg += e.Amount;
            float dps = _recentDamage.Count > 0 ? totalDmg / DpsWindow : 0f;
            GUI.Label(new Rect(x, y, boxW, lineH), $"DPS (5s): {dps:F1}", _labelStyle);
            y += lineH;

            // Hit/miss
            int totalAttacks = _hitCount + _missCount;
            float hitRate = totalAttacks > 0 ? (float)_hitCount / totalAttacks * 100f : 0f;
            GUI.Label(new Rect(x, y, boxW, lineH),
                $"Hits: {_hitCount}  Miss: {_missCount}  ({hitRate:F0}%)", _labelStyle);
            y += lineH;

            // Total attacks
            GUI.Label(new Rect(x, y, boxW, lineH), $"Total Attacks: {totalAttacks}", _labelStyle);
        }

        private void EnsureStyles()
        {
            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box);
                var bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
                bgTex.Apply();
                _boxStyle.normal.background = bgTex;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal
                };
                _labelStyle.normal.textColor = Color.white;
            }
        }
    }
}
