using DungeonGame.Classes;
using DungeonGame.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Shows buttons for each class; clicking sets the selected class for the next run.
    /// Add to a GameObject in Town; optionally assign Canvas or it will create one.
    /// </summary>
    public class ClassSelectUI : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private string panelTitle = "Select Class";

        private void Start()
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) BuildCanvas();
            RefreshButtons();
        }

        private void BuildCanvas()
        {
            var go = new GameObject("ClassSelectCanvas");
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
            rect.anchorMin = new Vector2(0.02f, 0.5f);
            rect.anchorMax = new Vector2(0.22f, 0.98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 16;
            title.text = panelTitle;
            title.color = Color.white;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.88f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(8, 4);
            titleRect.offsetMax = new Vector2(-8, -4);

            var containerGo = new GameObject("Buttons");
            containerGo.transform.SetParent(panelGo.transform, false);
            var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            var rect2 = containerGo.GetComponent<RectTransform>();
            rect2.anchorMin = new Vector2(0, 0);
            rect2.anchorMax = new Vector2(1, 0.88f);
            rect2.offsetMin = rect2.offsetMax = Vector2.zero;
            buttonContainer = containerGo.transform;
        }

        private void RefreshButtons()
        {
            if (buttonContainer == null) return;
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
                Destroy(buttonContainer.GetChild(i).gameObject);

            int count = ClassRegistry.GetClassCount();
            var meta = MetaProgression.Instance;
            int selected = meta != null ? meta.GetSelectedClassIndex() : -1;

            for (int i = 0; i < count; i++)
            {
                var def = ClassRegistry.GetByIndex(i);
                if (def == null) continue;
                var btn = CreateButton(def.displayName, i, i == selected);
                btn.transform.SetParent(buttonContainer, false);
            }
        }

        private Button CreateButton(string label, int classIndex, bool isSelected)
        {
            var go = new GameObject("Class_" + label);
            var image = go.AddComponent<Image>();
            image.color = isSelected ? new Color(0.2f, 0.45f, 0.3f) : new Color(0.2f, 0.2f, 0.28f);
            var btn = go.AddComponent<Button>();
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 36);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = isSelected ? $"> {label}" : label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            int idx = classIndex;
            btn.onClick.AddListener(() =>
            {
                if (MetaProgression.Instance != null)
                    MetaProgression.Instance.SetSelectedClassForNextRun(idx);
                RefreshButtons();
            });
            return btn;
        }
    }
}
