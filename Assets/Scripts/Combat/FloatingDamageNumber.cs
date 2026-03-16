using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Combat
{
    /// <summary>
    /// Renders floating damage numbers in world space using OnGUI.
    /// Place on the player prefab — only runs on the local owner client.
    /// Subscribes to NetworkHealth.OnAnyDamaged (fires on all clients via ClientRpc).
    /// </summary>
    public class FloatingDamageNumber : NetworkBehaviour
    {
        private struct FloatEntry
        {
            public Vector3 WorldPos;
            public int Amount;
            public float SpawnTime;
            public DamageType Type;
            public bool IsCrit;
            public float OffsetX; // slight random horizontal jitter
        }

        private static readonly List<FloatEntry> _entries = new();

        [Header("Settings")]
        [SerializeField] private float lifetime = 1.5f;
        [SerializeField] private float riseSpeed = 60f; // pixels per second
        [SerializeField] private int fontSize = 18;
        [SerializeField] private int critFontSize = 26;

        private GUIStyle _style;
        private GUIStyle _critStyle;
        private bool _isLocalOwner;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _isLocalOwner = IsOwner;

            if (_isLocalOwner)
            {
                NetworkHealth.OnAnyDamaged += HandleAnyDamaged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_isLocalOwner)
            {
                NetworkHealth.OnAnyDamaged -= HandleAnyDamaged;
            }
            base.OnNetworkDespawn();
        }

        private void HandleAnyDamaged(Vector3 worldPos, DamageInfo info)
        {
            _entries.Add(new FloatEntry
            {
                WorldPos = worldPos,
                Amount = info.Amount,
                SpawnTime = Time.time,
                Type = info.Type,
                IsCrit = info.IsCrit,
                OffsetX = Random.Range(-20f, 20f)
            });

            // If the local player dealt this damage, trigger hitstop + camera shake
            if (NetworkManager.Singleton != null &&
                info.AttackerClientId == NetworkManager.Singleton.LocalClientId)
            {
                CombatJuice.OnPlayerDealtHit(info);
            }
        }

        private void OnGUI()
        {
            if (!_isLocalOwner) return;
            if (Event.current.type != EventType.Repaint) return;

            var cam = Camera.main;
            if (cam == null) return;

            EnsureStyles();

            float now = Time.time;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                float age = now - entry.SpawnTime;

                if (age > lifetime)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                float t = age / lifetime; // 0→1

                // Project world position to screen
                Vector3 screenPos = cam.WorldToScreenPoint(entry.WorldPos);
                if (screenPos.z <= 0f) continue; // behind camera

                // Convert to GUI coordinates (Y is flipped)
                float guiX = screenPos.x + entry.OffsetX;
                float guiY = Screen.height - screenPos.y - (age * riseSpeed);

                // Fade alpha
                float alpha = 1f - Mathf.Pow(t, 2f); // quadratic fade

                // Color by damage type
                Color col = GetDamageColor(entry.Type);
                col.a = alpha;

                var style = entry.IsCrit ? _critStyle : _style;
                style.normal.textColor = col;

                // Shadow
                Color shadow = Color.black;
                shadow.a = alpha * 0.7f;
                var shadowStyle = entry.IsCrit ? _critStyle : _style;

                string text = entry.IsCrit ? $"{entry.Amount}!" : entry.Amount.ToString();
                var content = new GUIContent(text);
                var size = style.CalcSize(content);

                // Draw shadow offset
                var shadowRect = new Rect(guiX - size.x * 0.5f + 1f, guiY - size.y * 0.5f + 1f, size.x, size.y);
                var savedColor = shadowStyle.normal.textColor;
                shadowStyle.normal.textColor = shadow;
                GUI.Label(shadowRect, content, shadowStyle);
                shadowStyle.normal.textColor = savedColor;

                // Draw main text
                var rect = new Rect(guiX - size.x * 0.5f, guiY - size.y * 0.5f, size.x, size.y);
                GUI.Label(rect, content, style);
            }
        }

        private void EnsureStyles()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_critStyle == null)
            {
                _critStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = critFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private static Color GetDamageColor(DamageType type)
        {
            return type switch
            {
                DamageType.Fire => new Color(1f, 0.5f, 0f),      // orange
                DamageType.Ice => Color.cyan,
                DamageType.Lightning => Color.yellow,
                DamageType.Poison => Color.green,
                _ => Color.white                                   // Physical
            };
        }
    }
}
