using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Components
{
    /// <summary>
    /// Real-time line chart component for performance monitoring and data visualization.
    /// Optimized for continuous updates with smooth animation and minimal performance impact.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LineChart : MaskableGraphic
    {
        [Header("Chart Configuration")]
        [SerializeField] private float[] _data = new float[0];
        [SerializeField] private Color lineColor = Color.green;
        [SerializeField] private float lineWidth = 2f;
        [SerializeField] private string _title = "Line Chart";
        [SerializeField] private string _xAxisLabel = "Time";
        [SerializeField] private string _yAxisLabel = "Value";

        [Header("Axis Settings")]
        [SerializeField] private float yAxisMin = 0f;
        [SerializeField] private float yAxisMax = 100f;
        [SerializeField] private bool autoScale = true;
        [SerializeField] private float padding = 0.1f;

        [Header("Grid Settings")]
        [SerializeField] private bool showGrid = true;
        [SerializeField] private Color gridColor = Color.gray;
        [SerializeField] private int gridLinesX = 5;
        [SerializeField] private int gridLinesY = 5;

        [Header("Animation")]
        [SerializeField] private bool enableSmoothing = true;
        [SerializeField] private float smoothingFactor = 0.1f;

        [Header("UI References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text xAxisText;
        [SerializeField] private Text yAxisText;
        [SerializeField] private Text minValueText;
        [SerializeField] private Text maxValueText;

        // Chart metrics
        private float _actualYMin;
        private float _actualYMax;
        private Vector2 _chartOrigin;
        private Vector2 _chartSize;

        // Smoothing
        private float[] _smoothedData;

        // Performance tracking
        private readonly ProfilerMarker _drawMarker = new("LineChart.Draw");

        protected override void Awake()
        {
            base.Awake();
            InitializeChart();
        }

        protected override void Start()
        {
            UpdateLabels();
        }

        #region Initialization

        private void InitializeChart()
        {
            if (_data.Length == 0)
            {
                _data = new float[60]; // Default 60 data points
                for (int i = 0; i < _data.Length; i++)
                {
                    _data[i] = Mathf.Sin(i * 0.1f) * 50f + 50f; // Sample sine wave
                }
            }

            _smoothedData = new float[_data.Length];
            Array.Copy(_data, _smoothedData, _data.Length);

            CalculateChartBounds();
        }

        private void CalculateChartBounds()
        {
            var rect = GetPixelAdjustedRect();
            _chartOrigin = new Vector2(rect.x + rect.width * padding, rect.y + rect.height * padding);
            _chartSize = new Vector2(rect.width * (1f - 2f * padding), rect.height * (1f - 2f * padding));

            if (autoScale && _data.Length > 0)
            {
                _actualYMin = float.MaxValue;
                _actualYMax = float.MinValue;

                foreach (float value in _data)
                {
                    if (value < _actualYMin) _actualYMin = value;
                    if (value > _actualYMax) _actualYMax = value;
                }

                // Add some padding to the range
                float range = _actualYMax - _actualYMin;
                if (range > 0f)
                {
                    _actualYMin -= range * 0.1f;
                    _actualYMax += range * 0.1f;
                }
                else
                {
                    _actualYMin -= 10f;
                    _actualYMax += 10f;
                }
            }
            else
            {
                _actualYMin = yAxisMin;
                _actualYMax = yAxisMax;
            }
        }

        #endregion

        #region Public Interface

        public void SetData(float[] data)
        {
            if (data == null || data.Length == 0) return;

            _data = new float[data.Length];
            Array.Copy(data, _data, data.Length);

            // Initialize smoothed data if needed
            if (_smoothedData.Length != _data.Length)
            {
                _smoothedData = new float[_data.Length];
                Array.Copy(_data, _smoothedData, _data.Length);
            }

            CalculateChartBounds();
            SetVerticesDirty();
            UpdateLabels();
        }

        public void AddDataPoint(float value)
        {
            if (_data.Length == 0) return;

            // Shift existing data left
            for (int i = 0; i < _data.Length - 1; i++)
            {
                _data[i] = _data[i + 1];
            }

            // Add new data point at the end
            _data[_data.Length - 1] = value;

            // Update smoothed data
            if (enableSmoothing && _smoothedData.Length > 0)
            {
                for (int i = 0; i < _smoothedData.Length - 1; i++)
                {
                    _smoothedData[i] = _smoothedData[i + 1];
                }

                _smoothedData[_smoothedData.Length - 1] = Mathf.Lerp(
                    _smoothedData.Length > 1 ? _smoothedData[_smoothedData.Length - 2] : value,
                    value,
                    smoothingFactor
                );
            }

            CalculateChartBounds();
            SetVerticesDirty();
            UpdateLabels();
        }

        public void SetTitle(string title)
        {
            _title = title;
            if (titleText) titleText.text = title;
        }

        public void SetAxisLabels(string xLabel, string yLabel)
        {
            _xAxisLabel = xLabel;
            _yAxisLabel = yLabel;
            UpdateLabels();
        }

        public void SetYAxisRange(float min, float max)
        {
            yAxisMin = min;
            yAxisMax = max;
            autoScale = false;
            CalculateChartBounds();
            SetVerticesDirty();
        }

        public void SetLineColor(Color color)
        {
            lineColor = color;
            SetVerticesDirty();
        }

        public void SetLineWidth(float width)
        {
            lineWidth = width;
            SetVerticesDirty();
        }

        #endregion

        #region Rendering

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using (_drawMarker.Auto())
            {
                vh.Clear();

                if (_data == null || _data.Length < 2) return;

                CalculateChartBounds();

                // Draw grid if enabled
                if (showGrid)
                {
                    DrawGrid(vh);
                }

                // Draw the line chart
                DrawLineChart(vh);
            }
        }

        private void DrawGrid(VertexHelper vh)
        {
            Color gridColorAlpha = gridColor;
            gridColorAlpha.a = 0.3f;

            // Vertical grid lines
            for (int i = 0; i <= gridLinesX; i++)
            {
                float t = (float)i / gridLinesX;
                float x = _chartOrigin.x + t * _chartSize.x;
                Vector2 start = new Vector2(x, _chartOrigin.y);
                Vector2 end = new Vector2(x, _chartOrigin.y + _chartSize.y);
                DrawLine(vh, start, end, gridColorAlpha, 1f);
            }

            // Horizontal grid lines
            for (int i = 0; i <= gridLinesY; i++)
            {
                float t = (float)i / gridLinesY;
                float y = _chartOrigin.y + t * _chartSize.y;
                Vector2 start = new Vector2(_chartOrigin.x, y);
                Vector2 end = new Vector2(_chartOrigin.x + _chartSize.x, y);
                DrawLine(vh, start, end, gridColorAlpha, 1f);
            }
        }

        private void DrawLineChart(VertexHelper vh)
        {
            float[] dataToUse = enableSmoothing ? _smoothedData : _data;

            for (int i = 0; i < dataToUse.Length - 1; i++)
            {
                Vector2 point1 = DataPointToScreenPosition(i, dataToUse[i]);
                Vector2 point2 = DataPointToScreenPosition(i + 1, dataToUse[i + 1]);

                DrawLine(vh, point1, point2, lineColor, lineWidth);
            }
        }

        private Vector2 DataPointToScreenPosition(int index, float value)
        {
            float x = _chartOrigin.x + ((float)index / (_data.Length - 1)) * _chartSize.x;
            float normalizedY = Mathf.Clamp01((value - _actualYMin) / (_actualYMax - _actualYMin));
            float y = _chartOrigin.y + normalizedY * _chartSize.y;

            return new Vector2(x, y);
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

        #region UI Updates

        private void UpdateLabels()
        {
            if (titleText)
                titleText.text = _title;

            if (xAxisText)
                xAxisText.text = _xAxisLabel;

            if (yAxisText)
                yAxisText.text = _yAxisLabel;

            if (minValueText)
                minValueText.text = _actualYMin.ToString("F1");

            if (maxValueText)
                maxValueText.text = _actualYMax.ToString("F1");
        }

        #endregion

        #region Utility Methods

        public float GetCurrentValue()
        {
            return _data.Length > 0 ? _data[_data.Length - 1] : 0f;
        }

        public float GetMinValue()
        {
            return _actualYMin;
        }

        public float GetMaxValue()
        {
            return _actualYMax;
        }

        public float[] GetData()
        {
            float[] copy = new float[_data.Length];
            Array.Copy(_data, copy, _data.Length);
            return copy;
        }

        #endregion
    }
}