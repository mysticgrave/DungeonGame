using System.Collections.Generic;
using DungeonGame.Items;
using DungeonGame.Weapons;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DungeonGame.DevTools
{
    /// <summary>
    /// Character Preview dev tool. Spawns a non-networked player model and lets you
    /// preview held items with real-time transform editing. Attach to an empty GameObject
    /// in a lightweight preview scene.
    ///
    /// Setup:
    /// 1. Create scene "CharacterPreview" with: Directional Light, Ground Plane, this script on an empty GO.
    /// 2. Assign playerVisualPrefab (the 3P model from inside your Player prefab — the child with the Animator).
    /// 3. Assign itemRegistry (same list of WeaponConfigs from your ItemRegistry).
    /// 4. Press Play. Use the UI to select items and adjust offsets.
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The 3D model prefab (the child with Animator, NOT the full networked Player prefab).")]
        public GameObject playerVisualPrefab;

        [Tooltip("All available weapon configs. Copy from your ItemRegistry asset.")]
        public List<WeaponConfig> items = new();

        [Header("Hand Bone Names")]
        [Tooltip("Name of the right hand bone in the hierarchy.")]
        public string rightHandBoneName = "Hand_R";
        [Tooltip("Name of the left hand bone in the hierarchy.")]
        public string leftHandBoneName = "Hand_L";

        // Runtime
        private GameObject _modelInstance;
        private Transform _leftHandBone;
        private Transform _rightHandBone;
        private Transform _currentItemVisual;
        private PreviewOrbitCamera _orbitCam;

        // UI State
        private int _selectedItemIndex;
        private bool _isLeftHand;
        private Vector3 _posOffset;
        private Vector3 _rotOffset;
        private Vector3 _scaleOffset = Vector3.one;

        // UI refs
        private Canvas _canvas;
        private Dropdown _itemDropdown;
        private Toggle _handToggle;
        private Slider[] _posSliders = new Slider[3];
        private Slider[] _rotSliders = new Slider[3];
        private Slider[] _scaleSliders = new Slider[3];
        private Text _valuesText;
        private InputField[] _posInputs = new InputField[3];
        private InputField[] _rotInputs = new InputField[3];
        private InputField[] _scaleInputs = new InputField[3];

        private void Start()
        {
            SpawnModel();
            BuildUI();
            SetupCamera();
            ApplyItem();
        }

        private void SpawnModel()
        {
            if (playerVisualPrefab == null)
            {
                Debug.LogError("[CharacterPreview] playerVisualPrefab is not assigned!");
                return;
            }

            _modelInstance = Instantiate(playerVisualPrefab, Vector3.zero, Quaternion.identity);
            _modelInstance.name = "PreviewModel";

            // Find hand bones — use Animator if available for reliable bone lookup
            var animator = _modelInstance.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                _rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                _leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Debug.Log($"[CharacterPreview] Using Animator humanoid bones — R: {_rightHandBone?.name ?? "NULL"}, L: {_leftHandBone?.name ?? "NULL"}");
            }

            // Fallback to name search if Animator didn't find them
            if (_rightHandBone == null)
                _rightHandBone = FindBoneRecursive(_modelInstance.transform, rightHandBoneName);
            if (_leftHandBone == null)
                _leftHandBone = FindBoneRecursive(_modelInstance.transform, leftHandBoneName);

            if (_rightHandBone == null)
                Debug.LogWarning($"[CharacterPreview] Could not find right hand bone '{rightHandBoneName}'. Check the bone name.");
            else
                Debug.Log($"[CharacterPreview] Right hand bone: '{_rightHandBone.name}' (path: {GetPath(_rightHandBone)})");

            if (_leftHandBone == null)
                Debug.LogWarning($"[CharacterPreview] Could not find left hand bone '{leftHandBoneName}'. Check the bone name.");
            else
                Debug.Log($"[CharacterPreview] Left hand bone: '{_leftHandBone.name}' (path: {GetPath(_leftHandBone)})");

            // Play idle animation if available
            if (animator != null)
            {
                animator.SetFloat("MoveSpeed", 0f);
                animator.SetFloat("Speed", 0f);
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            // Exact match first (full pass)
            var exact = FindBoneExact(parent, boneName);
            if (exact != null) return exact;

            // Then contains match (for prefixed names like "mixamorig:Hand_R")
            return FindBoneContains(parent, boneName);
        }

        private Transform FindBoneExact(Transform parent, string boneName)
        {
            if (parent.name == boneName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindBoneExact(parent.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
        }

        private Transform FindBoneContains(Transform parent, string boneName)
        {
            // EndsWith is safer than Contains to avoid "Hand_Left" matching "Hand_L"
            if (parent.name.EndsWith(boneName)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindBoneContains(parent.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
        }

        private void SetupCamera()
        {
            var camGO = new GameObject("PreviewCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;

            _orbitCam = camGO.AddComponent<PreviewOrbitCamera>();
            _orbitCam.target = _modelInstance != null ? _modelInstance.transform : null;
            _orbitCam.distance = 3f;

            // Disable any existing cameras
            foreach (var existingCam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (existingCam != cam) existingCam.enabled = false;
            }
        }

        // ──────────────────── UI Building ────────────────────

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("PreviewUI");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists (required for all UI interaction)
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<InputSystemUIInputModule>();
            }

            // Panel background (left side)
            var panel = CreatePanel(canvasGO.transform, "Panel", new Vector2(320f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(320f, 1f));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.85f);

            float y = -10f;
            float spacing = 30f;

            // Title
            CreateLabel(panel.transform, "Character Preview", y, 18, FontStyle.Bold);
            y -= 35f;

            // Item dropdown
            CreateLabel(panel.transform, "Item:", y, 14);
            y -= 22f;
            _itemDropdown = CreateDropdown(panel.transform, y);
            PopulateItemDropdown();
            _itemDropdown.onValueChanged.AddListener(OnItemChanged);
            y -= 35f;

            // Hand toggle
            var handRow = CreateRow(panel.transform, y);
            CreateLabel(handRow.transform, "Hand:", 0f, 14);
            _handToggle = CreateToggle(handRow.transform, "Left Hand", 80f);
            _handToggle.onValueChanged.AddListener(OnHandChanged);
            y -= spacing;

            // Position sliders
            y -= 10f;
            CreateLabel(panel.transform, "Position Offset:", y, 14, FontStyle.Bold);
            y -= 22f;
            string[] posLabels = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                var row = CreateSliderRow(panel.transform, posLabels[i], y, -1f, 1f, 0f, i, SliderType.Position);
                y -= spacing;
            }

            // Rotation sliders
            y -= 10f;
            CreateLabel(panel.transform, "Rotation Offset:", y, 14, FontStyle.Bold);
            y -= 22f;
            string[] rotLabels = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                var row = CreateSliderRow(panel.transform, rotLabels[i], y, -180f, 180f, 0f, i, SliderType.Rotation);
                y -= spacing;
            }

            // Scale sliders
            y -= 10f;
            CreateLabel(panel.transform, "Scale:", y, 14, FontStyle.Bold);
            y -= 22f;
            string[] scaleLabels = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                var row = CreateSliderRow(panel.transform, scaleLabels[i], y, 0.01f, 100f, 1f, i, SliderType.Scale);
                y -= spacing;
            }

            // Values display
            y -= 15f;
            _valuesText = CreateLabel(panel.transform, "", y, 11);
            _valuesText.alignment = TextAnchor.UpperLeft;
            var valuesRect = _valuesText.GetComponent<RectTransform>();
            valuesRect.sizeDelta = new Vector2(300f, 80f);

            // Buttons
            y -= 85f;
            var btnRow = CreateRow(panel.transform, y);
            CreateButton(btnRow.transform, "Copy to Clipboard", 10f, 140f, OnCopyValues);
            CreateButton(btnRow.transform, "Reset", 155f, 70f, OnReset);
            CreateButton(btnRow.transform, "Focus Hand", 230f, 80f, OnFocusHand);

            // Load current values if item selected
            LoadCurrentConfigValues();
        }

        private void PopulateItemDropdown()
        {
            _itemDropdown.ClearOptions();
            var options = new List<string> { "(None)" };
            foreach (var item in items)
            {
                if (item != null)
                    options.Add(item.displayName);
                else
                    options.Add("(null)");
            }
            _itemDropdown.AddOptions(options);
        }

        // ──────────────────── UI Callbacks ────────────────────

        private void OnItemChanged(int index)
        {
            _selectedItemIndex = index;
            LoadCurrentConfigValues();
            ApplyItem();
        }

        private void OnHandChanged(bool isLeft)
        {
            _isLeftHand = isLeft;
            Debug.Log($"[CharacterPreview] Hand switched to: {(_isLeftHand ? "LEFT" : "RIGHT")}");
            LoadCurrentConfigValues();
            ApplyItem();
        }

        private void LoadCurrentConfigValues()
        {
            if (_selectedItemIndex <= 0 || _selectedItemIndex - 1 >= items.Count)
            {
                _posOffset = Vector3.zero;
                _rotOffset = Vector3.zero;
                _scaleOffset = Vector3.one;
            }
            else
            {
                var config = items[_selectedItemIndex - 1];
                if (_isLeftHand)
                {
                    _posOffset = config.leftHeldPositionOffset != Vector3.zero
                        ? config.leftHeldPositionOffset : config.heldPositionOffset;
                    _rotOffset = config.leftHeldRotationOffset != Vector3.zero
                        ? config.leftHeldRotationOffset : config.heldRotationOffset;
                    _scaleOffset = config.leftHeldScale != Vector3.zero
                        ? config.leftHeldScale
                        : (config.heldScale == Vector3.zero ? Vector3.one : config.heldScale);
                }
                else
                {
                    _posOffset = config.heldPositionOffset;
                    _rotOffset = config.heldRotationOffset;
                    _scaleOffset = config.heldScale == Vector3.zero ? Vector3.one : config.heldScale;
                }
            }

            // Update sliders without triggering callbacks
            SetSliderValues(_posSliders, _posInputs, _posOffset);
            SetSliderValues(_rotSliders, _rotInputs, _rotOffset);
            SetSliderValues(_scaleSliders, _scaleInputs, _scaleOffset);
            UpdateValuesText();
        }

        private void SetSliderValues(Slider[] sliders, InputField[] inputs, Vector3 val)
        {
            float[] v = { val.x, val.y, val.z };
            for (int i = 0; i < 3; i++)
            {
                if (sliders[i] != null)
                {
                    sliders[i].SetValueWithoutNotify(Mathf.Clamp(v[i], sliders[i].minValue, sliders[i].maxValue));
                }
                if (inputs[i] != null)
                    inputs[i].SetTextWithoutNotify(v[i].ToString("F4"));
            }
        }

        private void OnSliderChanged(int axis, float value, SliderType type)
        {
            switch (type)
            {
                case SliderType.Position:
                    _posOffset[axis] = value;
                    if (_posInputs[axis] != null) _posInputs[axis].SetTextWithoutNotify(value.ToString("F4"));
                    break;
                case SliderType.Rotation:
                    _rotOffset[axis] = value;
                    if (_rotInputs[axis] != null) _rotInputs[axis].SetTextWithoutNotify(value.ToString("F1"));
                    break;
                case SliderType.Scale:
                    _scaleOffset[axis] = value;
                    if (_scaleInputs[axis] != null) _scaleInputs[axis].SetTextWithoutNotify(value.ToString("F4"));
                    break;
            }
            UpdateItemVisual();
            UpdateValuesText();
        }

        private void OnInputChanged(int axis, string text, SliderType type)
        {
            if (!float.TryParse(text, out float value)) return;
            switch (type)
            {
                case SliderType.Position:
                    _posOffset[axis] = value;
                    if (_posSliders[axis] != null)
                        _posSliders[axis].SetValueWithoutNotify(Mathf.Clamp(value, _posSliders[axis].minValue, _posSliders[axis].maxValue));
                    break;
                case SliderType.Rotation:
                    _rotOffset[axis] = value;
                    if (_rotSliders[axis] != null)
                        _rotSliders[axis].SetValueWithoutNotify(Mathf.Clamp(value, _rotSliders[axis].minValue, _rotSliders[axis].maxValue));
                    break;
                case SliderType.Scale:
                    _scaleOffset[axis] = value;
                    if (_scaleSliders[axis] != null)
                        _scaleSliders[axis].SetValueWithoutNotify(Mathf.Clamp(value, _scaleSliders[axis].minValue, _scaleSliders[axis].maxValue));
                    break;
            }
            UpdateItemVisual();
            UpdateValuesText();
        }

        private void OnCopyValues()
        {
            string hand = _isLeftHand ? "Left" : "Right";
            string text = $"// {hand} Hand values for item #{_selectedItemIndex}\n" +
                          $"Position: ({_posOffset.x:F4}, {_posOffset.y:F4}, {_posOffset.z:F4})\n" +
                          $"Rotation: ({_rotOffset.x:F1}, {_rotOffset.y:F1}, {_rotOffset.z:F1})\n" +
                          $"Scale:    ({_scaleOffset.x:F4}, {_scaleOffset.y:F4}, {_scaleOffset.z:F4})";
            GUIUtility.systemCopyBuffer = text;
            Debug.Log($"[CharacterPreview] Copied to clipboard:\n{text}");
        }

        private void OnReset()
        {
            _posOffset = Vector3.zero;
            _rotOffset = Vector3.zero;
            _scaleOffset = Vector3.one;
            SetSliderValues(_posSliders, _posInputs, _posOffset);
            SetSliderValues(_rotSliders, _rotInputs, _rotOffset);
            SetSliderValues(_scaleSliders, _scaleInputs, _scaleOffset);
            UpdateItemVisual();
            UpdateValuesText();
        }

        private void OnFocusHand()
        {
            Transform bone = _isLeftHand ? _leftHandBone : _rightHandBone;
            if (bone != null && _orbitCam != null)
                _orbitCam.FocusOnHeight(bone.position.y);
        }

        private void UpdateValuesText()
        {
            if (_valuesText == null) return;
            string hand = _isLeftHand ? "leftHeld" : "held";
            _valuesText.text =
                $"<b>{hand}PositionOffset</b> = ({_posOffset.x:F4}, {_posOffset.y:F4}, {_posOffset.z:F4})\n" +
                $"<b>{hand}RotationOffset</b> = ({_rotOffset.x:F1}, {_rotOffset.y:F1}, {_rotOffset.z:F1})\n" +
                $"<b>{hand}Scale</b> = ({_scaleOffset.x:F4}, {_scaleOffset.y:F4}, {_scaleOffset.z:F4})";
        }

        // ──────────────────── Item Visual ────────────────────

        private void ApplyItem()
        {
            if (_currentItemVisual != null)
                Destroy(_currentItemVisual.gameObject);

            if (_selectedItemIndex <= 0 || _selectedItemIndex - 1 >= items.Count) return;

            var config = items[_selectedItemIndex - 1];
            if (config == null || config.heldVisualPrefab == null) return;

            Transform bone = _isLeftHand ? _leftHandBone : _rightHandBone;
            if (bone == null)
            {
                Debug.LogWarning($"[CharacterPreview] {(_isLeftHand ? "Left" : "Right")} hand bone is null! Check bone names in inspector.");
                return;
            }

            Debug.Log($"[CharacterPreview] Attaching '{config.displayName}' to {(_isLeftHand ? "LEFT" : "RIGHT")} bone: {bone.name}");
            _currentItemVisual = Instantiate(config.heldVisualPrefab, bone).transform;
            UpdateItemVisual();
        }

        private void UpdateItemVisual()
        {
            if (_currentItemVisual == null) return;
            _currentItemVisual.localPosition = _posOffset;
            _currentItemVisual.localRotation = Quaternion.Euler(_rotOffset);
            _currentItemVisual.localScale = _scaleOffset;
        }

        // ──────────────────── UI Helpers ────────────────────

        private enum SliderType { Position, Rotation, Scale }

        private GameObject CreatePanel(Transform parent, string name, Vector2 offsetMin,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = offsetMax;
            return go;
        }

        private Text CreateLabel(Transform parent, string text, float y, int fontSize, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-20f, 22f);

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = Color.white;
            t.supportRichText = true;
            return t;
        }

        private Dropdown CreateDropdown(Transform parent, float y)
        {
            var go = new GameObject("Dropdown");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-20f, 28f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f);

            var dd = go.AddComponent<Dropdown>();

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-25f, 0f);
            var labelText = labelGO.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 13;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            dd.captionText = labelText;

            // Template
            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(go.transform, false);
            var templateRect = templateGO.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0f, 200f);
            templateGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
            templateGO.AddComponent<ScrollRect>();

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(templateGO.transform, false);
            var vpRect = viewportGO.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = Color.white;
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            templateGO.GetComponent<ScrollRect>().viewport = vpRect;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 28f);
            templateGO.GetComponent<ScrollRect>().content = contentRect;

            // Item
            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 24f);
            itemGO.AddComponent<Toggle>();

            var itemLabelGO = new GameObject("Item Label");
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabelRect = itemLabelGO.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(8f, 0f);
            itemLabelRect.offsetMax = Vector2.zero;
            var itemLabel = itemLabelGO.AddComponent<Text>();
            itemLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabel.fontSize = 13;
            itemLabel.color = Color.white;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            dd.itemText = itemLabel;

            templateGO.SetActive(false);
            dd.template = templateRect;

            return dd;
        }

        private GameObject CreateRow(Transform parent, float y)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-20f, 24f);
            return go;
        }

        private Toggle CreateToggle(Transform parent, string label, float x)
        {
            var go = new GameObject("Toggle");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(120f, 0f);

            var toggle = go.AddComponent<Toggle>();

            // Checkbox background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.sizeDelta = new Vector2(16f, 16f);
            bgRect.anchoredPosition = new Vector2(8f, 0f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.3f, 0.3f, 0.35f);
            toggle.targetGraphic = bgImg;

            // Checkmark
            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkRect = checkGO.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(2f, 2f);
            checkRect.offsetMax = new Vector2(-2f, -2f);
            var checkImg = checkGO.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.8f, 0.4f);
            toggle.graphic = checkImg;

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(22f, 0f);
            labelRect.offsetMax = Vector2.zero;
            var t = labelGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 13;
            t.color = Color.white;

            return toggle;
        }

        private GameObject CreateSliderRow(Transform parent, string label, float y,
            float min, float max, float defaultVal, int axis, SliderType type)
        {
            var row = CreateRow(parent, y);

            // Axis label
            var labelGO = new GameObject("AxisLabel");
            labelGO.transform.SetParent(row.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            labelRect.sizeDelta = new Vector2(18f, 0f);
            var t = labelGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;

            // Slider
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(row.transform, false);
            var sliderRect = sliderGO.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(0f, 1f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(20f, 0f);
            sliderRect.sizeDelta = new Vector2(190f, 0f);

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.4f);
            bgRect.anchorMax = new Vector2(1f, 0.6f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.4f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.6f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillGO.AddComponent<Image>().color = new Color(0.3f, 0.6f, 1f, 0.5f);

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = handleGO.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12f, 20f);
            handleGO.AddComponent<Image>().color = new Color(0.5f, 0.7f, 1f);

            var slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;

            int capturedAxis = axis;
            SliderType capturedType = type;
            slider.onValueChanged.AddListener(v => OnSliderChanged(capturedAxis, v, capturedType));

            // Input field for precise values
            var inputGO = new GameObject("Input");
            inputGO.transform.SetParent(row.transform, false);
            var inputRect = inputGO.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(0f, 1f);
            inputRect.pivot = new Vector2(0f, 0.5f);
            inputRect.anchoredPosition = new Vector2(215f, 0f);
            inputRect.sizeDelta = new Vector2(75f, 0f);
            inputGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);

            var input = inputGO.AddComponent<InputField>();
            var inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(inputGO.transform, false);
            var inputTextRect = inputTextGO.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(4f, 0f);
            inputTextRect.offsetMax = new Vector2(-4f, 0f);
            var inputText = inputTextGO.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 12;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            input.textComponent = inputText;
            input.text = defaultVal.ToString("F4");
            input.contentType = InputField.ContentType.DecimalNumber;

            input.onEndEdit.AddListener(v => OnInputChanged(capturedAxis, v, capturedType));

            // Store references
            switch (type)
            {
                case SliderType.Position: _posSliders[axis] = slider; _posInputs[axis] = input; break;
                case SliderType.Rotation: _rotSliders[axis] = slider; _rotInputs[axis] = input; break;
                case SliderType.Scale:    _scaleSliders[axis] = slider; _scaleInputs[axis] = input; break;
            }

            return row;
        }

        private void CreateButton(Transform parent, string label, float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.4f, 0.7f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 11;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
