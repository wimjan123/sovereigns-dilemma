using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;
using SovereignsDilemma.UI.Core;

namespace SovereignsDilemma.UI.Panels
{
    /// <summary>
    /// Real-time voter analytics visualization panel.
    /// Displays demographic distributions, engagement metrics, and voter behavior patterns.
    /// </summary>
    public class VoterAnalyticsPanel : MonoBehaviour, IUIComponent
    {
        [Header("UI References")]
        [SerializeField] private Text totalVotersText;
        [SerializeField] private Text activeVotersText;
        [SerializeField] private Text dormantVotersText;
        [SerializeField] private Slider engagementSlider;
        [SerializeField] private Slider socialInfluenceSlider;

        [Header("Demographic Charts")]
        [SerializeField] private PieChart ageDistributionChart;
        [SerializeField] private BarChart educationDistributionChart;
        [SerializeField] private BarChart incomeDistributionChart;

        [Header("Visual Configuration")]
        [SerializeField] private bool enableAnimations = true;
        [SerializeField] private float animationDuration = 0.5f;
        [SerializeField] private bool compactMode = false;
        [SerializeField] private GameObject compactView;
        [SerializeField] private GameObject fullView;

        [Header("Color Scheme")]
        [SerializeField] private Color[] ageColors = new Color[5];
        [SerializeField] private Color[] educationColors = new Color[4];
        [SerializeField] private Color[] incomeColors = new Color[3];
        [SerializeField] private Color engagementColor = Color.green;
        [SerializeField] private Color socialInfluenceColor = Color.blue;

        // Data management
        private VoterAnalyticsData _currentData;
        private VoterAnalyticsData _previousData;
        private bool _hasValidData;

        // Performance tracking
        private readonly ProfilerMarker _updateMarker = new("VoterAnalyticsPanel.Update");
        private float _lastUpdateTime;
        private const float MIN_UPDATE_INTERVAL = 0.1f;

        // Animation system
        private readonly List<UIAnimation> _activeAnimations = new List<UIAnimation>();

        private void Awake()
        {
            InitializeColorSchemes();
            SetupChartConfigurations();
            ValidateReferences();
        }

        private void Start()
        {
            InitializeCharts();
            SetCompactMode(compactMode);
        }

        private void Update()
        {
            if (enableAnimations)
            {
                UpdateAnimations();
            }
        }

        #region Initialization

        private void InitializeColorSchemes()
        {
            // Default age distribution colors (if not set in inspector)
            if (ageColors.Length == 0 || ageColors[0] == Color.clear)
            {
                ageColors = new Color[]
                {
                    new Color(0.2f, 0.8f, 0.2f), // Young: Green
                    new Color(0.4f, 0.9f, 0.4f), // Young Adult: Light Green
                    new Color(0.8f, 0.8f, 0.2f), // Middle: Yellow
                    new Color(0.9f, 0.6f, 0.2f), // Mature: Orange
                    new Color(0.8f, 0.2f, 0.2f)  // Senior: Red
                };
            }

            // Default education colors
            if (educationColors.Length == 0 || educationColors[0] == Color.clear)
            {
                educationColors = new Color[]
                {
                    new Color(0.6f, 0.3f, 0.8f), // Primary: Purple
                    new Color(0.3f, 0.5f, 0.9f), // Secondary: Blue
                    new Color(0.2f, 0.7f, 0.9f), // Vocational: Cyan
                    new Color(0.9f, 0.7f, 0.2f)  // Higher: Gold
                };
            }

            // Default income colors
            if (incomeColors.Length == 0 || incomeColors[0] == Color.clear)
            {
                incomeColors = new Color[]
                {
                    new Color(0.8f, 0.4f, 0.4f), // Low: Light Red
                    new Color(0.4f, 0.8f, 0.4f), // Middle: Light Green
                    new Color(0.4f, 0.4f, 0.8f)  // High: Light Blue
                };
            }
        }

