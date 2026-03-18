using DungeonGame.Abilities;
using DungeonGame.Classes;
using DungeonGame.Meta;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Enhanced class-selection overlay. Shows a card per class with icon, stats, abilities,
    /// and description. Can be opened from Town or from the pause menu via Show()/Hide().
    /// Persists selection through MetaProgression (PlayerPrefs).
    /// </summary>
    public class ClassSelectUI : MonoBehaviour
    {
        public static ClassSelectUI Instance { get; private set; }

        [Header("References (auto-created if null)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform listContainer;
        [SerializeField] private Transform detailContainer;

        [Header("Config")]
        [SerializeField] private bool showOnStart = true;
        [Tooltip("Scenes where the UI is allowed to show. Empty = allow everywhere.")]
        [SerializeField] private string[] allowedScenes = { "Town" };
        [SerializeField] private string panelTitle = "Select Class";

        private int _selectedIndex = -1;
        private int _hoveredIndex = -1;
        private bool _stylesReady;
        private Font _font;

        // Detail panel references
        private Text _detailName;
        private Text _detailDescription;
        private Text _detailStats;
        private Text _detailAbilities;
        private Image _detailIcon;
        private Button _selectButton;
        private Text _selectButtonText;
        private GameObject _dimBg;

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
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var meta = MetaProgression.Instance;
            _selectedIndex = meta != null ? meta.GetSelectedClassIndex() : 0;

            if (canvas == null) canvas = GetComponentInChildren<Canvas>();
            if (canvas == null || panel == null) BuildUI();

            RefreshClassList();
            ShowDetail(_selectedIndex);

            if ((!showOnStart || !IsAllowedInCurrentScene()) && panel != null)
            {
                panel.SetActive(false);
                if (_dimBg != null) _dimBg.SetActive(false);
            }
        }

        private bool IsAllowedInCurrentScene()
        {
            if (allowedScenes == null || allowedScenes.Length == 0) return true;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            foreach (var s in allowedScenes)
                if (s == scene) return true;
            return false;
        }

        // ────────────── Public API ──────────────

        public void Show()
        {
            if (panel == null) return;
            var meta = MetaProgression.Instance;
            _selectedIndex = meta != null ? meta.GetSelectedClassIndex() : 0;
            RefreshClassList();
            ShowDetail(_selectedIndex);
            if (_dimBg != null) _dimBg.SetActive(true);
            panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (_dimBg != null) _dimBg.SetActive(false);
            if (panel != null) panel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public bool IsVisible => panel != null && panel.activeSelf;

        // ────────────── UI Construction ──────────────

        private void BuildUI()
        {
            // Canvas
            var canvasGo = new GameObject("ClassSelectCanvas");
            canvasGo.transform.SetParent(transform);
            var c = canvasGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 8500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvas = c;

            if (EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
            }

            // Full-screen dim background
            var dimGo = new GameObject("DimBg");
            dimGo.transform.SetParent(canvas.transform, false);
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            dimImg.raycastTarget = true;
            var dimRect = dimGo.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            // Click dim to close
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Hide);
            _dimBg = dimGo;

            // Main panel
            var panelGo = new GameObject("ClassSelectPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.08f);
            panelRect.anchorMax = new Vector2(0.9f, 0.92f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel = panelGo;

            // Title bar
            var titleGo = MakeText(panelGo.transform, "Title", panelTitle,
                new Vector2(0f, 0.92f), new Vector2(1f, 1f), 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleGo.GetComponent<Text>().color = new Color(1f, 0.85f, 0.2f);

            // Close button (top right)
            var closeGo = new GameObject("CloseBtn");
            closeGo.transform.SetParent(panelGo.transform, false);
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(Hide);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.94f, 0.93f);
            closeRect.anchorMax = new Vector2(0.99f, 0.99f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            MakeText(closeGo.transform, "X", "X",
                Vector2.zero, Vector2.one, 16, FontStyle.Bold, TextAnchor.MiddleCenter);

            // Left side: class list (scrollable)
            var listPanel = new GameObject("ClassList");
            listPanel.transform.SetParent(panelGo.transform, false);
            var listImg = listPanel.AddComponent<Image>();
            listImg.color = new Color(0.06f, 0.06f, 0.1f, 0.8f);
            var listRect = listPanel.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.01f, 0.02f);
            listRect.anchorMax = new Vector2(0.32f, 0.91f);
            listRect.offsetMin = listRect.offsetMax = Vector2.zero;

            var listContent = new GameObject("ListContent");
            listContent.transform.SetParent(listPanel.transform, false);
            var vlg = listContent.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var listContentRect = listContent.GetComponent<RectTransform>();
            listContentRect.anchorMin = Vector2.zero;
            listContentRect.anchorMax = Vector2.one;
            listContentRect.offsetMin = listContentRect.offsetMax = Vector2.zero;
            listContainer = listContent.transform;

            // Right side: detail panel
            var detailPanel = new GameObject("DetailPanel");
            detailPanel.transform.SetParent(panelGo.transform, false);
            var detailImg = detailPanel.AddComponent<Image>();
            detailImg.color = new Color(0.1f, 0.1f, 0.14f, 0.8f);
            var detailRect = detailPanel.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.34f, 0.02f);
            detailRect.anchorMax = new Vector2(0.99f, 0.91f);
            detailRect.offsetMin = detailRect.offsetMax = Vector2.zero;
            detailContainer = detailPanel.transform;

            // Detail: Icon
            var iconGo = new GameObject("ClassIcon");
            iconGo.transform.SetParent(detailPanel.transform, false);
            _detailIcon = iconGo.AddComponent<Image>();
            _detailIcon.color = new Color(0.2f, 0.2f, 0.25f);
            _detailIcon.preserveAspect = true;
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.02f, 0.7f);
            iconRect.anchorMax = new Vector2(0.18f, 0.96f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;

            // Detail: Class name
            var nameGo = MakeText(detailPanel.transform, "ClassName", "",
                new Vector2(0.2f, 0.85f), new Vector2(0.98f, 0.96f), 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            _detailName = nameGo.GetComponent<Text>();
            _detailName.color = new Color(1f, 0.9f, 0.3f);

            // Detail: Description
            var descGo = MakeText(detailPanel.transform, "Description", "",
                new Vector2(0.2f, 0.7f), new Vector2(0.98f, 0.85f), 14, FontStyle.Italic, TextAnchor.UpperLeft);
            _detailDescription = descGo.GetComponent<Text>();
            _detailDescription.color = new Color(0.8f, 0.8f, 0.8f);

            // Detail: Stats header
            MakeText(detailPanel.transform, "StatsHeader", "── Stats ──",
                new Vector2(0.02f, 0.58f), new Vector2(0.98f, 0.66f), 14, FontStyle.Bold, TextAnchor.MiddleLeft)
                .GetComponent<Text>().color = new Color(0.5f, 0.8f, 1f);

            // Detail: Stats
            var statsGo = MakeText(detailPanel.transform, "Stats", "",
                new Vector2(0.04f, 0.38f), new Vector2(0.98f, 0.58f), 14, FontStyle.Normal, TextAnchor.UpperLeft);
            _detailStats = statsGo.GetComponent<Text>();
            _detailStats.color = Color.white;

            // Detail: Abilities header
            MakeText(detailPanel.transform, "AbilitiesHeader", "── Abilities ──",
                new Vector2(0.02f, 0.28f), new Vector2(0.98f, 0.36f), 14, FontStyle.Bold, TextAnchor.MiddleLeft)
                .GetComponent<Text>().color = new Color(1f, 0.6f, 0.3f);

            // Detail: Abilities
            var abilitiesGo = MakeText(detailPanel.transform, "Abilities", "",
                new Vector2(0.04f, 0.08f), new Vector2(0.98f, 0.28f), 13, FontStyle.Normal, TextAnchor.UpperLeft);
            _detailAbilities = abilitiesGo.GetComponent<Text>();
            _detailAbilities.color = Color.white;

            // Select button
            var selectGo = new GameObject("SelectBtn");
            selectGo.transform.SetParent(detailPanel.transform, false);
            var selectImg = selectGo.AddComponent<Image>();
            selectImg.color = new Color(0.15f, 0.5f, 0.25f, 0.9f);
            _selectButton = selectGo.AddComponent<Button>();
            _selectButton.onClick.AddListener(OnSelectClicked);
            var selectRect = selectGo.GetComponent<RectTransform>();
            selectRect.anchorMin = new Vector2(0.6f, 0.01f);
            selectRect.anchorMax = new Vector2(0.98f, 0.07f);
            selectRect.offsetMin = selectRect.offsetMax = Vector2.zero;
            var selectTextGo = MakeText(selectGo.transform, "BtnText", "Select",
                Vector2.zero, Vector2.one, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            _selectButtonText = selectTextGo.GetComponent<Text>();
        }

        private GameObject MakeText(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, FontStyle style, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = _font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4, 2);
            rt.offsetMax = new Vector2(-4, -2);
            return go;
        }

        // ────────────── Class List ──────────────

        private void RefreshClassList()
        {
            if (listContainer == null) return;

            // Clear existing
            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);

            int count = ClassRegistry.GetClassCount();
            var meta = MetaProgression.Instance;
            _selectedIndex = meta != null ? meta.GetSelectedClassIndex() : 0;

            for (int i = 0; i < count; i++)
            {
                var def = ClassRegistry.GetByIndex(i);
                if (def == null) continue;
                CreateClassListItem(def, i, i == _selectedIndex);
            }
        }

        private void CreateClassListItem(ClassDefinition def, int index, bool isSelected)
        {
            var go = new GameObject("Class_" + def.displayName);
            go.transform.SetParent(listContainer, false);

            // Layout element for consistent height
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 52;
            le.preferredHeight = 52;

            // Background
            var img = go.AddComponent<Image>();
            img.color = isSelected
                ? new Color(0.15f, 0.45f, 0.25f, 0.9f)
                : new Color(0.14f, 0.14f, 0.2f, 0.8f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = isSelected
                ? new Color(0.2f, 0.55f, 0.3f)
                : new Color(0.2f, 0.2f, 0.3f);
            colors.pressedColor = new Color(0.15f, 0.5f, 0.25f);
            btn.colors = colors;

            int idx = index;
            btn.onClick.AddListener(() => ShowDetail(idx));

            // Icon (left side)
            if (def.icon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = def.icon;
                iconImg.preserveAspect = true;
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.1f);
                iconRect.anchorMax = new Vector2(0.2f, 0.9f);
                iconRect.offsetMin = new Vector2(4, 2);
                iconRect.offsetMax = new Vector2(-2, -2);
            }

            float textLeft = def.icon != null ? 0.22f : 0.04f;

            // Class name
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = isSelected ? $"▸ {def.displayName}" : def.displayName;
            nameText.font = _font;
            nameText.fontSize = 15;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = isSelected ? new Color(1f, 0.9f, 0.3f) : Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(textLeft, 0.45f);
            nameRect.anchorMax = new Vector2(0.96f, 0.95f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;

            // HP/Speed subtitle
            var subGo = new GameObject("Sub");
            subGo.transform.SetParent(go.transform, false);
            var subText = subGo.AddComponent<Text>();
            subText.text = $"HP: {def.baseMaxHp}  Spd: {def.baseMoveSpeed:F1}";
            subText.font = _font;
            subText.fontSize = 11;
            subText.color = new Color(0.6f, 0.6f, 0.7f);
            subText.alignment = TextAnchor.MiddleLeft;
            var subRect = subGo.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(textLeft, 0.05f);
            subRect.anchorMax = new Vector2(0.96f, 0.45f);
            subRect.offsetMin = subRect.offsetMax = Vector2.zero;
        }

        // ────────────── Detail Panel ──────────────

        private void ShowDetail(int classIndex)
        {
            var def = ClassRegistry.GetByIndex(classIndex);
            if (def == null) return;

            _hoveredIndex = classIndex;

            // Name + icon
            if (_detailName != null) _detailName.text = def.displayName;
            if (_detailDescription != null) _detailDescription.text = def.description;

            if (_detailIcon != null)
            {
                if (def.icon != null)
                {
                    _detailIcon.sprite = def.icon;
                    _detailIcon.color = Color.white;
                }
                else
                {
                    _detailIcon.sprite = null;
                    _detailIcon.color = new Color(0.2f, 0.2f, 0.25f);
                }
            }

            // Stats
            if (_detailStats != null)
            {
                string dmgMult = def.baseDamageMultiplier != 1f
                    ? $"{def.baseDamageMultiplier:F1}x"
                    : "1.0x";
                string dmgReduc = def.baseDamageReduction > 0f
                    ? $"{def.baseDamageReduction * 100f:F0}%"
                    : "None";

                _detailStats.text =
                    $"Health:         {def.baseMaxHp}\n" +
                    $"Move Speed:     {def.baseMoveSpeed:F1}\n" +
                    $"Sprint Speed:   {def.baseSprintSpeed:F1}\n" +
                    $"Damage:         {dmgMult}\n" +
                    $"Damage Reduc:   {dmgReduc}";
            }

            // Abilities
            if (_detailAbilities != null)
            {
                string abilityText = "";
                abilityText += FormatAbility("Q", def.abilityQ);
                abilityText += FormatAbility("E", def.abilityE);
                abilityText += FormatAbility("R", def.abilityR);

                if (string.IsNullOrEmpty(abilityText))
                    abilityText = "No abilities configured";

                _detailAbilities.text = abilityText;
            }

            // Select button
            bool isCurrentlySelected = _selectedIndex == classIndex;
            if (_selectButton != null)
            {
                var img = _selectButton.GetComponent<Image>();
                if (img != null)
                    img.color = isCurrentlySelected
                        ? new Color(0.3f, 0.3f, 0.35f, 0.9f)
                        : new Color(0.15f, 0.5f, 0.25f, 0.9f);
            }
            if (_selectButtonText != null)
                _selectButtonText.text = isCurrentlySelected ? "Selected" : "Select Class";
        }

        private string FormatAbility(string key, AbilityConfig config)
        {
            if (config == null) return $"[{key}]  —\n";
            string cd = config.cooldown > 0 ? $"  ({config.cooldown:F0}s)" : "";
            return $"[{key}]  {config.displayName}{cd}\n";
        }

        // ────────────── Actions ──────────────

        private void OnSelectClicked()
        {
            if (_hoveredIndex < 0) return;
            if (_hoveredIndex == _selectedIndex) return;

            _selectedIndex = _hoveredIndex;
            if (MetaProgression.Instance != null)
                MetaProgression.Instance.SetSelectedClassForNextRun(_selectedIndex);

            // Sync class over the network immediately
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            {
                var playerClass = nm.LocalClient.PlayerObject.GetComponent<PlayerClass>();
                if (playerClass != null)
                    playerClass.RequestClassChange(_selectedIndex);
            }

            RefreshClassList();
            ShowDetail(_selectedIndex);

            Debug.Log($"[ClassSelectUI] Selected class index {_selectedIndex}: {ClassRegistry.GetByIndex(_selectedIndex)?.displayName}");
        }
    }
}
