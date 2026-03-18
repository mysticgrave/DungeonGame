using DungeonGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.UI
{
    /// <summary>
    /// Player health bar HUD — top-left of screen.
    /// Attach to the Player prefab. Owner-only rendering (each player sees their own bar).
    /// Smooth HP transitions with a trailing damage indicator.
    /// Self-builds via OnGUI — no Canvas or scene references needed.
    /// </summary>
    public class PlayerHealthBarUI : NetworkBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float barWidth = 260f;
        [SerializeField] private float barHeight = 22f;
        [SerializeField] private float marginX = 16f;
        [SerializeField] private float marginY = 16f;

        [Header("Style")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private float trailDelay = 0.6f;
        [SerializeField] private float trailSpeed = 3f;

        private PlayerHealth _health;
        private float _displayRatio = 1f;
        private float _trailRatio = 1f;
        private float _trailVel;
        private float _lastDamageTime;
        private float _damageFlashAlpha;
        private bool _show;

        // Textures
        private Texture2D _bgTex;
        private Texture2D _fillTex;
        private Texture2D _trailTex;
        private Texture2D _borderTex;
        private Texture2D _flashTex;
        private GUIStyle _hpTextStyle;
        private GUIStyle _labelStyle;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _health = GetComponent<PlayerHealth>();
            if (_health == null)
            {
                enabled = false;
                return;
            }

            _show = true;
            _displayRatio = 1f;
            _trailRatio = 1f;
            CreateTextures();
        }

        private void OnDestroy()
        {
            DestroyTex(ref _bgTex);
            DestroyTex(ref _fillTex);
            DestroyTex(ref _trailTex);
            DestroyTex(ref _borderTex);
            DestroyTex(ref _flashTex);
        }

        private void Update()
        {
            if (!_show || _health == null) return;

            float maxHp = Mathf.Max(1, _health.MaxHp);
            float targetRatio = Mathf.Clamp01(_health.Hp / maxHp);

            // Detect damage
            if (targetRatio < _displayRatio - 0.001f)
            {
                _lastDamageTime = Time.time;
                _damageFlashAlpha = 1f;
            }

            // Smooth the main bar
            _displayRatio = Mathf.MoveTowards(_displayRatio, targetRatio, smoothSpeed * Time.deltaTime);

            // Trail bar follows after a delay
            if (Time.time > _lastDamageTime + trailDelay)
            {
                _trailRatio = Mathf.SmoothDamp(_trailRatio, _displayRatio, ref _trailVel, 1f / trailSpeed);
            }
            // Trail should never be below main bar
            if (_trailRatio < _displayRatio)
                _trailRatio = _displayRatio;

            // Fade damage flash
            if (_damageFlashAlpha > 0f)
                _damageFlashAlpha = Mathf.MoveTowards(_damageFlashAlpha, 0f, 2.5f * Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!_show || _health == null) return;

            // Hide during loading screens
            if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsVisible) return;

            EnsureStyles();

            float x = marginX;
            float y = marginY;

            // Background (dark)
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), _bgTex);

            // Trail bar (orange/yellow — shows recent damage)
            float trailW = barWidth * _trailRatio;
            if (trailW > 0.5f)
                GUI.DrawTexture(new Rect(x, y, trailW, barHeight), _trailTex);

            // Fill bar (green → yellow → red based on HP ratio)
            float fillW = barWidth * _displayRatio;
            if (fillW > 0.5f)
            {
                Color fillColor = GetFillColor(_displayRatio);
                _fillTex.SetPixel(0, 0, fillColor);
                _fillTex.Apply();
                GUI.DrawTexture(new Rect(x, y, fillW, barHeight), _fillTex);
            }

            // Damage flash overlay (white flash on hit)
            if (_damageFlashAlpha > 0.01f)
            {
                Color flashCol = new Color(1f, 0.3f, 0.2f, _damageFlashAlpha * 0.4f);
                _flashTex.SetPixel(0, 0, flashCol);
                _flashTex.Apply();
                GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), _flashTex);
            }

            // Border
            DrawBorder(x, y, barWidth, barHeight, 2f);

            // HP text centered on bar
            string hpText = $"{_health.Hp} / {_health.MaxHp}";
            var textRect = new Rect(x, y, barWidth, barHeight);
            GUI.Label(textRect, hpText, _hpTextStyle);

            // "HP" label above bar
            var labelRect = new Rect(x, y - 18f, barWidth, 18f);
            GUI.Label(labelRect, "HP", _labelStyle);
        }

        // ─────────────── Helpers ───────────────

        private static Color GetFillColor(float ratio)
        {
            // Green → Yellow → Red
            if (ratio > 0.5f)
            {
                float t = (ratio - 0.5f) * 2f; // 1 at full, 0 at half
                return Color.Lerp(new Color(0.95f, 0.85f, 0.15f), new Color(0.2f, 0.85f, 0.25f), t);
            }
            else
            {
                float t = ratio * 2f; // 1 at half, 0 at empty
                return Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.95f, 0.85f, 0.15f), t);
            }
        }

        private void DrawBorder(float x, float y, float w, float h, float thickness)
        {
            GUI.DrawTexture(new Rect(x, y, w, thickness), _borderTex);                     // top
            GUI.DrawTexture(new Rect(x, y + h - thickness, w, thickness), _borderTex);     // bottom
            GUI.DrawTexture(new Rect(x, y, thickness, h), _borderTex);                     // left
            GUI.DrawTexture(new Rect(x + w - thickness, y, thickness, h), _borderTex);     // right
        }

        private void CreateTextures()
        {
            _bgTex = MakeTex(new Color(0.08f, 0.08f, 0.1f, 0.85f));
            _fillTex = MakeTex(new Color(0.2f, 0.85f, 0.25f, 1f));
            _trailTex = MakeTex(new Color(0.9f, 0.55f, 0.1f, 0.7f));
            _borderTex = MakeTex(new Color(0.15f, 0.15f, 0.2f, 0.9f));
            _flashTex = MakeTex(new Color(1f, 0.3f, 0.2f, 0.4f));
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null) { Destroy(tex); tex = null; }
        }

        private void EnsureStyles()
        {
            if (_hpTextStyle != null) return;

            _hpTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.85f, 0.8f, 0.9f) }
            };
        }
    }
}
