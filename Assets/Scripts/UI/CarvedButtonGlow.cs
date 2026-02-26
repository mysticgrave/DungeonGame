using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace DungeonGame.UI
{
    /// <summary>
    /// Attach to a 3D button (mesh with Collider). On hover: glow via material emission. On click: invoke event.
    /// Use with a main menu where buttons are carved into a wall. Assign a material that supports emission (e.g. Standard or URP with Emission).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CarvedButtonGlow : MonoBehaviour
    {
        [Header("Glow")]
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Emission color when hovered (e.g. cyan/gold for magic).")]
        [SerializeField] private Color glowColor = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private float glowIntensity = 1.2f;
        [SerializeField] private float glowLerpSpeed = 8f;

        [Header("Click")]
        [SerializeField] private UnityEvent onClick;

        private Material _instanceMat;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private bool _hovered;
        private float _currentIntensity;

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
                _instanceMat = targetRenderer.material;
        }

        private void OnDestroy()
        {
            if (_instanceMat != null)
                Destroy(_instanceMat);
        }

        private void Update()
        {
            if (_instanceMat == null) return;

            float target = _hovered ? glowIntensity : 0f;
            _currentIntensity = Mathf.Lerp(_currentIntensity, target, glowLerpSpeed * Time.deltaTime);

            Color emission = glowColor * _currentIntensity;
            _instanceMat.SetColor(EmissionColorId, emission);
            if (_currentIntensity > 0.01f)
                _instanceMat.EnableKeyword("_EMISSION");
        }

        private void OnMouseEnter()
        {
            if (IsPointerOverUI()) return;
            _hovered = true;
        }

        private void OnMouseExit()
        {
            _hovered = false;
        }

        private void OnMouseDown()
        {
            if (IsPointerOverUI()) return;
            onClick?.Invoke();
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
