using DungeonGame.Abilities;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.UI
{
    /// <summary>
    /// OnGUI-based HUD showing Q/E/R ability slots with cooldown overlays.
    /// Matches HandSlotUI pattern. Add to the player prefab.
    /// </summary>
    public class AbilityCooldownUI : NetworkBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float slotSize = 48f;
        [SerializeField] private float slotSpacing = 6f;
        [SerializeField] private float bottomMargin = 40f;

        private PlayerAbilityController _controller;
        private GUIStyle _keyStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _cooldownStyle;
        private Texture2D _bgTex;
        private Texture2D _cooldownTex;
        private bool _stylesReady;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            _controller = GetComponent<PlayerAbilityController>();
        }

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.75f));
            _bgTex.Apply();

            _cooldownTex = new Texture2D(1, 1);
            _cooldownTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _cooldownTex.Apply();

            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _cooldownStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            if (_controller == null) return;
            if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsVisible) return;
            InitStyles();

            float totalWidth = slotSize * 3 + slotSpacing * 2;
            float startX = (Screen.width - totalWidth) / 2f;
            float startY = Screen.height - bottomMargin - slotSize;

            DrawAbilitySlot(new Rect(startX, startY, slotSize, slotSize),
                _controller.AbilityQ, _controller.CooldownRemainingQ,
                _controller.GetCooldownTotal(0), "Q");

            DrawAbilitySlot(new Rect(startX + slotSize + slotSpacing, startY, slotSize, slotSize),
                _controller.AbilityE, _controller.CooldownRemainingE,
                _controller.GetCooldownTotal(1), "E");

            DrawAbilitySlot(new Rect(startX + (slotSize + slotSpacing) * 2, startY, slotSize, slotSize),
                _controller.AbilityR, _controller.CooldownRemainingR,
                _controller.GetCooldownTotal(2), "R");
        }

        private void DrawAbilitySlot(Rect rect, AbilityConfig config, float cdRemaining,
            float cdTotal, string keyLabel)
        {
            // Background
            GUI.DrawTexture(rect, _bgTex);

            // Border
            DrawBorder(rect, cdRemaining > 0f ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.55f, 0.2f));

            if (config == null)
            {
                // Empty slot
                GUI.Label(rect, "—", _nameStyle);
            }
            else
            {
                // Icon or name
                if (config.icon != null)
                {
                    Rect iconRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
                    GUI.DrawTexture(iconRect, config.icon.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    Rect nameRect = new Rect(rect.x + 2, rect.y + 8, rect.width - 4, rect.height - 12);
                    GUI.Label(nameRect, config.displayName, _nameStyle);
                }

                // Cooldown overlay
                if (cdRemaining > 0f && cdTotal > 0f)
                {
                    float ratio = cdRemaining / cdTotal;
                    float overlayHeight = rect.height * ratio;
                    Rect overlayRect = new Rect(rect.x, rect.y + rect.height - overlayHeight,
                        rect.width, overlayHeight);
                    GUI.DrawTexture(overlayRect, _cooldownTex);

                    // Cooldown number
                    GUI.Label(rect, cdRemaining.ToString("F1"), _cooldownStyle);
                }
            }

            // Key label above slot
            Rect keyRect = new Rect(rect.x, rect.y - 16, rect.width, 16);
            GUI.Label(keyRect, keyLabel, _keyStyle);
        }

        private void DrawBorder(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;

            Texture2D borderTex = Texture2D.whiteTexture;
            float b = 1f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, b), borderTex);               // top
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - b, rect.width, b), borderTex);         // bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, b, rect.height), borderTex);               // left
            GUI.DrawTexture(new Rect(rect.xMax - b, rect.y, b, rect.height), borderTex);        // right

            GUI.color = prev;
        }
    }
}
