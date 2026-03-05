using DungeonGame.Core;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Local pause menu for in-game. Toggle with Escape. Freezes this player's camera and movement
    /// (multiplayer-safe); cursor unlocks so they can click Resume, Settings, or Quit.
    /// Add to a Canvas in Town and Spire_Slice, or a persistent object.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        [Header("UI")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button backToTownButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Config")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string townSceneName = "Town";
        [SerializeField] private string dungeonSceneName = "Spire_Slice";
        [Tooltip("Scenes where pause is allowed (e.g. Town, Spire_Slice). Empty = allow everywhere except MainMenu.")]
        [SerializeField] private string[] allowedScenes = { "Town", "Spire_Slice" };

        private Canvas _canvas;

        private void OnDestroy()
        {
            if (IsPaused)
            {
                IsPaused = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Awake()
        {
            if (panel == null) EnsureUI();
            if (panel != null) panel.SetActive(false);
            if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(Resume); }
            if (backToTownButton != null) { backToTownButton.onClick.RemoveAllListeners(); backToTownButton.onClick.AddListener(BackToTown); }
            if (quitButton != null) { quitButton.onClick.RemoveAllListeners(); quitButton.onClick.AddListener(QuitToMainMenu); }
            if (settingsButton != null) { settingsButton.onClick.RemoveAllListeners(); settingsButton.onClick.AddListener(OnSettingsClicked); }

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvas.sortingOrder = 9000; // Above gameplay UI, below loading screen
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null) return;
            if (!UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (!IsPauseAllowed()) return;

            TogglePause();
        }

        private bool IsPauseAllowed()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (scene == mainMenuSceneName) return false;
            if (allowedScenes == null || allowedScenes.Length == 0) return true;
            foreach (var s in allowedScenes)
                if (s == scene) return true;
            return false;
        }

        public void TogglePause()
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (!IsPauseAllowed()) return;
            IsPaused = true;
            UpdateBackToTownVisibility();
            if (panel != null) panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void UpdateBackToTownVisibility()
        {
            if (backToTownButton == null) return;
            var scene = SceneManager.GetActiveScene().name;
            var nm = NetworkManager.Singleton;
            bool canShow = scene == dungeonSceneName && nm != null && nm.IsServer;
            backToTownButton.gameObject.SetActive(canShow);
        }

        public void Resume()
        {
            IsPaused = false;
            if (panel != null) panel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnSettingsClicked()
        {
            // Placeholder: could open a Settings panel. For now, just log.
            Debug.Log("[PauseMenu] Settings — add your settings UI here.");
        }

        private void EnsureUI()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                var go = new GameObject("PauseMenuCanvas");
                go.transform.SetParent(transform);
                _canvas = go.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 9000;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                go.AddComponent<GraphicRaycaster>();
                if (EventSystem.current == null)
                {
                    var esGo = new GameObject("EventSystem");
                    esGo.AddComponent<EventSystem>();
                    esGo.AddComponent<InputSystemUIInputModule>();
                }
            }

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(_canvas.transform, false);
            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0.3f);
            rt.anchorMax = new Vector2(0.75f, 0.7f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            panel = panelGo;

            resumeButton = CreateButton(panel.transform, "Resume", new Vector2(0.35f, 0.68f), new Vector2(0.65f, 0.82f), Resume);
            backToTownButton = CreateButton(panel.transform, "Back to Town", new Vector2(0.35f, 0.5f), new Vector2(0.65f, 0.64f), BackToTown);
            settingsButton = CreateButton(panel.transform, "Settings", new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.46f), OnSettingsClicked);
            quitButton = CreateButton(panel.transform, "Quit to Main Menu", new Vector2(0.35f, 0.14f), new Vector2(0.65f, 0.28f), QuitToMainMenu);
            if (backToTownButton != null) backToTownButton.gameObject.SetActive(false);
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.4f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial");
            text.fontSize = 24;
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

        public void BackToTown()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.SceneManager == null)
                return;

            IsPaused = false;
            if (panel != null) panel.SetActive(false);

            LoadingScreenManager.Instance?.ShowWithMessage("Returning to town...", showCancelButton: false);
            nm.SceneManager.LoadScene(townSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public void QuitToMainMenu()
        {
            IsPaused = false;
            if (panel != null) panel.SetActive(false);

            LoadingScreenManager.Instance?.ShowWithMessage("Returning to main menu...", showCancelButton: false);

#if !DISABLESTEAMWORKS
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.LeaveLobby();
            else
#endif
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                    nm.Shutdown();
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
