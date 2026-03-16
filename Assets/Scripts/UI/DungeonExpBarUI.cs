using DungeonGame.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// In-dungeon only EXP bar. Shows current exp / exp to next level.
    /// Subscribe to DungeonRunExp.OnExpChanged. Resets when run ends (handled by DungeonRunExp).
    /// </summary>
    public class DungeonExpBarUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Slider expSlider;
        [SerializeField] private Image fillImage;
        [Tooltip("Optional: \"Lvl 2\" or \"5 / 25\"")]
        [SerializeField] private Text labelText;
        [Tooltip("Hide this root when not in dungeon")]
        [SerializeField] private GameObject root;

        private DungeonRunExp _runExp;

        private void Start()
        {
            _runExp = FindFirstObjectByType<DungeonRunExp>();
            if (_runExp != null)
                _runExp.OnExpChanged += OnExpChanged;

            UpdateBar(0, 1, 25);
        }

        private void OnDestroy()
        {
            if (_runExp != null)
                _runExp.OnExpChanged -= OnExpChanged;
        }

        private void OnExpChanged(int exp, int level)
        {
            int expToNext = _runExp != null ? _runExp.GetExpToNextLevel(exp) : 25;
            int expInLevel = _runExp != null ? _runExp.GetExpInCurrentLevel(exp) : 0;

            UpdateBar(expInLevel, level, expToNext);
        }

        private void UpdateBar(int expInLevel, int level, int expToNext)
        {
            float fill = expToNext > 0 ? (float)expInLevel / expToNext : 0f;

            if (expSlider != null)
            {
                expSlider.minValue = 0;
                expSlider.maxValue = 1;
                expSlider.value = fill;
            }

            if (fillImage != null)
                fillImage.fillAmount = fill;

            if (labelText != null)
                labelText.text = $"Lvl {level}  {expInLevel}/{expToNext}";

            if (root != null)
                root.SetActive(true);
        }
    }
}
