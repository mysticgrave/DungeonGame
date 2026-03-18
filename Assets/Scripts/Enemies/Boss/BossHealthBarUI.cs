using DungeonGame.Combat;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Enemies.Boss
{
    /// <summary>
    /// Full-width boss health bar at the top of the screen.
    /// Self-builds via OnGUI — no scene references needed.
    /// Automatically shows/hides based on whether a tracked boss is alive.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        private static BossHealthBarUI _instance;
        public static BossHealthBarUI Instance => _instance;

        private NetworkHealth _bossHealth;
        private string _bossName = "???";
        private float _displayHp;
        private float _displayMaxHp;
        private float _smoothVel;
        private bool _isVisible;
        private float _hideTime;

        private GUIStyle _nameStyle;
        private GUIStyle _hpTextStyle;
        private Texture2D _bgTex;
        private Texture2D _fillTex;
        private Texture2D _damageTex;

        private void Awake()
        {
            _instance = this;
            CreateTextures();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_bgTex != null) Destroy(_bgTex);
            if (_fillTex != null) Destroy(_fillTex);
            if (_damageTex != null) Destroy(_damageTex);
        }

        /// <summary>Begin tracking a boss. Call when the boss fight starts.</summary>
        public void ShowBoss(NetworkHealth health, string bossName)
        {
            _bossHealth = health;
            _bossName = bossName;
            _displayHp = health.Hp;
            _displayMaxHp = health.MaxHp;
            _isVisible = true;

            health.OnHealthChanged += OnBossHealthChanged;
        }

        /// <summary>Stop tracking. Call when boss dies or fight resets.</summary>
        public void HideBoss()
        {
            if (_bossHealth != null)
                _bossHealth.OnHealthChanged -= OnBossHealthChanged;
            _bossHealth = null;
            _hideTime = Time.time;
        }

        private void OnBossHealthChanged(int prev, int cur)
        {
            _displayMaxHp = _bossHealth.MaxHp;
            // Don't snap — we'll smooth it
        }

        private void Update()
        {
            if (_bossHealth == null)
            {
                if (_isVisible && Time.time > _hideTime + 2f)
                    _isVisible = false;
                return;
            }

            // Smooth HP display
            float targetHp = _bossHealth.Hp;
            _displayHp = Mathf.SmoothDamp(_displayHp, targetHp, ref _smoothVel, 0.3f);
            if (Mathf.Abs(_displayHp - targetHp) < 0.5f)
                _displayHp = targetHp;
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            EnsureStyles();

            float sw = Screen.width;
            float barWidth = sw * 0.5f;
            float barHeight = 22f;
            float barX = (sw - barWidth) * 0.5f;
            float barY = 30f;
            float nameY = barY - 26f;

            // Boss name
            GUI.Label(new Rect(0, nameY, sw, 30f), _bossName, _nameStyle);

            // Background
            GUI.DrawTexture(new Rect(barX - 2, barY - 2, barWidth + 4, barHeight + 4), _bgTex);

            // Damage trail (smooth lag behind actual HP)
            float maxHp = Mathf.Max(1f, _displayMaxHp);
            float hpRatio = Mathf.Clamp01(_displayHp / maxHp);
            float actualRatio = _bossHealth != null ? Mathf.Clamp01((float)_bossHealth.Hp / maxHp) : hpRatio;

            // Red damage trail
            if (hpRatio > actualRatio)
            {
                float trailWidth = barWidth * hpRatio;
                GUI.DrawTexture(new Rect(barX, barY, trailWidth, barHeight), _damageTex);
            }

            // Main HP fill
            float fillWidth = barWidth * actualRatio;
            GUI.DrawTexture(new Rect(barX, barY, fillWidth, barHeight), _fillTex);

            // HP text
            if (_bossHealth != null)
            {
                string hpText = $"{_bossHealth.Hp} / {_bossHealth.MaxHp}";
                GUI.Label(new Rect(barX, barY, barWidth, barHeight), hpText, _hpTextStyle);
            }

            // Phase markers (25%, 50%, 75%)
            Color markerColor = new Color(0f, 0f, 0f, 0.5f);
            for (int i = 1; i <= 3; i++)
            {
                float mx = barX + barWidth * (i * 0.25f);
                GUI.DrawTexture(new Rect(mx - 1, barY, 2, barHeight), _bgTex);
            }
        }

        private void CreateTextures()
        {
            _bgTex = MakeTex(new Color(0.1f, 0.1f, 0.12f, 0.85f));
            _fillTex = MakeTex(new Color(0.8f, 0.15f, 0.15f, 1f));
            _damageTex = MakeTex(new Color(0.9f, 0.5f, 0.1f, 0.7f));
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void EnsureStyles()
        {
            if (_nameStyle != null) return;

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _nameStyle.normal.textColor = new Color(0.9f, 0.8f, 1f);

            _hpTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hpTextStyle.normal.textColor = Color.white;
        }
    }
}
