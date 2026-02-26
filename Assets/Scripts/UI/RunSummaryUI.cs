using DungeonGame.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Shows a panel when returning to Town with the last run result (outcome, gold, EXP, class level).
    /// Add to any GameObject in the Town scene; optionally assign an existing Canvas.
    /// </summary>
    public class RunSummaryUI : MonoBehaviour
    {
        [SerializeField] private string townSceneName = "Town";
        [SerializeField] private float autoHideAfterSeconds = 0f;
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text rewardsText;
        [SerializeField] private Button continueButton;

        private float _showTime;
        private bool _showing;

        private void Start()
        {
            if (canvas == null) canvas = GetComponentInChildren<Canvas>();
            if (canvas != null && panel == null) panel = canvas.GetComponentInChildren<Image>()?.gameObject;
            if (panel != null) panel.SetActive(false);
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != townSceneName) return;
            var meta = MetaProgression.Instance;
            if (meta == null) return;
            var result = meta.GetLastRunResult();
            if (!result.HasValue) return;
            Show(result.Value);
        }

        private void Update()
        {
            if (!_showing || panel == null || !panel.activeSelf) return;
            if (autoHideAfterSeconds > 0f && Time.unscaledTime - _showTime >= autoHideAfterSeconds)
                Dismiss();
        }

        private void Show(MetaProgression.LastRunResult result)
        {
            EnsureCanvas();
            if (titleText != null)
            {
                string outcomeStr = result.outcome switch
                {
                    RunOutcome.Victory => "Victory!",
                    RunOutcome.Evac => "Evacuated",
                    RunOutcome.Wipe => "Wiped",
                    _ => "Run Over"
                };
                titleText.text = outcomeStr;
            }

            if (rewardsText != null)
            {
                rewardsText.text = $"+{result.gold} gold\n+{result.exp} EXP\n" +
                    (result.classLevel > 0 ? $"Class level {result.classLevel}" : "");
            }

            if (continueButton != null)
                continueButton.onClick.RemoveAllListeners();
            if (continueButton != null)
                continueButton.onClick.AddListener(Dismiss);

            if (panel != null) panel.SetActive(true);
            _showTime = Time.unscaledTime;
            _showing = true;
        }

        public void Dismiss()
        {
            _showing = false;
            if (panel != null) panel.SetActive(false);
            var meta = MetaProgression.Instance;
            if (meta != null) meta.ClearLastRunResult();
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            var go = new GameObject("RunSummaryCanvas");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            canvas = c;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvas.transform, false);
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.35f);
            rect.anchorMax = new Vector2(0.75f, 0.65f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel = panelGo;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 28;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.7f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(10, 10);
            titleRect.offsetMax = new Vector2(-10, -10);
            titleText = title;

            var rewardsGo = new GameObject("Rewards");
            rewardsGo.transform.SetParent(panel.transform, false);
            var rewards = rewardsGo.AddComponent<Text>();
            rewards.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rewards.fontSize = 18;
            rewards.alignment = TextAnchor.MiddleCenter;
            rewards.color = new Color(0.9f, 0.85f, 0.7f);
            var rewardsRect = rewardsGo.GetComponent<RectTransform>();
            rewardsRect.anchorMin = new Vector2(0, 0.25f);
            rewardsRect.anchorMax = new Vector2(1, 0.7f);
            rewardsRect.offsetMin = new Vector2(10, 10);
            rewardsRect.offsetMax = new Vector2(-10, -10);
            rewardsText = rewards;

            var btnGo = new GameObject("ContinueButton");
            btnGo.transform.SetParent(panel.transform, false);
            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.5f, 0.3f);
            var btn = btnGo.AddComponent<Button>();
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.3f, 0.05f);
            btnRect.anchorMax = new Vector2(0.7f, 0.2f);
            btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;
            var btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnText = btnTextGo.AddComponent<Text>();
            btnText.text = "Continue";
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 20;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            var btnTextRect = btnTextGo.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = btnTextRect.offsetMax = Vector2.zero;
            continueButton = btn;
        }
    }
}
