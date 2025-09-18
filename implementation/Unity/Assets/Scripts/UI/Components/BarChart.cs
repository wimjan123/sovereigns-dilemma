using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Components
{
    /// <summary>
    /// Interactive bar chart component for data visualization.
    /// Optimized for real-time updates with smooth animations and accessibility support.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BarChart : MaskableGraphic
    {
        [Header("Chart Configuration")]
        [SerializeField] private float[] _data = new float[0];
        [SerializeField] private Color[] _colors = new Color[0];
        [SerializeField] private string[] _labels = new string[0];
        [SerializeField] private string _title = "Bar Chart";
        [SerializeField] private string _xAxisLabel = "Categories";
        [SerializeField] private string _yAxisLabel = "Values";

        [Header("Visual Settings")]
        [SerializeField] private float barSpacing = 0.1f;
        [SerializeField] private float chartPadding = 0.1f;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private bool showValues = true;
        [SerializeField] private bool enableInteraction = true;

        [Header("Animation")]
        [SerializeField] private bool enableAnimations = true;
        [SerializeField] private float animationDuration = 0.8f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Accessibility")]
        [SerializeField] private bool accessibilityMode = false;
        [SerializeField] private float accessibilityScale = 1.2f;
        [SerializeField] private bool compactMode = false;

        [Header("UI References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text xAxisText;
        [SerializeField] private Text yAxisText;
        [SerializeField] private Transform labelContainer;
        [SerializeField] private GameObject labelPrefab;

        // Animation state
        private float[] _currentAnimatedData;
        private float[] _targetData;
        private float _animationStartTime;
        private bool _isAnimating;

        // Interaction state
        private int _hoveredBar = -1;
        private bool _isHovered;

        // Chart metrics
        private float _maxValue;
        private float _chartWidth;
        private float _chartHeight;
        private Vector2 _chartOrigin;

        // Performance tracking
        private readonly ProfilerMarker _drawMarker = new("BarChart.Draw");
        private readonly ProfilerMarker _updateMarker = new("BarChart.Update");

        // UI generation
        private readonly List<GameObject> _labelObjects = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            InitializeChart();
        }

        protected override void Start()
        {
            SetupLabels();
            UpdateTitle();
        }

        private void Update()
        {
            using (_updateMarker.Auto())
            {
                if (_isAnimating && enableAnimations)
                {
                    UpdateAnimation();
                }
            }
        }

        #region Initialization

        private void InitializeChart()
        {
            // Initialize with default data if empty
            if (_data.Length == 0)
            {
                _data = new float[] { 0.25f, 0.35f, 0.2f, 0.15f, 0.05f };
            }

            // Initialize colors if empty
            if (_colors.Length == 0)
            {
                GenerateDefaultColors();
            }

            // Initialize labels if empty
            if (_labels.Length == 0)
            {
                GenerateDefaultLabels();
            }

            _currentAnimatedData = new float[_data.Length];
            _targetData = new float[_data.Length];
            Array.Copy(_data, _currentAnimatedData, _data.Length);
            Array.Copy(_data, _targetData, _data.Length);

            CalculateChartMetrics();
        }

        private void GenerateDefaultColors()
        {
            _colors = new Color[_data.Length];
            for (int i = 0; i < _data.Length; i++)
            {
                float hue = (float)i / _data.Length;
                _colors[i] = Color.HSVToRGB(hue, 0.7f, 0.9f);
            }
        }

        private void GenerateDefaultLabels()
        {
            _labels = new string[_data.Length];
            for (int i = 0; i < _data.Length; i++)
            {
                _labels[i] = $"Item {i + 1}";
            }
        }

        private void CalculateChartMetrics()
        {
            _maxValue = 0f;
            foreach (float value in _data)
            {
                if (value > _maxValue)
                    _maxValue = value;
            }

            var rect = GetPixelAdjustedRect();
            _chartWidth = rect.width * (1f - 2f * chartPadding);
            _chartHeight = rect.height * (1f - 2f * chartPadding);
            _chartOrigin = new Vector2(
                rect.x + rect.width * chartPadding,
                rect.y + rect.height * chartPadding
            );
        }

        #endregion

        #region Public Interface

        public void SetData(float[] data)
        {
            if (data == null || data.Length == 0) return;

            _data = new float[data.Length];
            Array.Copy(data, _data, data.Length);

            // Update arrays if sizes changed
            if (_currentAnimatedData.Length != _data.Length)
            {
                _currentAnimatedData = new float[_data.Length];
                _targetData = new float[_data.Length];

                if (_colors.Length != _data.Length)
                {
                    GenerateDefaultColors();
                }

                if (_labels.Length != _data.Length)
                {
                    GenerateDefaultLabels();
                }
            }

            Array.Copy(_data, _targetData, _data.Length);
            CalculateChartMetrics();

            if (enableAnimations)
            {
                StartAnimation();
            }
            else
            {
                Array.Copy(_data, _currentAnimatedData, _data.Length);
                SetVerticesDirty();
            }

            UpdateLabels();
        }

        public void AnimateToData(float[] data, float duration)
        {
            animationDuration = duration;
            SetData(data);
        }

        public void SetColors(Color[] colors)
        {
            if (colors != null && colors.Length > 0)
            {
                _colors = new Color[colors.Length];
                Array.Copy(colors, _colors, colors.Length);
                SetVerticesDirty();
            }
        }

        public void SetLabels(string[] labels)
        {
            if (labels != null && labels.Length > 0)
            {
                _labels = new string[labels.Length];
                Array.Copy(labels, _labels, labels.Length);
                UpdateLabels();
            }
        }

        public void SetTitle(string title)
        {
            _title = title;
            UpdateTitle();
        }

        public void SetAxisLabels(string xAxis, string yAxis)
        {
            _xAxisLabel = xAxis;
            _yAxisLabel = yAxis;
            UpdateAxisLabels();
        }

        public void SetCompactMode(bool compact)
        {
            compactMode = compact;

            if (compact)
            {
                showLabels = false;
                showGrid = false;
                chartPadding = 0.05f;
            }
            else
            {
                showLabels = true;
                showGrid = true;
                chartPadding = 0.1f;
            }

            CalculateChartMetrics();
            SetVerticesDirty();
            SetupLabels();
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            accessibilityMode = enabled;

            if (enabled)
            {
                // Increase contrast and size
                for (int i = 0; i < _colors.Length; i++)
                {
                    _colors[i] = IncreaseContrast(_colors[i]);
                }

                chartPadding *= accessibilityScale;
            }

            CalculateChartMetrics();
            SetVerticesDirty();
            UpdateLabels();
        }

        #endregion

        #region Rendering

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using (_drawMarker.Auto())
            {
                vh.Clear();

                if (_currentAnimatedData == null || _currentAnimatedData.Length == 0)
                    return;

                CalculateChartMetrics();

                // Draw grid if enabled
                if (showGrid)
                {
                    DrawGrid(vh);
                }

                // Draw bars
                DrawBars(vh);
            }
        }

        private void DrawGrid(VertexHelper vh)
        {
            Color gridColor = Color.gray;
            gridColor.a = 0.3f;

            int gridLines = 5;
            float stepY = _chartHeight / gridLines;

            for (int i = 0; i <= gridLines; i++)
            {
                float y = _chartOrigin.y + i * stepY;
                Vector2 start = new Vector2(_chartOrigin.x, y);
                Vector2 end = new Vector2(_chartOrigin.x + _chartWidth, y);
                DrawLine(vh, start, end, gridColor, 1f);
            }
        }

        private void DrawBars(VertexHelper vh)
        {
            if (_currentAnimatedData.Length == 0) return;

            float barWidth = (_chartWidth - ((_currentAnimatedData.Length - 1) * barSpacing * _chartWidth)) / _currentAnimatedData.Length;

            for (int i = 0; i < _currentAnimatedData.Length; i++)
            {
                if (_currentAnimatedData[i] <= 0f) continue;

                float barHeight = (_currentAnimatedData[i] / _maxValue) * _chartHeight;
                float x = _chartOrigin.x + i * (barWidth + barSpacing * _chartWidth);

                Color barColor = i < _colors.Length ? _colors[i] : Color.white;

                // Highlight hovered bar
                if (enableInteraction && i == _hoveredBar)
                {
                    barColor = Color.Lerp(barColor, Color.white, 0.3f);
                    barHeight *= 1.05f; // Slightly taller for hover effect
                }

                DrawBar(vh, x, _chartOrigin.y, barWidth, barHeight, barColor);

                // Draw value labels if enabled
                if (showValues)
                {
                    // Value labels would be handled by separate UI Text components
                    // positioned dynamically in UpdateLabels()
                }
            }
        }

        private void DrawBar(VertexHelper vh, float x, float y, float width, float height, Color color)
        {
            Vector2 bottomLeft = new Vector2(x, y);
            Vector2 bottomRight = new Vector2(x + width, y);
            Vector2 topRight = new Vector2(x + width, y + height);
            Vector2 topLeft = new Vector2(x, y + height);

            int vertexIndex = vh.currentVertCount;

            vh.AddVert(bottomLeft, color, Vector2.zero);
            vh.AddVert(bottomRight, color, Vector2.zero);
            vh.AddVert(topRight, color, Vector2.zero);
            vh.AddVert(topLeft, color, Vector2.zero);

            vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vh.AddTriangle(vertexIndex + 2, vertexIndex + 3, vertexIndex);
        }

        private void DrawLine(VertexHelper vh, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * thickness * 0.5f;

            Vector2 p1 = start - perpendicular;
            Vector2 p2 = start + perpendicular;
            Vector2 p3 = end + perpendicular;
            Vector2 p4 = end - perpendicular;

            int vertexIndex = vh.currentVertCount;

            vh.AddVert(p1, color, Vector2.zero);
            vh.AddVert(p2, color, Vector2.zero);
            vh.AddVert(p3, color, Vector2.zero);
            vh.AddVert(p4, color, Vector2.zero);

            vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vh.AddTriangle(vertexIndex + 2, vertexIndex + 3, vertexIndex);
        }

        #endregion

        #region Animation

        private void StartAnimation()
        {
            _isAnimating = true;
            _animationStartTime = Time.time;
        }

        private void UpdateAnimation()
        {
            float elapsed = Time.time - _animationStartTime;
            float progress = Mathf.Clamp01(elapsed / animationDuration);
            float easedProgress = animationCurve.Evaluate(progress);

            bool hasChanged = false;
            for (int i = 0; i < _currentAnimatedData.Length; i++)
            {
                float oldValue = _currentAnimatedData[i];
                _currentAnimatedData[i] = Mathf.Lerp(0f, _targetData[i], easedProgress);

                if (Mathf.Abs(_currentAnimatedData[i] - oldValue) > 0.001f)
                {
                    hasChanged = true;
                }
            }

            if (hasChanged)
            {
                SetVerticesDirty();
            }

            if (progress >= 1f)
            {
                _isAnimating = false;
                Array.Copy(_targetData, _currentAnimatedData, _targetData.Length);
                SetVerticesDirty();
            }
        }

        #endregion

        #region Labels and UI

        private void SetupLabels()
        {
            if (!showLabels || !labelContainer || compactMode)
            {
                ClearLabels();
                return;
            }

            ClearLabels();
            CreateLabels();
        }

        private void CreateLabels()
        {
            if (!labelPrefab) return;

            for (int i = 0; i < _data.Length && i < _labels.Length; i++)
            {
                GameObject labelObj = Instantiate(labelPrefab, labelContainer);
                Text labelText = labelObj.GetComponentInChildren<Text>();

                if (labelText)
                {
                    labelText.text = _labels[i];

                    if (accessibilityMode)
                    {
                        labelText.fontSize = Mathf.RoundToInt(labelText.fontSize * accessibilityScale);
                    }
                }

                _labelObjects.Add(labelObj);
            }
        }

        private void UpdateLabels()
        {
            if (showLabels && !compactMode)
            {
                SetupLabels();
            }
        }

        private void ClearLabels()
        {
            foreach (GameObject labelObj in _labelObjects)
            {
                if (labelObj) DestroyImmediate(labelObj);
            }
            _labelObjects.Clear();
        }

        private void UpdateTitle()
        {
            if (titleText)
            {
                titleText.text = _title;

                if (accessibilityMode)
                {
                    titleText.fontSize = Mathf.RoundToInt(titleText.fontSize * accessibilityScale);
                }
            }
        }

        private void UpdateAxisLabels()
        {
            if (xAxisText)
            {
                xAxisText.text = _xAxisLabel;
            }

            if (yAxisText)
            {
                yAxisText.text = _yAxisLabel;
            }
        }

        #endregion

        #region Interaction

        public void OnPointerEnter(int barIndex)
        {
            if (!enableInteraction) return;

            _hoveredBar = barIndex;
            _isHovered = true;
            SetVerticesDirty();
        }

        public void OnPointerExit()
        {
            if (!enableInteraction) return;

            _hoveredBar = -1;
            _isHovered = false;
            SetVerticesDirty();
        }

        public int GetBarAtPosition(Vector2 localPosition)
        {
            if (_currentAnimatedData.Length == 0) return -1;

            CalculateChartMetrics();
            float barWidth = (_chartWidth - ((_currentAnimatedData.Length - 1) * barSpacing * _chartWidth)) / _currentAnimatedData.Length;

            for (int i = 0; i < _currentAnimatedData.Length; i++)
            {
                float x = _chartOrigin.x + i * (barWidth + barSpacing * _chartWidth);
                float barHeight = (_currentAnimatedData[i] / _maxValue) * _chartHeight;

                if (localPosition.x >= x && localPosition.x <= x + barWidth &&
                    localPosition.y >= _chartOrigin.y && localPosition.y <= _chartOrigin.y + barHeight)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region Utility

        private Color IncreaseContrast(Color color)
        {
            // Increase contrast for accessibility
            float brightness = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;

            if (brightness > 0.5f)
            {
                // Make brighter colors darker
                return color * 0.7f;
            }
            else
            {
                // Make darker colors brighter
                return Color.Lerp(color, Color.white, 0.4f);
            }
        }

        public float[] GetCurrentData()
        {
            return _currentAnimatedData;
        }

        public bool IsAnimating()
        {
            return _isAnimating;
        }

        public float GetMaxValue()
        {
            return _maxValue;
        }

        #endregion

        #region Cleanup

        protected override void OnDestroy()
        {
            ClearLabels();
            base.OnDestroy();
        }

        #endregion
    }
}