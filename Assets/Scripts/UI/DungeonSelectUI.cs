using System;
using System.Collections.Generic;
using DungeonGame.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Canvas popup that lists available dungeons from DungeonRegistry.
    /// The host picks one, then OnDungeonSelected fires with the chosen config.
    /// Not a NetworkBehaviour — pure local UI. Place on a persistent GameObject in Town
    /// or on the Bootstrap object.
    /// </summary>
    public class DungeonSelectUI : MonoBehaviour
    {
        public static DungeonSelectUI Instance { get; private set; }

        /// <summary>Fired when the host selects a dungeon. Listeners should call TownPlayController.EnterDungeon(config).</summary>
        public event Action<DungeonConfig> OnDungeonSelected;

        private Canvas _canvas;
        private GameObject _panel;
        private readonly List<GameObject> _entryObjects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Show the dungeon selection popup. Builds/rebuilds entries from DungeonRegistry.</summary>
        public void Show()
        {
            EnsureCanvas();
            RebuildEntries();
            _canvas.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Hide the popup and re-lock cursor for gameplay.</summary>
        public void Hide()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public bool IsVisible => _canvas != null && _canvas.gameObject.activeSelf;

        private void SelectDungeon(DungeonConfig config)
        {
            Hide();
            OnDungeonSelected?.Invoke(config);
        }

        // ─── UI Construction ────────────────────────────────────────

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("DungeonSelectCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 8500; // Below pause menu (9000), above gameplay UI
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
            }

            // Dark overlay background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.7f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            // Panel (centered, scrollable area)
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.2f, 0.15f);
            panelRt.anchorMax = new Vector2(0.8f, 0.85f);
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            _panel = panelGo;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "Select Dungeon";
            titleText.font = GetFont();
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.88f);
            titleRt.anchorMax = new Vector2(1f, 0.98f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

            // Cancel button (bottom)
            CreateButton(panelGo.transform, "Cancel", new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.10f), Hide);

            // Start hidden — Show() will enable it
            canvasGo.SetActive(false);
        }

        private void RebuildEntries()
        {
            // Clear existing entries
            foreach (var obj in _entryObjects)
                if (obj != null) Destroy(obj);
            _entryObjects.Clear();

            var dungeons = DungeonRegistry.GetAll();
            if (dungeons == null || dungeons.Count == 0)
            {
                // No configs registered — show a message
                var msgGo = new GameObject("NoConfigsMsg");
                msgGo.transform.SetParent(_panel.transform, false);
                var msgText = msgGo.AddComponent<Text>();
                msgText.text = "No dungeons configured.\nAdd DungeonConfig assets to DungeonRegistry.";
                msgText.font = GetFont();
                msgText.fontSize = 20;
                msgText.color = new Color(1f, 1f, 1f, 0.6f);
                msgText.alignment = TextAnchor.MiddleCenter;
                var msgRt = msgGo.GetComponent<RectTransform>();
                msgRt.anchorMin = new Vector2(0.1f, 0.3f);
                msgRt.anchorMax = new Vector2(0.9f, 0.7f);
                msgRt.offsetMin = msgRt.offsetMax = Vector2.zero;
                _entryObjects.Add(msgGo);
                return;
            }

            // Layout entries vertically in the available space (0.12 to 0.87)
            float usableTop = 0.87f;
            float usableBottom = 0.12f;
            float totalSpace = usableTop - usableBottom;
            int count = dungeons.Count;
            float entryHeight = Mathf.Min(0.14f, totalSpace / count - 0.01f);
            float gap = 0.01f;

            for (int i = 0; i < count; i++)
            {
                var cfg = dungeons[i];
                if (cfg == null) continue;

                float top = usableTop - i * (entryHeight + gap);
                float bottom = top - entryHeight;

                var entryGo = CreateDungeonEntry(_panel.transform, cfg,
                    new Vector2(0.05f, bottom), new Vector2(0.95f, top));
                _entryObjects.Add(entryGo);
            }
        }

        private GameObject CreateDungeonEntry(Transform parent, DungeonConfig cfg, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(cfg.dungeonId + "_Entry");
            go.transform.SetParent(parent, false);

            // Button background
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.35f, 0.4f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Hover highlight
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.3f, 0.5f, 0.55f);
            colors.pressedColor = new Color(0.15f, 0.3f, 0.35f);
            btn.colors = colors;

            var config = cfg; // capture for lambda
            btn.onClick.AddListener(() => SelectDungeon(config));

            // Name + difficulty (left side)
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = cfg.displayName;
            nameText.font = GetFont();
            nameText.fontSize = 24;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.04f, 0.5f);
            nameRt.anchorMax = new Vector2(0.6f, 0.95f);
            nameRt.offsetMin = nameRt.offsetMax = Vector2.zero;

            // Difficulty label (right side)
            var diffGo = new GameObject("Difficulty");
            diffGo.transform.SetParent(go.transform, false);
            var diffText = diffGo.AddComponent<Text>();
            diffText.text = cfg.difficultyLabel;
            diffText.font = GetFont();
            diffText.fontSize = 18;
            diffText.color = GetDifficultyColor(cfg.difficultyLabel);
            diffText.alignment = TextAnchor.MiddleRight;
            var diffRt = diffGo.GetComponent<RectTransform>();
            diffRt.anchorMin = new Vector2(0.6f, 0.5f);
            diffRt.anchorMax = new Vector2(0.96f, 0.95f);
            diffRt.offsetMin = diffRt.offsetMax = Vector2.zero;

            // Description (bottom row, spans full width)
            if (!string.IsNullOrEmpty(cfg.description))
            {
                var descGo = new GameObject("Description");
                descGo.transform.SetParent(go.transform, false);
                var descText = descGo.AddComponent<Text>();
                // Truncate long descriptions
                string desc = cfg.description.Length > 120 ? cfg.description.Substring(0, 117) + "..." : cfg.description;
                descText.text = desc;
                descText.font = GetFont();
                descText.fontSize = 16;
                descText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                descText.alignment = TextAnchor.MiddleLeft;
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Truncate;
                var descRt = descGo.GetComponent<RectTransform>();
                descRt.anchorMin = new Vector2(0.04f, 0.05f);
                descRt.anchorMax = new Vector2(0.96f, 0.5f);
                descRt.offsetMin = descRt.offsetMax = Vector2.zero;
            }

            return go;
        }

        private static Color GetDifficultyColor(string label)
        {
            if (string.IsNullOrEmpty(label)) return Color.white;
            string lower = label.ToLowerInvariant();
            if (lower.Contains("easy")) return new Color(0.4f, 0.9f, 0.4f);
            if (lower.Contains("normal")) return Color.white;
            if (lower.Contains("hard")) return new Color(1f, 0.6f, 0.2f);
            if (lower.Contains("nightmare") || lower.Contains("extreme")) return new Color(1f, 0.2f, 0.2f);
            return new Color(0.9f, 0.9f, 0.5f);
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = GetFont();
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        private static Font GetFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial");
        }
    }
}
