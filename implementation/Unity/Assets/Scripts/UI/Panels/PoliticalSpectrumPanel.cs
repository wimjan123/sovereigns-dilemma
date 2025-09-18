using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SovereignsDilemma.UI.Core;
using SovereignsDilemma.UI.Components;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Panels
{
    /// <summary>
    /// Political spectrum visualization panel showing economic, social, and environmental distributions.
    /// Displays interactive spectrum charts with party positions and voter clustering.
    /// </summary>
    public class PoliticalSpectrumPanel : MonoBehaviour, IUIComponent
    {
        [Header("Spectrum Charts")]
        [SerializeField] private BarChart economicSpectrumChart;
        [SerializeField] private BarChart socialSpectrumChart;
        [SerializeField] private BarChart environmentalSpectrumChart;

        [Header("Heatmap Visualization")]
        [SerializeField] private Image politicalHeatmap;
        [SerializeField] private Texture2D heatmapTexture;
        [SerializeField] private int heatmapWidth = 100;
        [SerializeField] private int heatmapHeight = 100;

        [Header("Party Support")]
        [SerializeField] private PieChart partySupportChart;
        [SerializeField] private Transform partyListContainer;
        [SerializeField] private GameObject partyItemPrefab;

        [Header("Tension Indicator")]
        [SerializeField] private Slider politicalTensionSlider;
        [SerializeField] private Text tensionValueText;
        [SerializeField] private Image tensionIndicator;
        [SerializeField] private Color lowTensionColor = Color.green;
        [SerializeField] private Color highTensionColor = Color.red;

        [Header("Median Position")]
        [SerializeField] private RectTransform medianPositionIndicator;
        [SerializeField] private Text medianPositionText;

        [Header("UI Elements")]
        [SerializeField] private Text panelTitle;
        [SerializeField] private Text lastUpdatedText;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Toggle compactModeToggle;

        [Header("Animation Settings")]
        [SerializeField] private float updateAnimationDuration = 1.0f;
        [SerializeField] private AnimationCurve updateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Data state
        private PoliticalSpectrumData _currentData;
        private bool _hasData = false;
        private bool _isVisible = true;

        // Animation state
        private bool _isAnimating = false;
        private float _animationStartTime;

        // Party UI management
        private readonly List<GameObject> _partyUIItems = new List<GameObject>();

        // Performance tracking
        private readonly ProfilerMarker _updateMarker = new("PoliticalSpectrumPanel.Update");
        private readonly ProfilerMarker _heatmapMarker = new("PoliticalSpectrumPanel.Heatmap");

        // Spectrum labels
        private readonly string[] _economicLabels = {
            "Socialist", "Social Dem", "Center-Left", "Centrist", "Center-Right",
            "Conservative", "Free Market", "Libertarian", "Neo-Liberal", "Anarchist"
        };

        private readonly string[] _socialLabels = {
            "Progressive", "Liberal", "Moderate Lib", "Centrist", "Moderate Con",
            "Conservative", "Traditional", "Religious", "Authoritarian", "Reactionary"
        };

        private readonly string[] _environmentalLabels = {
            "Deep Green", "Green", "Eco-Friendly", "Moderate", "Skeptical",
            "Business First", "Development", "Growth Focus", "Anti-Regulation", "Denial"
        };

        public Type GetDataType() => typeof(PoliticalSpectrumData);
        public bool IsVisible() => _isVisible;

        private void Awake()
        {
            InitializePanel();
            SetupEventHandlers();
        }

        private void Start()
        {
            InitializeCharts();
            InitializeHeatmap();
        }

        private void Update()
        {
            using (_updateMarker.Auto())
            {
                if (_isAnimating)
                {
                    UpdateAnimations();
                }
            }
        }

        #region Initialization

        private void InitializePanel()
        {
            if (panelTitle)
                panelTitle.text = "Political Spectrum Analysis";

            if (politicalTensionSlider)
            {
                politicalTensionSlider.minValue = 0f;
                politicalTensionSlider.maxValue = 1f;
                politicalTensionSlider.value = 0f;
            }

            // Initialize with default data for preview
            _currentData = new PoliticalSpectrumData(true);
            GenerateDefaultData();
        }

        private void SetupEventHandlers()
        {
            if (refreshButton)
                refreshButton.onClick.AddListener(RequestDataRefresh);

            if (compactModeToggle)
                compactModeToggle.onValueChanged.AddListener(SetCompactMode);
        }

        private void InitializeCharts()
        {
            // Initialize economic spectrum chart
            if (economicSpectrumChart)
            {
                economicSpectrumChart.SetTitle("Economic Spectrum");
                economicSpectrumChart.SetAxisLabels("Position", "Voter %");
                economicSpectrumChart.SetLabels(_economicLabels);
                economicSpectrumChart.SetColors(GenerateSpectrumColors(10, Color.blue, Color.red));
            }

            // Initialize social spectrum chart
            if (socialSpectrumChart)
            {
                socialSpectrumChart.SetTitle("Social Spectrum");
                socialSpectrumChart.SetAxisLabels("Position", "Voter %");
                socialSpectrumChart.SetLabels(_socialLabels);
                socialSpectrumChart.SetColors(GenerateSpectrumColors(10, Color.magenta, Color.cyan));
            }

            // Initialize environmental spectrum chart
            if (environmentalSpectrumChart)
            {
                environmentalSpectrumChart.SetTitle("Environmental Spectrum");
                environmentalSpectrumChart.SetAxisLabels("Position", "Voter %");
                environmentalSpectrumChart.SetLabels(_environmentalLabels);
                environmentalSpectrumChart.SetColors(GenerateSpectrumColors(10, Color.green, Color.yellow));
            }

            // Initialize party support chart
            if (partySupportChart)
            {
                partySupportChart.SetTitle("Party Support");
            }
        }

        private void InitializeHeatmap()
        {
            if (!politicalHeatmap) return;

            heatmapTexture = new Texture2D(heatmapWidth, heatmapHeight, TextureFormat.RGBA32, false);
            heatmapTexture.filterMode = FilterMode.Bilinear;

            // Create initial empty heatmap
            for (int x = 0; x < heatmapWidth; x++)
            {
                for (int y = 0; y < heatmapHeight; y++)
                {
                    heatmapTexture.SetPixel(x, y, Color.clear);
                }
            }
            heatmapTexture.Apply();

            Sprite heatmapSprite = Sprite.Create(heatmapTexture,
                new Rect(0, 0, heatmapWidth, heatmapHeight),
                new Vector2(0.5f, 0.5f));
            politicalHeatmap.sprite = heatmapSprite;
        }

        private void GenerateDefaultData()
        {
            // Generate sample data for preview
            _currentData.EconomicSpectrum = GenerateNormalDistribution(10, 5, 2);
            _currentData.SocialSpectrum = GenerateNormalDistribution(10, 6, 1.5f);
            _currentData.EnvironmentalSpectrum = GenerateNormalDistribution(10, 4, 2.5f);
            _currentData.PoliticalTension = 0.3f;
            _currentData.MedianPosition = new Vector2(0.5f, 0.6f);

            // Add some default parties
            _currentData.PartySupport = new Dictionary<string, float>
            {
                { "Progressive Alliance", 0.28f },
                { "Conservative Party", 0.32f },
                { "Green Movement", 0.18f },
                { "Liberal Democrats", 0.15f },
                { "Independent", 0.07f }
            };

            _currentData.HasData = true;
            _currentData.LastUpdated = DateTime.UtcNow;
        }

        #endregion

        #region Public Interface

        public void UpdateData(object data)
        {
            if (data is PoliticalSpectrumData spectrumData)
            {
                UpdateData(spectrumData);
            }
        }

        public void UpdateData(PoliticalSpectrumData data)
        {
            _currentData = data;
            _hasData = data.HasData;

            if (_hasData)
            {
                StartUpdateAnimation();
                UpdateLastUpdatedText();
            }
        }

        public void SetVisibility(bool visible)
        {
            _isVisible = visible;
            gameObject.SetActive(visible);
        }

        public void SetCompactMode(bool compact)
        {
            if (economicSpectrumChart) economicSpectrumChart.SetCompactMode(compact);
            if (socialSpectrumChart) socialSpectrumChart.SetCompactMode(compact);
            if (environmentalSpectrumChart) environmentalSpectrumChart.SetCompactMode(compact);
            if (partySupportChart) partySupportChart.SetCompactMode(compact);

            // Hide detailed elements in compact mode
            if (partyListContainer) partyListContainer.gameObject.SetActive(!compact);
            if (medianPositionText) medianPositionText.gameObject.SetActive(!compact);
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            if (economicSpectrumChart) economicSpectrumChart.EnableAccessibilityMode(enabled);
            if (socialSpectrumChart) socialSpectrumChart.EnableAccessibilityMode(enabled);
            if (environmentalSpectrumChart) environmentalSpectrumChart.EnableAccessibilityMode(enabled);
            if (partySupportChart) partySupportChart.EnableAccessibilityMode(enabled);
        }

        #endregion

        #region Update Methods

        private void StartUpdateAnimation()
        {
            _isAnimating = true;
            _animationStartTime = Time.time;
            UpdateAllVisualization();
        }

        private void UpdateAnimations()
        {
            float elapsed = Time.time - _animationStartTime;
            float progress = Mathf.Clamp01(elapsed / updateAnimationDuration);

            if (progress >= 1f)
            {
                _isAnimating = false;
            }
        }

        private void UpdateAllVisualization()
        {
            if (!_hasData) return;

            UpdateSpectrumCharts();
            UpdatePartySupportChart();
            UpdatePartyList();
            UpdatePoliticalTension();
            UpdateMedianPosition();
            UpdateHeatmap();
        }

        private void UpdateSpectrumCharts()
        {
            if (economicSpectrumChart && _currentData.EconomicSpectrum != null)
            {
                economicSpectrumChart.AnimateToData(_currentData.EconomicSpectrum, updateAnimationDuration);
            }

            if (socialSpectrumChart && _currentData.SocialSpectrum != null)
            {
                socialSpectrumChart.AnimateToData(_currentData.SocialSpectrum, updateAnimationDuration);
            }

            if (environmentalSpectrumChart && _currentData.EnvironmentalSpectrum != null)
            {
                environmentalSpectrumChart.AnimateToData(_currentData.EnvironmentalSpectrum, updateAnimationDuration);
            }
        }

        private void UpdatePartySupportChart()
        {
            if (!partySupportChart || _currentData.PartySupport == null) return;

            var partyNames = new List<string>();
            var supportValues = new List<float>();
            var partyColors = new List<Color>();

            foreach (var party in _currentData.PartySupport)
            {
                partyNames.Add(party.Key);
                supportValues.Add(party.Value);
                partyColors.Add(GetPartyColor(party.Key));
            }

            partySupportChart.SetLabels(partyNames.ToArray());
            partySupportChart.SetColors(partyColors.ToArray());
            partySupportChart.AnimateToData(supportValues.ToArray(), updateAnimationDuration);
        }

        private void UpdatePartyList()
        {
            if (!partyListContainer || _currentData.PartySupport == null) return;

            ClearPartyList();

            foreach (var party in _currentData.PartySupport)
            {
                CreatePartyListItem(party.Key, party.Value);
            }
        }

        private void UpdatePoliticalTension()
        {
            if (!politicalTensionSlider) return;

            if (_isAnimating)
            {
                float elapsed = Time.time - _animationStartTime;
                float progress = Mathf.Clamp01(elapsed / updateAnimationDuration);
                float easedProgress = updateCurve.Evaluate(progress);

                float targetTension = _currentData.PoliticalTension;
                float currentTension = Mathf.Lerp(politicalTensionSlider.value, targetTension, easedProgress);
                politicalTensionSlider.value = currentTension;

                if (tensionValueText)
                    tensionValueText.text = $"{currentTension:P1}";

                if (tensionIndicator)
                    tensionIndicator.color = Color.Lerp(lowTensionColor, highTensionColor, currentTension);
            }
            else
            {
                politicalTensionSlider.value = _currentData.PoliticalTension;

                if (tensionValueText)
                    tensionValueText.text = $"{_currentData.PoliticalTension:P1}";

                if (tensionIndicator)
                    tensionIndicator.color = Color.Lerp(lowTensionColor, highTensionColor, _currentData.PoliticalTension);
            }
        }

        private void UpdateMedianPosition()
        {
            if (!medianPositionIndicator) return;

            Vector2 position = _currentData.MedianPosition;

            // Convert normalized position to UI position
            var rect = medianPositionIndicator.parent.GetComponent<RectTransform>();
            if (rect)
            {
                Vector2 uiPosition = new Vector2(
                    rect.rect.width * position.x,
                    rect.rect.height * position.y
                );
                medianPositionIndicator.anchoredPosition = uiPosition;
            }

            if (medianPositionText)
            {
                medianPositionText.text = $"Median: ({position.x:F2}, {position.y:F2})";
            }
        }

        private void UpdateHeatmap()
        {
            if (!politicalHeatmap || !heatmapTexture) return;

            using (_heatmapMarker.Auto())
            {
                GenerateHeatmapTexture();
            }
        }

        #endregion

        #region Heatmap Generation

        private void GenerateHeatmapTexture()
        {
            // Generate 2D political distribution based on economic and social spectra
            for (int x = 0; x < heatmapWidth; x++)
            {
                for (int y = 0; y < heatmapHeight; y++)
                {
                    float normalizedX = (float)x / (heatmapWidth - 1);
                    float normalizedY = (float)y / (heatmapHeight - 1);

                    float density = CalculatePoliticalDensity(normalizedX, normalizedY);
                    Color pixelColor = GetHeatmapColor(density);

                    heatmapTexture.SetPixel(x, y, pixelColor);
                }
            }

            heatmapTexture.Apply();
        }

        private float CalculatePoliticalDensity(float economicPosition, float socialPosition)
        {
            // Sample from both economic and social spectra to create 2D density
            float economicDensity = SampleSpectrum(_currentData.EconomicSpectrum, economicPosition);
            float socialDensity = SampleSpectrum(_currentData.SocialSpectrum, socialPosition);

            // Combine densities (you could use different combination methods)
            return (economicDensity + socialDensity) * 0.5f;
        }

        private float SampleSpectrum(float[] spectrum, float position)
        {
            if (spectrum == null || spectrum.Length == 0) return 0f;

            float floatIndex = position * (spectrum.Length - 1);
            int index = Mathf.FloorToInt(floatIndex);
            float fraction = floatIndex - index;

            if (index >= spectrum.Length - 1)
                return spectrum[spectrum.Length - 1];

            return Mathf.Lerp(spectrum[index], spectrum[index + 1], fraction);
        }

        private Color GetHeatmapColor(float density)
        {
            // Create a heat gradient from blue (low) to red (high)
            if (density <= 0f) return Color.clear;

            Color[] heatColors = {
                new Color(0, 0, 0.5f, 0.3f),    // Dark blue
                new Color(0, 0, 1f, 0.5f),      // Blue
                new Color(0, 1f, 1f, 0.7f),     // Cyan
                new Color(0, 1f, 0, 0.8f),      // Green
                new Color(1f, 1f, 0, 0.9f),     // Yellow
                new Color(1f, 0.5f, 0, 0.95f),  // Orange
                new Color(1f, 0, 0, 1f)         // Red
            };

            float scaledDensity = Mathf.Clamp01(density * 3f); // Amplify for visibility
            float colorIndex = scaledDensity * (heatColors.Length - 1);
            int index = Mathf.FloorToInt(colorIndex);
            float fraction = colorIndex - index;

            if (index >= heatColors.Length - 1)
                return heatColors[heatColors.Length - 1];

            return Color.Lerp(heatColors[index], heatColors[index + 1], fraction);
        }

        #endregion

        #region UI Management

        private void CreatePartyListItem(string partyName, float support)
        {
            if (!partyItemPrefab || !partyListContainer) return;

            GameObject partyItem = Instantiate(partyItemPrefab, partyListContainer);

            // Find UI components in the prefab
            Text nameText = partyItem.transform.Find("PartyName")?.GetComponent<Text>();
            Text supportText = partyItem.transform.Find("SupportPercentage")?.GetComponent<Text>();
            Image colorIndicator = partyItem.transform.Find("ColorIndicator")?.GetComponent<Image>();

            if (nameText) nameText.text = partyName;
            if (supportText) supportText.text = $"{support:P1}";
            if (colorIndicator) colorIndicator.color = GetPartyColor(partyName);

            _partyUIItems.Add(partyItem);
        }

        private void ClearPartyList()
        {
            foreach (GameObject item in _partyUIItems)
            {
                if (item) DestroyImmediate(item);
            }
            _partyUIItems.Clear();
        }

        private void UpdateLastUpdatedText()
        {
            if (lastUpdatedText)
            {
                lastUpdatedText.text = $"Updated: {_currentData.LastUpdated:HH:mm:ss}";
            }
        }

        #endregion

        #region Utility Methods

        private Color GetPartyColor(string partyName)
        {
            // Simple hash-based color generation for parties
            int hash = partyName.GetHashCode();
            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(hash);

            Color color = Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 0.9f);

            UnityEngine.Random.state = oldState;
            return color;
        }

        private Color[] GenerateSpectrumColors(int count, Color startColor, Color endColor)
        {
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                colors[i] = Color.Lerp(startColor, endColor, t);
            }
            return colors;
        }

        private float[] GenerateNormalDistribution(int bins, float mean, float stdDev)
        {
            float[] distribution = new float[bins];
            float sum = 0f;

            for (int i = 0; i < bins; i++)
            {
                float x = (float)i;
                float value = Mathf.Exp(-0.5f * Mathf.Pow((x - mean) / stdDev, 2));
                distribution[i] = value;
                sum += value;
            }

            // Normalize to sum to 1
            for (int i = 0; i < bins; i++)
            {
                distribution[i] /= sum;
            }

            return distribution;
        }

        private void RequestDataRefresh()
        {
            // This would typically trigger a request to the data binding manager
            // For now, we'll just refresh with current data
            UpdateAllVisualization();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            ClearPartyList();

            if (heatmapTexture)
            {
                DestroyImmediate(heatmapTexture);
            }
        }

        #endregion
    }
}