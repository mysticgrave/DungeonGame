using DungeonGame.Items;
using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.UI
{
    /// <summary>
    /// HUD showing left and right hand items at the bottom of the screen.
    /// Two slots side-by-side: [LMB icon/name] [RMB icon/name]
    /// Displays item icon if available, otherwise text name.
    /// Owner-only — attach to the player prefab alongside HandSystem.
    /// </summary>
    public class HandSlotUI : NetworkBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float slotSize = 64f;
        [SerializeField] private float slotSpacing = 8f;
        [SerializeField] private float bottomMargin = 40f;
        [SerializeField] private float iconPadding = 8f;

        [Header("Colors")]
        [SerializeField] private Color slotBackground = new(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color slotBorderEmpty = new(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color slotBorderFilled = new(1f, 0.85f, 0.4f, 0.7f);
        [SerializeField] private Color labelColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color keyHintColor = new(1f, 1f, 1f, 0.4f);

        private HandSystem _handSystem;
        private GUIStyle _nameStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _gripStyle;
        private Texture2D _bgTex;
        private Texture2D _borderTex;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _handSystem = GetComponent<HandSystem>();
            if (_handSystem == null)
            {
                Debug.LogWarning("[HandSlotUI] No HandSystem found on this GameObject.", this);
                enabled = false;
            }
        }

        private void EnsureStyles()
        {
            if (_nameStyle != null) return;

            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, Color.white);
            _bgTex.Apply();

            _borderTex = Texture2D.whiteTexture;

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
                fontStyle = FontStyle.Bold,
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 9,
            };

            _gripStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 9,
                fontStyle = FontStyle.Italic,
            };
        }

        private void OnGUI()
        {
            if (_handSystem == null) return;

            // Hide during loading screens (OnGUI draws over Canvas elements)
            if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsVisible) return;

            EnsureStyles();

            float totalWidth = slotSize * 2f + slotSpacing;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float startY = Screen.height - slotSize - bottomMargin;

            bool twoHanded = _handSystem.HasTwoHandedItem;
            var leftItem = _handSystem.LeftItem;
            var rightItem = _handSystem.RightItem;

            if (twoHanded)
            {
                // Draw one wide slot spanning both
                var wideRect = new Rect(startX, startY, totalWidth, slotSize);
                DrawSlot(wideRect, leftItem, "LMB / RMB", true);
            }
            else
            {
                var leftRect = new Rect(startX, startY, slotSize, slotSize);
                var rightRect = new Rect(startX + slotSize + slotSpacing, startY, slotSize, slotSize);

                DrawSlot(leftRect, leftItem, "LMB", false);
                DrawSlot(rightRect, rightItem, "RMB", false);
            }
        }

        private void DrawSlot(Rect rect, WeaponConfig item, string keyHint, bool isTwoHanded)
        {
            bool filled = item != null;
            var oldColor = GUI.color;

            // Background
            GUI.color = slotBackground;
            GUI.DrawTexture(rect, _bgTex);

            // Border (2px)
            GUI.color = filled ? slotBorderFilled : slotBorderEmpty;
            float b = 2f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, b), _borderTex);                   // top
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - b, rect.width, b), _borderTex);             // bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, b, rect.height), _borderTex);                   // left
            GUI.DrawTexture(new Rect(rect.xMax - b, rect.y, b, rect.height), _borderTex);            // right

            // Content
            if (filled)
            {
                if (item.icon != null)
                {
                    // Draw sprite icon
                    var iconRect = new Rect(
                        rect.x + iconPadding,
                        rect.y + iconPadding,
                        rect.width - iconPadding * 2f,
                        rect.height - iconPadding * 2f - 14f // leave room for label below
                    );
                    GUI.color = Color.white;
                    GUI.DrawTexture(iconRect, item.icon.texture, ScaleMode.ScaleToFit);

                    // Name below icon
                    var nameRect = new Rect(rect.x, rect.yMax - 16f, rect.width, 14f);
                    GUI.color = labelColor;
                    _nameStyle.fontSize = 9;
                    GUI.Label(nameRect, item.displayName, _nameStyle);
                    _nameStyle.fontSize = 11;
                }
                else
                {
                    // Text-only: item name centered
                    GUI.color = labelColor;
                    GUI.Label(rect, item.displayName, _nameStyle);
                }

                // Grip indicator for two-handed
                if (isTwoHanded)
                {
                    GUI.color = keyHintColor;
                    var gripRect = new Rect(rect.x, rect.y, rect.width, rect.height - 4f);
                    _gripStyle.normal.textColor = keyHintColor;
                    GUI.Label(gripRect, "2H", _gripStyle);
                }
            }
            else
            {
                // Empty slot
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                GUI.Label(rect, "Empty", _nameStyle);
            }

            // Key hint above slot
            GUI.color = keyHintColor;
            var hintRect = new Rect(rect.x, rect.y - 16f, rect.width, 16f);
            _hintStyle.normal.textColor = keyHintColor;
            GUI.Label(hintRect, keyHint, _hintStyle);

            GUI.color = oldColor;
        }
    }
}
