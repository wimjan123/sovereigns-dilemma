using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Testing.Performance
{
    /// <summary>
    /// Real-time performance monitoring system for The Sovereign's Dilemma.
    /// Provides runtime performance metrics and alerting for production use.
    /// </summary>
    public class SystemPerformanceMonitor : MonoBehaviour
    {
        [Header("Monitoring Configuration")]
        [SerializeField] private bool enableRuntimeMonitoring = true;
        [SerializeField] private float updateInterval = 1.0f;
        [SerializeField] private int maxSamples = 300; // 5 minutes at 1 second intervals
        [SerializeField] private bool enablePerformanceWarnings = true;
        [SerializeField] private bool enablePerformanceUI = false;

        [Header("Performance Thresholds")]
        [SerializeField] private float frameTimeWarningThreshold = 20.0f; // 50 FPS
        [SerializeField] private float frameTimeCriticalThreshold = 33.33f; // 30 FPS
        [SerializeField] private float memoryWarningThreshold = 800.0f; // 800MB
        [SerializeField] private float memoryCriticalThreshold = 1024.0f; // 1GB

        // Performance tracking
        private readonly List<PerformanceSample> _performanceSamples = new();
        private readonly Dictionary<string, SystemMetrics> _systemMetrics = new();
        private float _lastUpdateTime;
        private int _currentVoterCount;

        // UI for performance display
        private Rect _performanceUIRect = new Rect(10, 10, 300, 200);
        private bool _showDetailedMetrics = false;

        // Profiler markers for monitoring
        private static readonly ProfilerMarker PerformanceMonitorMarker = new("SovereignsDilemma.PerformanceMonitor");

        private void Update()
        {
            if (!enableRuntimeMonitoring)
                return;

            var currentTime = Time.realtimeSinceStartup;

            if (currentTime - _lastUpdateTime >= updateInterval)
            {
                using (PerformanceMonitorMarker.Auto())
                {
                    CollectPerformanceMetrics();
                    CheckPerformanceThresholds();
                    UpdateSystemMetrics();
                }

                _lastUpdateTime = currentTime;
            }
        }

        private void OnGUI()
        {
            if (!enablePerformanceUI)
                return;

            // Performance overlay UI
            _performanceUIRect = GUI.Window(0, _performanceUIRect, DrawPerformanceWindow, "Performance Monitor");
        }

        /// <summary>
        /// Collects current performance metrics.
        /// </summary>
        private void CollectPerformanceMetrics()
        {
            var currentTime = Time.realtimeSinceStartup;
            var frameTime = Time.smoothDeltaTime * 1000f; // Convert to ms
            var fps = 1.0f / Time.smoothDeltaTime;

            // Memory metrics
            var totalMemory = System.GC.GetTotalMemory(false) / (1024f * 1024f); // MB
            var allocatedMemory = Profiler.GetTotalAllocatedMemory(Profiler.GetMainThreadIndex()) / (1024f * 1024f);

            // Voter count (if available)
            _currentVoterCount = GetCurrentVoterCount();

            // System metrics
            var cpuUsage = GetCPUUsage();
            var renderingTime = GetRenderingTime();

            var sample = new PerformanceSample
            {
                Timestamp = currentTime,
                FrameTime = frameTime,
                FPS = fps,
                TotalMemoryMB = totalMemory,
                AllocatedMemoryMB = allocatedMemory,
                VoterCount = _currentVoterCount,
                CPUUsage = cpuUsage,
                RenderingTime = renderingTime
            };

            _performanceSamples.Add(sample);

            // Keep only recent samples
            if (_performanceSamples.Count > maxSamples)
            {
                _performanceSamples.RemoveAt(0);
            }

            // Update performance profiler with current data
            PerformanceProfiler.RecordMeasurement("FrameTime", frameTime);
            PerformanceProfiler.RecordMeasurement("MemoryUsage", totalMemory);
            PerformanceProfiler.RecordMeasurement("VoterCount", _currentVoterCount);
        }

        /// <summary>
        /// Checks performance metrics against thresholds and issues warnings.
        /// </summary>
        private void CheckPerformanceThresholds()
        {
            if (!enablePerformanceWarnings || _performanceSamples.Count == 0)
                return;

            var latestSample = _performanceSamples.Last();

            // Frame time warnings
            if (latestSample.FrameTime > frameTimeCriticalThreshold)
            {
                Debug.LogWarning($"CRITICAL: Frame time {latestSample.FrameTime:F1}ms exceeds critical threshold ({frameTimeCriticalThreshold:F1}ms)");
                LogPerformanceEvent("FrameTime", "Critical", latestSample.FrameTime);
            }
            else if (latestSample.FrameTime > frameTimeWarningThreshold)
            {
                Debug.LogWarning($"WARNING: Frame time {latestSample.FrameTime:F1}ms exceeds warning threshold ({frameTimeWarningThreshold:F1}ms)");
                LogPerformanceEvent("FrameTime", "Warning", latestSample.FrameTime);
            }

            // Memory warnings
            if (latestSample.TotalMemoryMB > memoryCriticalThreshold)
            {
                Debug.LogWarning($"CRITICAL: Memory usage {latestSample.TotalMemoryMB:F1}MB exceeds critical threshold ({memoryCriticalThreshold:F1}MB)");
                LogPerformanceEvent("Memory", "Critical", latestSample.TotalMemoryMB);
            }
            else if (latestSample.TotalMemoryMB > memoryWarningThreshold)
            {
                Debug.LogWarning($"WARNING: Memory usage {latestSample.TotalMemoryMB:F1}MB exceeds warning threshold ({memoryWarningThreshold:F1}MB)");
                LogPerformanceEvent("Memory", "Warning", latestSample.TotalMemoryMB);
            }

            // Check for performance degradation trends
            CheckPerformanceTrends();
        }

        /// <summary>
        /// Analyzes performance trends and issues warnings for degradation.
        /// </summary>
        private void CheckPerformanceTrends()
        {
            if (_performanceSamples.Count < 30) // Need at least 30 samples for trend analysis
                return;

            var recentSamples = _performanceSamples.TakeLast(30).ToArray();
            var oldSamples = _performanceSamples.Skip(Math.Max(0, _performanceSamples.Count - 60)).Take(30).ToArray();

            if (oldSamples.Length < 30)
                return;

            // Compare average performance
            var recentAvgFrameTime = recentSamples.Average(s => s.FrameTime);
            var oldAvgFrameTime = oldSamples.Average(s => s.FrameTime);

            var recentAvgMemory = recentSamples.Average(s => s.TotalMemoryMB);
            var oldAvgMemory = oldSamples.Average(s => s.TotalMemoryMB);

            // Check for significant degradation
            if (recentAvgFrameTime > oldAvgFrameTime * 1.2f) // 20% worse
            {
                Debug.LogWarning($"Performance degradation detected: Frame time increased from {oldAvgFrameTime:F1}ms to {recentAvgFrameTime:F1}ms");
            }

            if (recentAvgMemory > oldAvgMemory * 1.1f) // 10% increase
            {
                Debug.LogWarning($"Memory usage increase detected: Memory increased from {oldAvgMemory:F1}MB to {recentAvgMemory:F1}MB");
            }
        }

        /// <summary>
        /// Updates system-specific performance metrics.
        /// </summary>
        private void UpdateSystemMetrics()
        {
            // Get performance profiler stats
            var allStats = PerformanceProfiler.GetAllStats();

            foreach (var stat in allStats)
            {
                var systemMetric = new SystemMetrics
                {
                    SystemName = stat.Key,
                    AverageTime = stat.Value.AverageTimeMs,
                    MaxTime = stat.Value.MaxTimeMs,
                    SampleCount = stat.Value.SampleCount,
                    IsWithinTarget = stat.Value.IsWithinTarget,
                    PerformanceRatio = stat.Value.PerformanceRatio,
                    LastUpdated = DateTime.UtcNow
                };

                _systemMetrics[stat.Key] = systemMetric;
            }
        }

        /// <summary>
        /// Gets the current number of active voters in the simulation.
        /// </summary>
        private int GetCurrentVoterCount()
        {
            // This would integrate with the ECS world to get actual voter count
            // For now, return a placeholder value
            try
            {
                // In a real implementation, this would query the ECS world
                // var world = World.DefaultGameObjectInjectionWorld;
                // if (world != null)
                // {
                //     var voterQuery = world.EntityManager.CreateEntityQuery(typeof(VoterData));
                //     return voterQuery.CalculateEntityCount();
                // }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets current CPU usage percentage.
        /// </summary>
        private float GetCPUUsage()
        {
            // Placeholder - would integrate with platform-specific CPU monitoring
            return Profiler.GetMonoUsedSizeLong() / (1024f * 1024f * 10f); // Rough approximation
        }

        /// <summary>
        /// Gets current rendering time in milliseconds.
        /// </summary>
        private float GetRenderingTime()
        {
            // Use Unity's built-in profiler to get rendering time
            return Profiler.GetCounterValue("GPU.GPU.Time") / 1000000f; // Convert nanoseconds to ms
        }

        /// <summary>
        /// Logs a performance event for analysis.
        /// </summary>
        private void LogPerformanceEvent(string metric, string severity, float value)
        {
            var logEntry = $"[PERFORMANCE] {severity}: {metric} = {value:F2} at {DateTime.UtcNow:HH:mm:ss}";

            if (severity == "Critical")
            {
                Debug.LogError(logEntry);
            }
            else
            {
                Debug.LogWarning(logEntry);
            }

            // In a production system, this could also send telemetry data
        }

        /// <summary>
        /// Draws the performance monitoring UI window.
        /// </summary>
        private void DrawPerformanceWindow(int windowID)
        {
            GUILayout.BeginVertical();

            // Current performance metrics
            if (_performanceSamples.Count > 0)
            {
                var latest = _performanceSamples.Last();

                GUILayout.Label($"FPS: {latest.FPS:F1}", GetStyleForValue(latest.FPS, 60f, 30f));
                GUILayout.Label($"Frame Time: {latest.FrameTime:F1}ms", GetStyleForValue(latest.FrameTime, frameTimeWarningThreshold, frameTimeCriticalThreshold, true));
                GUILayout.Label($"Memory: {latest.TotalMemoryMB:F1}MB", GetStyleForValue(latest.TotalMemoryMB, memoryWarningThreshold, memoryCriticalThreshold, true));
                GUILayout.Label($"Voters: {latest.VoterCount}");

                if (latest.CPUUsage > 0)
                {
                    GUILayout.Label($"CPU: {latest.CPUUsage:F1}%");
                }
            }

            GUILayout.Space(10);

            // Toggle detailed metrics
            if (GUILayout.Button(_showDetailedMetrics ? "Hide Details" : "Show Details"))
            {
                _showDetailedMetrics = !_showDetailedMetrics;
            }

            if (_showDetailedMetrics)
            {
                GUILayout.Label("System Metrics:", GUI.skin.box);

                foreach (var metric in _systemMetrics.Values.Take(5)) // Show top 5 systems
                {
                    var statusColor = metric.IsWithinTarget ? Color.green : Color.red;
                    var originalColor = GUI.color;
                    GUI.color = statusColor;

                    GUILayout.Label($"{metric.SystemName}: {metric.AverageTime:F1}ms");

                    GUI.color = originalColor;
                }
            }

            // Performance trend indicators
            if (_performanceSamples.Count >= 10)
            {
                var recent = _performanceSamples.TakeLast(10).Average(s => s.FrameTime);
                var older = _performanceSamples.Skip(Math.Max(0, _performanceSamples.Count - 20)).Take(10).Average(s => s.FrameTime);

                var trend = recent > older * 1.05f ? "↗" : recent < older * 0.95f ? "↘" : "→";
                GUILayout.Label($"Trend: {trend}");
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        /// <summary>
        /// Gets GUI style based on performance value thresholds.
        /// </summary>
        private GUIStyle GetStyleForValue(float value, float warningThreshold, float criticalThreshold, bool higherIsBad = false)
        {
            var style = new GUIStyle(GUI.skin.label);

            bool isWarning, isCritical;

            if (higherIsBad)
            {
                isWarning = value > warningThreshold;
                isCritical = value > criticalThreshold;
            }
            else
            {
                isWarning = value < warningThreshold;
                isCritical = value < criticalThreshold;
            }

            if (isCritical)
            {
                style.normal.textColor = Color.red;
            }
            else if (isWarning)
            {
                style.normal.textColor = Color.yellow;
            }
            else
            {
                style.normal.textColor = Color.green;
            }

            return style;
        }

        /// <summary>
        /// Gets current performance summary.
        /// </summary>
        public PerformanceSummary GetPerformanceSummary()
        {
            if (_performanceSamples.Count == 0)
                return new PerformanceSummary();

            var recentSamples = _performanceSamples.TakeLast(60).ToArray(); // Last minute of data

            return new PerformanceSummary
            {
                AverageFrameTime = recentSamples.Average(s => s.FrameTime),
                AverageFPS = recentSamples.Average(s => s.FPS),
                MaxFrameTime = recentSamples.Max(s => s.FrameTime),
                MinFPS = recentSamples.Min(s => s.FPS),
                AverageMemoryMB = recentSamples.Average(s => s.TotalMemoryMB),
                CurrentVoterCount = _currentVoterCount,
                IsPerformanceHealthy = recentSamples.All(s => s.FrameTime <= frameTimeWarningThreshold && s.TotalMemoryMB <= memoryWarningThreshold),
                SampleCount = recentSamples.Length
            };
        }

        /// <summary>
        /// Exports performance data for analysis.
        /// </summary>
        public string ExportPerformanceData()
        {
            var csv = "Timestamp,FrameTime,FPS,TotalMemoryMB,AllocatedMemoryMB,VoterCount,CPUUsage,RenderingTime\n";

            foreach (var sample in _performanceSamples)
            {
                csv += $"{sample.Timestamp:F2},{sample.FrameTime:F2},{sample.FPS:F1},{sample.TotalMemoryMB:F1},{sample.AllocatedMemoryMB:F1},{sample.VoterCount},{sample.CPUUsage:F1},{sample.RenderingTime:F2}\n";
            }

            return csv;
        }

        // Data structures
        private struct PerformanceSample
        {
            public float Timestamp;
            public float FrameTime;
            public float FPS;
            public float TotalMemoryMB;
            public float AllocatedMemoryMB;
            public int VoterCount;
            public float CPUUsage;
            public float RenderingTime;
        }

        private struct SystemMetrics
        {
            public string SystemName;
            public double AverageTime;
            public double MaxTime;
            public int SampleCount;
            public bool IsWithinTarget;
            public float PerformanceRatio;
            public DateTime LastUpdated;
        }

        public struct PerformanceSummary
        {
            public float AverageFrameTime;
            public float AverageFPS;
            public float MaxFrameTime;
            public float MinFPS;
            public float AverageMemoryMB;
            public int CurrentVoterCount;
            public bool IsPerformanceHealthy;
            public int SampleCount;
        }
    }
}