        private void SetupChartConfigurations()
        {
            // Configure pie chart for age distribution
            if (ageDistributionChart)
            {
                ageDistributionChart.SetTitle("Age Distribution");
                ageDistributionChart.SetColors(ageColors);
                ageDistributionChart.SetLabels(new[] { "18-25", "26-35", "36-50", "51-65", "65+" });
            }

            // Configure bar chart for education
            if (educationDistributionChart)
            {
                educationDistributionChart.SetTitle("Education Levels");
                educationDistributionChart.SetColors(educationColors);
                educationDistributionChart.SetLabels(new[] { "Primary", "Secondary", "Vocational", "Higher" });
            }

            // Configure bar chart for income
            if (incomeDistributionChart)
            {
                incomeDistributionChart.SetTitle("Income Distribution");
                incomeDistributionChart.SetColors(incomeColors);
                incomeDistributionChart.SetLabels(new[] { "Low", "Middle", "High" });
            }
        }

        private void ValidateReferences()
        {
            if (!totalVotersText) Debug.LogWarning("Total voters text reference missing");
            if (!activeVotersText) Debug.LogWarning("Active voters text reference missing");
            if (!dormantVotersText) Debug.LogWarning("Dormant voters text reference missing");
            if (!engagementSlider) Debug.LogWarning("Engagement slider reference missing");
            if (!socialInfluenceSlider) Debug.LogWarning("Social influence slider reference missing");
        }

        private void InitializeCharts()
        {
            // Initialize charts with placeholder data
            var placeholderAgeData = new float[] { 0.2f, 0.3f, 0.25f, 0.15f, 0.1f };
            var placeholderEducationData = new float[] { 0.1f, 0.4f, 0.3f, 0.2f };
            var placeholderIncomeData = new float[] { 0.3f, 0.5f, 0.2f };

            if (ageDistributionChart)
                ageDistributionChart.SetData(placeholderAgeData);
            if (educationDistributionChart)
                educationDistributionChart.SetData(placeholderEducationData);
            if (incomeDistributionChart)
                incomeDistributionChart.SetData(placeholderIncomeData);
        }

        #endregion

        #region IUIComponent Implementation

        public Type GetDataType()
        {
            return typeof(VoterAnalyticsData);
        }

        public void UpdateData(object data)
        {
            if (data is VoterAnalyticsData voterData)
            {
                UpdateData(voterData);
            }
        }

        public bool IsVisible()
        {
            return gameObject.activeInHierarchy;
        }

        public void SetVisibility(bool visible)
        {
            gameObject.SetActive(visible);
        }

        #endregion

        #region Data Updates

        public void UpdateData(VoterAnalyticsData data)
        {
            if (Time.time - _lastUpdateTime < MIN_UPDATE_INTERVAL) return;

            using (_updateMarker.Auto())
            {
                _previousData = _currentData;
                _currentData = data;
                _hasValidData = data.HasData;

                if (_hasValidData)
                {
                    UpdateVoterCounts();
                    UpdateEngagementMetrics();
                    UpdateDemographicCharts();
                }

                _lastUpdateTime = Time.time;
            }
        }

        private void UpdateVoterCounts()
        {
            var data = _currentData;

            if (totalVotersText)
            {
                var targetValue = data.TotalVoters;
                if (enableAnimations && _previousData.HasData)
                {
                    AnimateTextValue(totalVotersText, _previousData.TotalVoters, targetValue, "Total: {0:N0}");
                }
                else
                {
                    totalVotersText.text = $"Total: {targetValue:N0}";
                }
            }

            if (activeVotersText)
            {
                var targetValue = data.ActiveVoters;
                if (enableAnimations && _previousData.HasData)
                {
                    AnimateTextValue(activeVotersText, _previousData.ActiveVoters, targetValue, "Active: {0:N0}");
                }
                else
                {
                    activeVotersText.text = $"Active: {targetValue:N0}";
                }
            }

            if (dormantVotersText)
            {
                var targetValue = data.DormantVoters;
                if (enableAnimations && _previousData.HasData)
                {
                    AnimateTextValue(dormantVotersText, _previousData.DormantVoters, targetValue, "Dormant: {0:N0}");
                }
                else
                {
                    dormantVotersText.text = $"Dormant: {targetValue:N0}";
                }
            }
        }

