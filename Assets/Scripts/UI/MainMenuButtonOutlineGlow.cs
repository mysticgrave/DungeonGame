using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DungeonGame.UI
{
    /// <summary>
    /// Glows only the outlines of the button background and text on hover (no fill).
    /// Adds or uses Unity's Outline component on the Image and on any Text/Graphic children.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MainMenuButtonOutlineGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Color glowColor = new Color(0.3f, 0.6f, 1f, 1f);
        [Tooltip("Outline thickness in pixels. Use 4–6 if you don't see anything.")]
        [SerializeField] private Vector2 outlineDistance = new Vector2(5f, 5f);
        [Tooltip("Log when hover starts/ends to confirm events are firing.")]
        [SerializeField] private bool debugLog;

        private Outline[] _outlines;
        private Graphic[] _graphics;

        private void Awake()
        {
            CacheOutlines();
        }

        private void CacheOutlines()
        {
            var graphics = GetComponentsInChildren<Graphic>(true);
            var list = new System.Collections.Generic.List<Outline>();
            var graphicList = new System.Collections.Generic.List<Graphic>();
            foreach (var g in graphics)
            {
                var outline = g.GetComponent<Outline>();
                if (outline == null)
                    outline = g.gameObject.AddComponent<Outline>();
                outline.enabled = false;
                list.Add(outline);
                graphicList.Add(g);
            }
            _outlines = list.ToArray();
            _graphics = graphicList.ToArray();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (debugLog) Debug.Log($"[OutlineGlow] Enter {gameObject.name}");
            if (_outlines == null) CacheOutlines();
            for (int i = 0; i < _outlines.Length; i++)
            {
                var o = _outlines[i];
                if (o == null) continue;
                o.effectColor = glowColor;
                o.effectDistance = outlineDistance;
                o.enabled = true;
                if (i < _graphics.Length && _graphics[i] != null)
                    _graphics[i].SetVerticesDirty();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (debugLog) Debug.Log($"[OutlineGlow] Exit {gameObject.name}");
            if (_outlines == null) return;
            for (int i = 0; i < _outlines.Length; i++)
            {
                var o = _outlines[i];
                if (o == null) continue;
                o.enabled = false;
                if (i < _graphics.Length && _graphics[i] != null)
                    _graphics[i].SetVerticesDirty();
            }
        }
    }
}
