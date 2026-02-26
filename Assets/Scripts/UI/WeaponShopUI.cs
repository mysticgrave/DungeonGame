using DungeonGame.Meta;
using DungeonGame.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Lists weapons from WeaponRegistry; shows Buy (gold) or Equip. Add to a GameObject in Town.
    /// </summary>
    public class WeaponShopUI : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private Text goldText;
        [SerializeField] private string panelTitle = "Weapons";

        private void Start()
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) BuildCanvas();
            if (MetaProgression.Instance != null)
                MetaProgression.Instance.OnGoldChanged += OnGoldChanged;
            RefreshGold();
            RefreshWeapons();
        }

        private void OnDestroy()
        {
            if (MetaProgression.Instance != null)
                MetaProgression.Instance.OnGoldChanged -= OnGoldChanged;
        }

        private void OnGoldChanged(int _) => RefreshGold();

        private void RefreshGold()
        {
            if (goldText != null && MetaProgression.Instance != null)
                goldText.text = $"Gold: {MetaProgression.Instance.Gold}";
        }

        private void BuildCanvas()
        {
            var go = new GameObject("WeaponShopCanvas");
            go.transform.SetParent(transform);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            canvas = c;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvas.transform, false);
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.78f, 0.5f);
            rect.anchorMax = new Vector2(0.98f, 0.98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 16;
            title.text = panelTitle;
            title.color = Color.white;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.92f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(8, 4);
            titleRect.offsetMax = new Vector2(-8, -4);

            var goldGo = new GameObject("Gold");
            goldGo.transform.SetParent(panelGo.transform, false);
            var gold = goldGo.AddComponent<Text>();
            gold.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gold.fontSize = 14;
            gold.color = new Color(1f, 0.85f, 0.4f);
            var goldRect = goldGo.GetComponent<RectTransform>();
            goldRect.anchorMin = new Vector2(0, 0.86f);
            goldRect.anchorMax = new Vector2(1, 0.92f);
            goldRect.offsetMin = new Vector2(8, 4);
            goldRect.offsetMax = new Vector2(-8, -4);
            goldText = gold;

            var scrollGo = new GameObject("Content");
            scrollGo.transform.SetParent(panelGo.transform, false);
            var vlg = scrollGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            var contentRect = scrollGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.86f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
            contentRoot = scrollGo.transform;
        }

        private void RefreshWeapons()
        {
            if (contentRoot == null) return;
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);

            var weapons = WeaponRegistry.GetAll();
            var meta = MetaProgression.Instance;
            string equipped = meta != null ? meta.GetEquippedWeaponId() : null;

            foreach (var w in weapons)
            {
                if (w == null) continue;
                bool unlocked = meta != null && meta.IsWeaponUnlocked(w.weaponId);
                bool isEquipped = w.weaponId == equipped;
                var row = CreateWeaponRow(w.displayName, w.weaponId, w.unlockCostGold, unlocked, isEquipped);
                row.transform.SetParent(contentRoot, false);
            }
        }

        private GameObject CreateWeaponRow(string displayName, string weaponId, int cost, bool unlocked, bool isEquipped)
        {
            var row = new GameObject("Weapon_" + weaponId);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childForceExpandWidth = false;
            layout.childControlWidth = true;
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 44);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = isEquipped ? $"[E] {displayName}" : displayName;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 13;
            label.color = Color.white;
            var layoutElem = labelGo.AddComponent<LayoutElement>();
            layoutElem.flexibleWidth = 1;
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(80, 0);

            if (unlocked)
            {
                var equipBtn = CreateButton(row.transform, "Equip", new Color(0.25f, 0.4f, 0.5f));
                equipBtn.onClick.AddListener(() =>
                {
                    if (MetaProgression.Instance != null)
                        MetaProgression.Instance.SetEquippedWeaponId(weaponId);
                    RefreshWeapons();
                });
            }
            else
            {
                var buyBtn = CreateButton(row.transform, cost == 0 ? "Free" : cost + "g", new Color(0.3f, 0.5f, 0.25f));
                buyBtn.onClick.AddListener(() =>
                {
                    if (MetaProgression.Instance != null && MetaProgression.Instance.UnlockWeapon(weaponId, cost))
                        RefreshWeapons();
                    RefreshGold();
                });
            }

            return row;
        }

        private static Button CreateButton(Transform parent, string label, Color color)
        {
            var go = new GameObject("Btn");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var btn = go.AddComponent<Button>();
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 56;
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = Color.white;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            return btn;
        }
    }
}
