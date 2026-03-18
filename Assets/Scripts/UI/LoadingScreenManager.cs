using System;
using System.Collections;
using DungeonGame.SpireGen;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// Shows a full-screen loading overlay during scene transitions. Subscribes to NetworkSceneManager
    /// scene events: shows on Load/Synchronize, fades out on LoadEventCompleted/SynchronizeComplete.
    /// For the dungeon (Spire_Slice), waits for NavMesh to finish baking before fading (so procedural generation completes first).
    /// Add to Main Menu, Town, and Spire_Slice. Uses DontDestroyOnLoad. Also used when quitting to main menu or returning to town.
    /// </summary>
    public class LoadingScreenManager : MonoBehaviour
    {
        public static LoadingScreenManager Instance { get; private set; }

        /// <summary>Fired when user clicks Cancel on the loading screen (e.g. during Host/Join).</summary>
        public static event Action OnCancelRequested;

        [Header("UI")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Text statusText;
        [Tooltip("Optional. Shown while loading.")]
        [SerializeField] private string loadingMessage = "Loading...";
        [Tooltip("Duration of fade-out when revealing the loaded scene.")]
        [SerializeField] private float fadeOutDuration = 1.5f;
        [Tooltip("Dungeon scene name. Loading screen waits for NavMesh bake before fading in this scene.")]
        [SerializeField] private string dungeonSceneName = "Spire_Slice";

        private Coroutine _fadeCoroutine;
        private bool _waitingForDungeonGen;
        private float _dungeonWaitStarted;
        private Button _cancelButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureUI();
            if (panel != null) panel.SetActive(false);
        }

        private void Start()
        {
            SubscribeToSceneEvents();
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
            // Loading screen is now hidden by DungeonPlayerSpawner after player repositioning.
            // NavMeshBakeOnLayout.OnNavMeshBuilt += OnDungeonNavMeshBuilt;
        }

        private void OnEnable()
        {
            SubscribeToSceneEvents();
        }

        private void Update()
        {
            // SceneManager is only created when NetworkManager starts (StartHost/StartClient).
            // Clients joining from MainMenu have SceneManager=null at Start, so we never subscribed.
            // Poll until it's available so we receive Synchronize/SynchronizeComplete for late joiners.
            SubscribeToSceneEvents();

            if (_waitingForDungeonGen && Time.realtimeSinceStartup - _dungeonWaitStarted > 30f)
            {
                _waitingForDungeonGen = false;
                FadeOutAndHide();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            // NavMeshBakeOnLayout.OnNavMeshBuilt -= OnDungeonNavMeshBuilt;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SceneManager != null)
                nm.SceneManager.OnSceneEvent -= HandleSceneEvent;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        }

        /// <summary>Fallback: when Main Menu unloads during Town load, Netcode events may not fire. Unity's sceneLoaded always fires.</summary>
        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[LoadingScreen] OnUnitySceneLoaded: {scene.name} (panel.active={panel?.activeSelf})");
            if (panel == null || !panel.activeSelf) return;
            if (scene.name == dungeonSceneName)
            {
                _waitingForDungeonGen = true;
                _dungeonWaitStarted = Time.realtimeSinceStartup;
                if (statusText != null) statusText.text = "Generating dungeon...";
                return;
            }
            Debug.Log($"[LoadingScreen] Fading out via OnUnitySceneLoaded ({scene.name})");
            FadeOutAndHide();
        }

        private void OnDungeonNavMeshBuilt(NavMeshBakeOnLayout baker)
        {
            if (!_waitingForDungeonGen) return;
            if (panel == null || !panel.activeSelf) return;
            _waitingForDungeonGen = false;
            FadeOutAndHide();
        }

        private bool _loggedSceneSubscribe;

        private void SubscribeToSceneEvents()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null) return;
            nm.SceneManager.OnSceneEvent -= HandleSceneEvent;
            nm.SceneManager.OnSceneEvent += HandleSceneEvent;
            if (!_loggedSceneSubscribe)
            {
                _loggedSceneSubscribe = true;
                Debug.Log($"[LoadingScreen] Subscribed to SceneManager.OnSceneEvent (IsClient={nm.IsClient} IsServer={nm.IsServer})");
            }
        }

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            var nm = NetworkManager.Singleton;
            var role = nm != null ? (nm.IsServer ? "Server" : nm.IsClient ? "Client" : "?") : "no NM";
            Debug.Log($"[LoadingScreen] SceneEvent: {sceneEvent.SceneEventType} scene={sceneEvent.SceneName} clientId={sceneEvent.ClientId} ({role})");

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                case SceneEventType.Synchronize:
                    _waitingForDungeonGen = false;
                    Show();
                    break;
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.SynchronizeComplete:
                    string sceneName = sceneEvent.SceneName;
                    // SynchronizeComplete may not have SceneName populated; fall back to active scene
                    if (string.IsNullOrEmpty(sceneName))
                        sceneName = SceneManager.GetActiveScene().name;
                    if (sceneName == dungeonSceneName)
                    {
                        _waitingForDungeonGen = true;
                        _dungeonWaitStarted = Time.realtimeSinceStartup;
                        if (statusText != null) statusText.text = "Generating dungeon...";
                    }
                    else
                    {
                        Debug.Log($"[LoadingScreen] Fading out (scene '{sceneName}' loaded, not dungeon)");
                        FadeOutAndHide();
                    }
                    break;
            }
        }

        /// <summary>Show the loading screen. Call before Host/Join or LoadScene.</summary>
        public void Show()
        {
            ShowWithMessage(loadingMessage);
        }

        /// <summary>Show with a custom status message. Set showCancel true for Main Menu Host/Join so user can cancel.</summary>
        public void ShowWithMessage(string message, bool showCancelButton = false)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            EnsureUI(showCancelButton);
            if (panel != null) panel.SetActive(true);
            EnsureCanvasGroup();
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
            // Ensure fully opaque background (avoid semi-transparent overlay).
            if (panel != null)
            {
                var img = panel.GetComponent<Image>();
                if (img != null)
                {
                    var c = img.color;
                    c.a = 1f;
                    img.color = c;
                }
            }
            if (statusText != null && !string.IsNullOrEmpty(message))
                statusText.text = message;
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(showCancelButton);
        }

        /// <summary>True while the loading screen is showing (including during fade-out).</summary>
        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>Instantly hide the loading screen.</summary>
        public void Hide()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>Fade out over fadeOutDuration, then hide. Use when scene is ready to reveal.</summary>
        public void FadeOutAndHide()
        {
            if (panel == null || !panel.activeSelf)
                return;
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            EnsureCanvasGroup();
            var cg = panelCanvasGroup;
            if (cg == null)
            {
                if (panel != null) panel.SetActive(false);
                _fadeCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(1f - elapsed / fadeOutDuration);
                yield return null;
            }

            cg.alpha = 0f;
            if (panel != null) panel.SetActive(false);
            _fadeCoroutine = null;
        }

        private void EnsureCanvasGroup()
        {
            if (panelCanvasGroup != null || panel == null) return;
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null) panelCanvasGroup = panel.AddComponent<CanvasGroup>();
        }

        private void EnsureUI(bool needCancelButton = false)
        {
            if (panel != null)
            {
                if (needCancelButton && _cancelButton == null)
                    CreateCancelButton();
                return;
            }

            var go = new GameObject("LoadingScreenCanvas");
            go.transform.SetParent(transform);

            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 9999; // Above other UI
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            canvas = c;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvas.transform, false);
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel = panelGo;

            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(panel.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.9f, 0.9f);
            text.text = loadingMessage;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.2f, 0.4f);
            textRect.anchorMax = new Vector2(0.8f, 0.6f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            statusText = text;

            if (needCancelButton)
                CreateCancelButton();
        }

        private void CreateCancelButton()
        {
            if (_cancelButton != null || panel == null) return;

            var btnGo = new GameObject("CancelButton");
            btnGo.transform.SetParent(panel.transform, false);
            btnGo.AddComponent<Image>().color = new Color(0.25f, 0.2f, 0.2f, 0.95f);
            var btn = btnGo.AddComponent<Button>();
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.4f, 0.15f);
            btnRect.anchorMax = new Vector2(0.6f, 0.28f);
            btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

            var btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnText = btnTextGo.AddComponent<Text>();
            btnText.text = "Cancel";
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial");
            btnText.fontSize = 22;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.horizontalOverflow = HorizontalWrapMode.Overflow;
            btnText.verticalOverflow = VerticalWrapMode.Overflow;
            var btnTextRect = btnTextGo.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = btnTextRect.offsetMax = Vector2.zero;

            btn.onClick.AddListener(() =>
            {
                Hide();
                OnCancelRequested?.Invoke();
            });
            _cancelButton = btn;
            _cancelButton.gameObject.SetActive(false);
        }
    }
}
