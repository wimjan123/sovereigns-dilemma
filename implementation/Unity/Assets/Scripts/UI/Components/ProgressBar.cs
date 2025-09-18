using UnityEngine;
using UnityEngine.UI;

namespace SovereignsDilemma.UI.Components
{
    /// <summary>
    /// Simple progress bar component with smooth animation.
    /// </summary>
    public class ProgressBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text progressText;
        [SerializeField] private float animationSpeed = 2f;
        [SerializeField] private bool showPercentage = true;

        private float _currentProgress = 0f;
        private float _targetProgress = 0f;

        private void Update()
        {
            if (Mathf.Abs(_currentProgress - _targetProgress) > 0.01f)
            {
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * animationSpeed);
                UpdateDisplay();
            }
        }

        public void SetProgress(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
        }

        public void SetProgressImmediate(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
            _currentProgress = _targetProgress;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (fillImage)
                fillImage.fillAmount = _currentProgress;

            if (progressText && showPercentage)
                progressText.text = $"{_currentProgress * 100:F0}%";
        }
    }
}