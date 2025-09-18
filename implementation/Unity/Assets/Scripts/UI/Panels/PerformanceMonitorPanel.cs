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
    /// Real-time performance monitoring panel displaying FPS, memory usage, and system metrics.
    /// Optimized for minimal performance impact while providing comprehensive monitoring.
    /// </summary>
    public class PerformanceMonitorPanel : MonoBehaviour, IUIComponent
    {
        [Header("Performance Metrics")]
        [SerializeField] private Text fpsText;
        [SerializeField] private Text memoryText;
        [SerializeField] private Text cpuText;
        [SerializeField] private Text eventQueueText;
        [SerializeField] private Text aiResponseTimeText;

        [Header("Real-time Graphs")]
        [SerializeField] private LineChart fpsChart;
        [SerializeField] private LineChart memoryChart;
        [SerializeField] private BarChart systemLoadChart;

        [Header("Performance Indicators")]
        [SerializeField] private Image fpsIndicator;
        [SerializeField] private Image memoryIndicator;
        [SerializeField] private Image overallHealthIndicator;

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private int historyLength = 60; // 6 seconds at 10Hz
        [SerializeField] private bool enableGraphs = true;
        [SerializeField] private bool enableProfiling = true;

        [Header("Thresholds")]
        [SerializeField] private float fpsWarningThreshold = 45f;
        [SerializeField] private float fpsCriticalThreshold = 30f;
        [SerializeField] private float memoryWarningThreshold = 0.8f; // 80% of available
        [SerializeField] private float memoryCriticalThreshold = 0.9f; // 90% of available

        [Header("Compact Mode")]
        [SerializeField] private bool compactMode = false;
        [SerializeField] private Transform compactContainer;
        [SerializeField] private Transform fullContainer;

        // Performance data
        private PerformanceData _currentData;
        private bool _hasData = false;
        private bool _isVisible = true;

        // History tracking
        private readonly Queue<float> _fpsHistory = new Queue<float>();
        private readonly Queue<float> _memoryHistory = new Queue<float>();
        private readonly Queue<float> _cpuHistory = new Queue<float>();

        // Update timing
        private float _lastUpdateTime;
        private float _frameCount;
        private float _frameTimeAccumulator;

        // Memory tracking
        private long _maxMemoryUsage = 0;
        private float _memoryGrowthRate = 0f;
        private float _lastMemoryReading = 0f;

        // System monitoring
        private int _lastEventQueueSize = 0;
        private float _lastAIResponseTime = 0f;
        private float _systemLoad = 0f;

        // Color coding
        private readonly Color _goodColor = Color.green;
        private readonly Color _warningColor = Color.yellow;
        private readonly Color _criticalColor = Color.red;

        // Performance tracking (minimal overhead)
        private readonly ProfilerMarker _updateMarker = new("PerformanceMonitor.Update");

        public Type GetDataType() => typeof(PerformanceData);
        public bool IsVisible() => _isVisible;

        private void Awake()
        {
            InitializeMonitor();
            SetupGraphs();
        }

        private void Start()
        {
            SetCompactMode(compactMode);
        }

        private void Update()
        {
            using (_updateMarker.Auto())
            {
                UpdateFrameStats();

                if (Time.time - _lastUpdateTime >= updateInterval)
                {
                    CollectPerformanceData();
                    UpdateDisplay();
                    UpdateHistory();
                    _lastUpdateTime = Time.time;
                }
            }
        }

        #region Initialization

        private void InitializeMonitor()
        {
            _lastUpdateTime = Time.time;
            _currentData = new PerformanceData(true);

            // Get system memory info
            _maxMemoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(false);

            Debug.Log("PerformanceMonitor initialized");
        }

        private void SetupGraphs()
        {
            if (!enableGraphs) return;

            // Setup FPS chart
            if (fpsChart)
            {
                fpsChart.SetTitle("FPS");
                fpsChart.SetAxisLabels("Time", "FPS");
                fpsChart.SetYAxisRange(0f, 120f);
            }

            // Setup Memory chart
            if (memoryChart)
            {
                memoryChart.SetTitle("Memory (MB)");
                memoryChart.SetAxisLabels("Time", "Memory");
                memoryChart.SetYAxisRange(0f, 2048f);
            }

            // Setup System Load chart
            if (systemLoadChart)
            {
                systemLoadChart.SetTitle("System Load");
                systemLoadChart.SetAxisLabels("Component", "Load %");
                systemLoadChart.SetLabels(new[] { "CPU", "Memory", "Events", "AI" });
            }
        }

        #endregion

        #region Public Interface

        public void UpdateData(object data)
        {
            if (data is PerformanceData perfData)
            {
                UpdateData(perfData);
            }
        }

        public void UpdateData(PerformanceData data)
        {
            _currentData = data;
            _hasData = data.HasData;

            if (_hasData)
            {
                UpdateFromExternalData();
            }
        }

        public void SetVisibility(bool visible)
        {
            _isVisible = visible;
            gameObject.SetActive(visible);
        }

        public void SetCompactMode(bool compact)
        {
            compactMode = compact;

            if (compactContainer) compactContainer.gameObject.SetActive(compact);
            if (fullContainer) fullContainer.gameObject.SetActive(!compact);

            // Disable graphs in compact mode for performance
            enableGraphs = !compact;

            if (fpsChart) fpsChart.gameObject.SetActive(enableGraphs);
            if (memoryChart) memoryChart.gameObject.SetActive(enableGraphs);
            if (systemLoadChart) systemLoadChart.gameObject.SetActive(enableGraphs);
        }

        public void EnableAccessibilityMode(bool enabled)
        {
            // Performance monitor accessibility features
            if (enabled)
            {
                // Provide text descriptions of performance states
                if (fpsText && _currentData.CurrentFPS < fpsCriticalThreshold)
                {
                    // Screen reader would announce critical performance issues
                }
            }
        }

        public void ResetStatistics()
        {
            _fpsHistory.Clear();
            _memoryHistory.Clear();
            _cpuHistory.Clear();
            _maxMemoryUsage = 0;
            _frameCount = 0;
            _frameTimeAccumulator = 0f;

            Debug.Log("Performance statistics reset");
        }

        #endregion

        #region Data Collection

        private void UpdateFrameStats()
        {
            _frameCount++;
            _frameTimeAccumulator += Time.unscaledDeltaTime;
        }

        private void CollectPerformanceData()
        {
            // Calculate FPS
            if (_frameTimeAccumulator > 0f)
            {
                _currentData.CurrentFPS = _frameCount / _frameTimeAccumulator;
                _frameCount = 0;
                _frameTimeAccumulator = 0f;
            }

            // Update average FPS
            if (_fpsHistory.Count > 0)
            {
                float sum = 0f;
                foreach (float fps in _fpsHistory)
                    sum += fps;
                _currentData.AverageFPS = sum / _fpsHistory.Count;
            }

            // Get memory usage
            long totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(false);
            _currentData.MemoryUsage = totalMemory / (1024f * 1024f); // Convert to MB

            // Track max memory usage
            if (totalMemory > _maxMemoryUsage)
                _maxMemoryUsage = totalMemory;

            // Calculate memory growth rate
            if (_lastMemoryReading > 0f)
            {
                _memoryGrowthRate = (_currentData.MemoryUsage - _lastMemoryReading) / updateInterval;
            }
            _lastMemoryReading = _currentData.MemoryUsage;

            // Estimate CPU usage (simplified)
            _currentData.CPUUsage = EstimateCPUUsage();

            // System-specific metrics (would be provided by external systems)
            _currentData.EventQueueSize = _lastEventQueueSize;
            _currentData.EventsProcessed = _lastEventQueueSize; // Placeholder
            _currentData.AIResponseTime = _lastAIResponseTime;
            _currentData.ActiveVoterCount = GetActiveVoterCount();

            _currentData.HasData = true;
            _currentData.LastUpdated = DateTime.UtcNow;
        }

        private float EstimateCPUUsage()
        {
            // Simple CPU usage estimation based on frame time
            float targetFrameTime = 1f / 60f; // 60 FPS target
            float actualFrameTime = Time.unscaledDeltaTime;
            float cpuLoad = Mathf.Clamp01(actualFrameTime / targetFrameTime);

            return cpuLoad * 100f;
        }

        private int GetActiveVoterCount()
        {
            // This would be retrieved from the voter system
            // For now, return a placeholder value
            return UnityEngine.Random.Range(8000, 10000);
        }

        private void UpdateFromExternalData()
        {
            // Update metrics that come from external systems
            _lastEventQueueSize = _currentData.EventQueueSize;
            _lastAIResponseTime = _currentData.AIResponseTime;
        }

        #endregion

        #region Display Updates

        private void UpdateDisplay()
        {
            UpdateTextDisplays();
            UpdateIndicators();
            UpdateGraphs();
        }

        private void UpdateTextDisplays()
        {
            if (fpsText)
            {
                fpsText.text = $"FPS: {_currentData.CurrentFPS:F1}";
                fpsText.color = GetPerformanceColor(_currentData.CurrentFPS, fpsWarningThreshold, fpsCriticalThreshold, true);
            }

            if (memoryText)
            {
                memoryText.text = $"Memory: {_currentData.MemoryUsage:F1} MB";
                float memoryPercent = _currentData.MemoryUsage / (_maxMemoryUsage / (1024f * 1024f));
                memoryText.color = GetPerformanceColor(memoryPercent, memoryWarningThreshold, memoryCriticalThreshold, false);
            }

            if (cpuText)
            {
                cpuText.text = $"CPU: {_currentData.CPUUsage:F1}%";
                cpuText.color = GetPerformanceColor(_currentData.CPUUsage, 70f, 85f, false);
            }

            if (eventQueueText)
                eventQueueText.text = $"Events: {_currentData.EventQueueSize}";

            if (aiResponseTimeText)
            {
                aiResponseTimeText.text = $"AI: {_currentData.AIResponseTime:F2}s";
                aiResponseTimeText.color = GetPerformanceColor(_currentData.AIResponseTime, 2f, 5f, false);
            }
        }

        private void UpdateIndicators()
        {
            if (fpsIndicator)
            {
                fpsIndicator.color = GetPerformanceColor(_currentData.CurrentFPS, fpsWarningThreshold, fpsCriticalThreshold, true);
            }

            if (memoryIndicator)
            {
                float memoryPercent = _currentData.MemoryUsage / (_maxMemoryUsage / (1024f * 1024f));
                memoryIndicator.color = GetPerformanceColor(memoryPercent, memoryWarningThreshold, memoryCriticalThreshold, false);
            }

            if (overallHealthIndicator)
            {
                overallHealthIndicator.color = CalculateOverallHealth();
            }
        }

        private void UpdateGraphs()
        {
            if (!enableGraphs) return;

            // Update FPS chart
            if (fpsChart && _fpsHistory.Count > 1)
            {
                var fpsData = new float[_fpsHistory.Count];
                _fpsHistory.CopyTo(fpsData, 0);
                fpsChart.SetData(fpsData);
            }

            // Update Memory chart
            if (memoryChart && _memoryHistory.Count > 1)
            {
                var memoryData = new float[_memoryHistory.Count];
                _memoryHistory.CopyTo(memoryData, 0);
                memoryChart.SetData(memoryData);
            }

            // Update System Load chart
            if (systemLoadChart)
            {
                float[] loadData = {
                    _currentData.CPUUsage,
                    (_currentData.MemoryUsage / (_maxMemoryUsage / (1024f * 1024f))) * 100f,
                    Mathf.Clamp01(_currentData.EventQueueSize / 100f) * 100f,
                    Mathf.Clamp01(_currentData.AIResponseTime / 5f) * 100f
                };
                systemLoadChart.SetData(loadData);
            }
        }

        #endregion

        #region History Management

        private void UpdateHistory()
        {
            // Add current values to history
            _fpsHistory.Enqueue(_currentData.CurrentFPS);
            _memoryHistory.Enqueue(_currentData.MemoryUsage);
            _cpuHistory.Enqueue(_currentData.CPUUsage);

            // Maintain history length
            while (_fpsHistory.Count > historyLength)
                _fpsHistory.Dequeue();

            while (_memoryHistory.Count > historyLength)
                _memoryHistory.Dequeue();

            while (_cpuHistory.Count > historyLength)
                _cpuHistory.Dequeue();
        }

        #endregion

        #region Utility Methods

        private Color GetPerformanceColor(float value, float warningThreshold, float criticalThreshold, bool higherIsBetter)
        {
            if (higherIsBetter)
            {
                if (value < criticalThreshold) return _criticalColor;
                if (value < warningThreshold) return _warningColor;
                return _goodColor;
            }
            else
            {
                if (value > criticalThreshold) return _criticalColor;
                if (value > warningThreshold) return _warningColor;
                return _goodColor;
            }
        }

        private Color CalculateOverallHealth()
        {
            int criticalCount = 0;
            int warningCount = 0;

            // Check FPS
            if (_currentData.CurrentFPS < fpsCriticalThreshold) criticalCount++;
            else if (_currentData.CurrentFPS < fpsWarningThreshold) warningCount++;

            // Check Memory
            float memoryPercent = _currentData.MemoryUsage / (_maxMemoryUsage / (1024f * 1024f));
            if (memoryPercent > memoryCriticalThreshold) criticalCount++;
            else if (memoryPercent > memoryWarningThreshold) warningCount++;

            // Check CPU
            if (_currentData.CPUUsage > 85f) criticalCount++;
            else if (_currentData.CPUUsage > 70f) warningCount++;

            // Check AI Response Time
            if (_currentData.AIResponseTime > 5f) criticalCount++;
            else if (_currentData.AIResponseTime > 2f) warningCount++;

            if (criticalCount > 0) return _criticalColor;
            if (warningCount > 0) return _warningColor;
            return _goodColor;
        }

        public PerformanceStatistics GetStatistics()
        {
            return new PerformanceStatistics
            {
                CurrentFPS = _currentData.CurrentFPS,
                AverageFPS = _currentData.AverageFPS,
                MemoryUsage = _currentData.MemoryUsage,
                MaxMemoryUsage = _maxMemoryUsage / (1024f * 1024f),
                MemoryGrowthRate = _memoryGrowthRate,
                CPUUsage = _currentData.CPUUsage,
                EventQueueSize = _currentData.EventQueueSize,
                AIResponseTime = _currentData.AIResponseTime,
                ActiveVoterCount = _currentData.ActiveVoterCount,
                OverallHealth = CalculateOverallHealthScore()
            };
        }

        private float CalculateOverallHealthScore()
        {
            float fpsScore = Mathf.Clamp01(_currentData.CurrentFPS / 60f);
            float memoryScore = 1f - Mathf.Clamp01((_currentData.MemoryUsage / (_maxMemoryUsage / (1024f * 1024f))));
            float cpuScore = 1f - Mathf.Clamp01(_currentData.CPUUsage / 100f);
            float aiScore = 1f - Mathf.Clamp01(_currentData.AIResponseTime / 5f);

            return (fpsScore + memoryScore + cpuScore + aiScore) / 4f;
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        public struct PerformanceStatistics
        {
            public float CurrentFPS;
            public float AverageFPS;
            public float MemoryUsage;
            public float MaxMemoryUsage;
            public float MemoryGrowthRate;
            public float CPUUsage;
            public int EventQueueSize;
            public float AIResponseTime;
            public int ActiveVoterCount;
            public float OverallHealth;
        }

        #endregion
    }
}