        private void UpdateEngagementMetrics()
        {
            var data = _currentData;

            if (engagementSlider)
            {
                var targetValue = data.AveragePoliticalEngagement;
                if (enableAnimations && _previousData.HasData)
                {
                    AnimateSliderValue(engagementSlider, _previousData.AveragePoliticalEngagement, targetValue);
                }
                else
                {
                    engagementSlider.value = targetValue;
                }
            }

            if (socialInfluenceSlider)
            {
                var targetValue = data.AverageSocialInfluence;
                if (enableAnimations && _previousData.HasData)
                {
                    AnimateSliderValue(socialInfluenceSlider, _previousData.AverageSocialInfluence, targetValue);
                }
                else
                {
                    socialInfluenceSlider.value = targetValue;
                }
            }
        }

        private void UpdateDemographicCharts()
        {
            var data = _currentData;

            // Update age distribution chart
            if (ageDistributionChart && data.AgeDistribution != null && data.AgeDistribution.Length >= 5)
            {
                if (enableAnimations && _previousData.HasData && _previousData.AgeDistribution != null)
                {
                    ageDistributionChart.AnimateToData(data.AgeDistribution, animationDuration);
                }
                else
                {
                    ageDistributionChart.SetData(data.AgeDistribution);
                }
            }

            // Update education distribution chart
            if (educationDistributionChart && data.EducationDistribution != null && data.EducationDistribution.Length >= 4)
            {
                if (enableAnimations && _previousData.HasData && _previousData.EducationDistribution != null)
                {
                    educationDistributionChart.AnimateToData(data.EducationDistribution, animationDuration);
                }
                else
                {
                    educationDistributionChart.SetData(data.EducationDistribution);
                }
            }

            // Update income distribution chart
            if (incomeDistributionChart && data.IncomeDistribution != null && data.IncomeDistribution.Length >= 3)
            {
                if (enableAnimations && _previousData.HasData && _previousData.IncomeDistribution != null)
                {
                    incomeDistributionChart.AnimateToData(data.IncomeDistribution, animationDuration);
                }
                else
                {
                    incomeDistributionChart.SetData(data.IncomeDistribution);
                }
            }
        }

        #endregion

        #region Animation System

        private void AnimateTextValue(Text textComponent, float startValue, float endValue, string format)
        {
            var animation = new UIAnimation
            {
                AnimationType = UIAnimationType.TextValue,
                Target = textComponent,
                StartValue = startValue,
                EndValue = endValue,
                Duration = animationDuration,
                StartTime = Time.time,
                Format = format
            };

            _activeAnimations.Add(animation);
        }

        private void AnimateSliderValue(Slider slider, float startValue, float endValue)
        {
            var animation = new UIAnimation
            {
                AnimationType = UIAnimationType.SliderValue,
                Target = slider,
                StartValue = startValue,
                EndValue = endValue,
                Duration = animationDuration,
                StartTime = Time.time
            };

            _activeAnimations.Add(animation);
        }

