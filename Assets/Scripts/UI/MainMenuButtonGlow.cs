using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DungeonGame.UI
{
    /// <summary>
    /// Optional: attach to a UI Button for a simple hover glow (brightens the button Image).
    /// Assign Glow Color or leave default; on hover the image tints toward that color.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MainMenuButtonGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Color glowColor = new Color(0.6f, 0.85f, 1f);
        [SerializeField] private float glowStrength = 0.4f;

        private Image _image;
        private Color _normalColor;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null)
                _normalColor = _image.color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_image == null) return;
            _image.color = Color.Lerp(_normalColor, glowColor, glowStrength);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_image == null) return;
            _image.color = _normalColor;
        }
    }
}
