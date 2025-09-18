using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Components
{
    /// <summary>
    /// Interactive pie chart component for data visualization.
    /// Optimized for real-time updates with smooth animations and accessibility support.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PieChart : MaskableGraphic
    {
        [Header("Chart Configuration")]
        [SerializeField] private float[] _data = new float[0];
        [SerializeField] private Color[] _colors = new Color[0];
        [SerializeField] private string[] _labels = new string[0];
        [SerializeField] private string _title = "Pie Chart";

        [Header("Visual Settings")]
        [SerializeField] private float innerRadius = 0.3f;
        [SerializeField] private float outerRadius = 0.9f;
        [SerializeField] private int segments = 64;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private bool showLegend = true;
        [SerializeField] private bool enableInteraction = true;

        [Header("Animation")]
        [SerializeField] private bool enableAnimations = true;
        [SerializeField] private float animationDuration = 0.5f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Accessibility")]
        [SerializeField] private bool accessibilityMode = false;
        [SerializeField] private float accessibilityScale = 1.2f;
        [SerializeField] private bool compactMode = false;

        [Header("UI References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Transform legendContainer;
        [SerializeField] private GameObject legendItemPrefab;

        // Animation state
        private float[] _currentAnimatedData;
        private float[] _targetData;
        private float _animationStartTime;
        private bool _isAnimating;

        // Interaction state
        private int _hoveredSegment = -1;
        private bool _isHovered;

        // Performance tracking
        private readonly ProfilerMarker _drawMarker = new("PieChart.Draw");
        private readonly ProfilerMarker _updateMarker = new("PieChart.Update");

        // UI generation
        private readonly List<GameObject> _legendItems = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            InitializeChart();
        }

        protected override void Start()
        {
            SetupLegend();
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
                _labels[i] = $"Segment {i + 1}";
            }
        }

        #endregion

        #region Public Interface

        public void SetData(float[] data)
        {
            if (data == null || data.Length == 0) return;

            // Normalize data to sum to 1.0
            float sum = 0f;
            foreach (float value in data)
            {
                sum += Mathf.Max(0f, value);
            }

            if (sum > 0f)
            {
                _data = new float[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    _data[i] = Mathf.Max(0f, data[i]) / sum;
                }

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

                if (enableAnimations)
                {
                    StartAnimation();
                }
                else
                {
                    Array.Copy(_data, _currentAnimatedData, _data.Length);
                    SetVerticesDirty();
                }

                UpdateLegend();
            }
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
                UpdateLegend();
            }
        }

        public void SetLabels(string[] labels)
        {
            if (labels != null && labels.Length > 0)
            {
                _labels = new string[labels.Length];
                Array.Copy(labels, _labels, labels.Length);
                UpdateLegend();
            }
        }

        public void SetTitle(string title)
        {
            _title = title;
            UpdateTitle();
        }

        public void SetCompactMode(bool compact)
        {
            compactMode = compact;

            if (compact)
            {
                showLabels = false;
                showLegend = false;
                outerRadius = 0.8f;
            }
            else
            {
                showLabels = true;
                showLegend = true;
                outerRadius = 0.9f;
            }

            SetVerticesDirty();
            SetupLegend();
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

                outerRadius *= accessibilityScale;
                innerRadius *= accessibilityScale;
            }

            SetVerticesDirty();
            UpdateLegend();
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

                var rect = GetPixelAdjustedRect();
                var center = rect.center;
                var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
                var innerR = radius * innerRadius;
                var outerR = radius * outerRadius;

                float currentAngle = 0f;
                int vertexIndex = 0;

                for (int i = 0; i < _currentAnimatedData.Length; i++)
                {
                    if (_currentAnimatedData[i] <= 0f) continue;

                    float segmentAngle = _currentAnimatedData[i] * 360f;
                    Color segmentColor = i < _colors.Length ? _colors[i] : Color.white;

                    // Highlight hovered segment
                    if (enableInteraction && i == _hoveredSegment)
                    {
                        segmentColor = Color.Lerp(segmentColor, Color.white, 0.3f);
                        innerR *= 0.95f; // Slightly smaller inner radius for hover effect
                        outerR *= 1.05f; // Slightly larger outer radius for hover effect
                    }

                    DrawSegment(vh, center, innerR, outerR, currentAngle, segmentAngle, segmentColor, ref vertexIndex);
                    currentAngle += segmentAngle;

                    // Reset radius for next segment
                    if (enableInteraction && i == _hoveredSegment)
                    {
                        innerR = radius * innerRadius;
                        outerR = radius * outerRadius;
                    }
                }
            }
        }

        private void DrawSegment(VertexHelper vh, Vector2 center, float innerRadius, float outerRadius,
            float startAngle, float segmentAngle, Color color, ref int vertexIndex)
        {
            int segmentCount = Mathf.Max(3, (int)(segments * segmentAngle / 360f));
            float angleStep = segmentAngle / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle1 = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                float angle2 = (startAngle + (i + 1) * angleStep) * Mathf.Deg2Rad;

                // Create quad vertices
                Vector2 inner1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * innerRadius;
                Vector2 outer1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * outerRadius;
                Vector2 inner2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * innerRadius;
                Vector2 outer2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * outerRadius;

                // Add vertices
                vh.AddVert(inner1, color, Vector2.zero);
                vh.AddVert(outer1, color, Vector2.zero);
                vh.AddVert(outer2, color, Vector2.zero);
                vh.AddVert(inner2, color, Vector2.zero);

                // Add triangles
                vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
                vh.AddTriangle(vertexIndex + 2, vertexIndex + 3, vertexIndex);

                vertexIndex += 4;
            }
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
                _currentAnimatedData[i] = Mathf.Lerp(_currentAnimatedData[i], _targetData[i], easedProgress);

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

        #region Legend and Labels

        private void SetupLegend()
        {
            if (!showLegend || !legendContainer || compactMode)
            {
                ClearLegend();
                return;
            }

            ClearLegend();

            for (int i = 0; i < _data.Length && i < _labels.Length && i < _colors.Length; i++)
            {
                if (_data[i] <= 0f) continue;

                GameObject legendItem = CreateLegendItem(i);
                if (legendItem != null)
                {
                    _legendItems.Add(legendItem);
                }
            }
        }

        private GameObject CreateLegendItem(int index)
        {
            if (!legendItemPrefab) return null;

            GameObject item = Instantiate(legendItemPrefab, legendContainer);

            // Find color indicator
            Image colorIndicator = item.GetComponentInChildren<Image>();
            if (colorIndicator)
            {
                colorIndicator.color = _colors[index];
            }

            // Find label text
            Text labelText = item.GetComponentInChildren<Text>();
            if (labelText)
            {
                float percentage = _data[index] * 100f;
                labelText.text = $"{_labels[index]}: {percentage:F1}%";

                if (accessibilityMode)
                {
                    labelText.fontSize = Mathf.RoundToInt(labelText.fontSize * accessibilityScale);
                }
            }

            return item;
        }

        private void UpdateLegend()
        {
            if (showLegend && !compactMode)
            {
                SetupLegend();
            }
        }

        private void ClearLegend()
        {
            foreach (GameObject item in _legendItems)
            {
                if (item) DestroyImmediate(item);
            }
            _legendItems.Clear();
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

        #endregion

        #region Interaction

        public void OnPointerEnter(int segmentIndex)
        {
            if (!enableInteraction) return;

            _hoveredSegment = segmentIndex;
            _isHovered = true;
            SetVerticesDirty();
        }

        public void OnPointerExit()
        {
            if (!enableInteraction) return;

            _hoveredSegment = -1;
            _isHovered = false;
            SetVerticesDirty();
        }

        public int GetSegmentAtPosition(Vector2 localPosition)
        {
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var offset = localPosition - center;
            var distance = offset.magnitude;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;

            // Check if within chart area
            if (distance < radius * innerRadius || distance > radius * outerRadius)
                return -1;

            // Calculate angle
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // Find segment
            float currentAngle = 0f;
            for (int i = 0; i < _currentAnimatedData.Length; i++)
            {
                float segmentAngle = _currentAnimatedData[i] * 360f;
                if (angle >= currentAngle && angle < currentAngle + segmentAngle)
                {
                    return i;
                }
                currentAngle += segmentAngle;
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

        #endregion

        #region Cleanup

        protected override void OnDestroy()
        {
            ClearLegend();
            base.OnDestroy();
        }

        #endregion
    }
}