        private void UpdateAnimations()
        {
            for (int i = _activeAnimations.Count - 1; i >= 0; i--)
            {
                var animation = _activeAnimations[i];
                var elapsed = Time.time - animation.StartTime;
                var progress = Mathf.Clamp01(elapsed / animation.Duration);

                // Apply easing
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                var currentValue = Mathf.Lerp(animation.StartValue, animation.EndValue, easedProgress);

                // Update target based on animation type
                switch (animation.AnimationType)
                {
                    case UIAnimationType.TextValue:
                        if (animation.Target is Text text)
                        {
                            text.text = string.Format(animation.Format, currentValue);
                        }
                        break;

                    case UIAnimationType.SliderValue:
                        if (animation.Target is Slider slider)
                        {
                            slider.value = currentValue;
                        }
                        break;
                }

                // Remove completed animations
                if (progress >= 1f)
                {
                    _activeAnimations.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Layout and Display Modes

        public void SetCompactMode(bool compact)
        {
            compactMode = compact;

            if (compactView && fullView)
            {
                compactView.SetActive(compact);
                fullView.SetActive(!compact);
            }

            // Adjust chart sizes and detail levels
            if (compact)
            {
                if (ageDistributionChart) ageDistributionChart.SetCompactMode(true);
                if (educationDistributionChart) educationDistributionChart.SetCompactMode(true);
                if (incomeDistributionChart) incomeDistributionChart.SetCompactMode(true);
            }
            else
            {
                if (ageDistributionChart) ageDistributionChart.SetCompactMode(false);
                if (educationDistributionChart) educationDistributionChart.SetCompactMode(false);
                if (incomeDistributionChart) incomeDistributionChart.SetCompactMode(false);
            }

            Debug.Log($"VoterAnalyticsPanel compact mode: {compact}");
        }

        public void SetAnimationsEnabled(bool enabled)
        {
            enableAnimations = enabled;

            // Stop current animations if disabling
            if (!enabled)
            {
                _activeAnimations.Clear();
            }
        }

        public void SetAnimationDuration(float duration)
        {
            animationDuration = Mathf.Clamp(duration, 0.1f, 2f);
        }

        #endregion

        #region Accessibility

        public void EnableAccessibilityMode(bool enabled)
        {
            // Adjust for screen readers and high contrast
            if (enabled)
            {
                // Increase font sizes
                if (totalVotersText) totalVotersText.fontSize = Mathf.RoundToInt(totalVotersText.fontSize * 1.2f);
                if (activeVotersText) activeVotersText.fontSize = Mathf.RoundToInt(activeVotersText.fontSize * 1.2f);
                if (dormantVotersText) dormantVotersText.fontSize = Mathf.RoundToInt(dormantVotersText.fontSize * 1.2f);

                // Simplify visualizations
                if (ageDistributionChart) ageDistributionChart.EnableAccessibilityMode(true);
                if (educationDistributionChart) educationDistributionChart.EnableAccessibilityMode(true);
                if (incomeDistributionChart) incomeDistributionChart.EnableAccessibilityMode(true);
            }
        }

        public void EnableHighContrastMode(bool enabled)
        {
            if (enabled)
            {
                // Apply high contrast color scheme
                var highContrastColors = new Color[]
                {
                    Color.white, Color.black, Color.yellow, Color.cyan, Color.magenta
                };

                if (ageDistributionChart) ageDistributionChart.SetColors(highContrastColors);
            }
            else
            {
                // Restore original colors
                if (ageDistributionChart) ageDistributionChart.SetColors(ageColors);
                if (educationDistributionChart) educationDistributionChart.SetColors(educationColors);
                if (incomeDistributionChart) incomeDistributionChart.SetColors(incomeColors);
            }
        }

        #endregion

        #region Public Interface

        public VoterAnalyticsData GetCurrentData()
        {
            return _currentData;
        }

        public bool HasValidData()
        {
            return _hasValidData;
        }

        public void RefreshDisplay()
        {
            if (_hasValidData)
            {
                UpdateData(_currentData);
            }
        }

        public void ResetToDefaults()
        {
            _hasValidData = false;
            _currentData = new VoterAnalyticsData(true);
            _previousData = new VoterAnalyticsData(true);

            // Reset UI elements
            if (totalVotersText) totalVotersText.text = "Total: 0";
            if (activeVotersText) activeVotersText.text = "Active: 0";
            if (dormantVotersText) dormantVotersText.text = "Dormant: 0";
            if (engagementSlider) engagementSlider.value = 0f;
            if (socialInfluenceSlider) socialInfluenceSlider.value = 0f;

            // Reset charts
            InitializeCharts();
        }

        #endregion
    }

    // Supporting animation system
    public class UIAnimation
    {
        public UIAnimationType AnimationType;
        public object Target;
        public float StartValue;
        public float EndValue;
        public float Duration;
        public float StartTime;
        public string Format = "{0}";
    }

    public enum UIAnimationType
    {
        TextValue,
        SliderValue,
        ChartData,
        Color,
        Scale
    }
}