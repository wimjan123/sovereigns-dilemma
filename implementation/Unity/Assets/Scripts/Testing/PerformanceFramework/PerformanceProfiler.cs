using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace SovereignsDilemma.Testing.Performance
{
    /// <summary>
    /// Performance profiling framework for The Sovereign's Dilemma.
    /// Tracks simulation performance metrics and provides benchmarking capabilities.
    /// </summary>
    public static class PerformanceProfiler
    {
        private static readonly Dictionary<string, ProfilerMarker> _markers = new();
        private static readonly Dictionary<string, List<float>> _measurements = new();
        private static readonly Dictionary<string, float> _targets = new();

        // Core simulation markers
        public static readonly ProfilerMarker VoterUpdateMarker = new("SovereignsDilemma.Voters.Update");
        public static readonly ProfilerMarker PoliticalAnalysisMarker = new("SovereignsDilemma.AI.PoliticalAnalysis");
        public static readonly ProfilerMarker EventProcessingMarker = new("SovereignsDilemma.Events.Processing");
        public static readonly ProfilerMarker NetworkSyncMarker = new("SovereignsDilemma.Network.Sync");
        public static readonly ProfilerMarker DatabaseQueryMarker = new("SovereignsDilemma.Database.Query");

        // Performance targets (in milliseconds)
        private static readonly Dictionary<string, float> DefaultTargets = new()
        {
            { "VoterUpdate", 16.67f },        // 60 FPS target
            { "PoliticalAnalysis", 100.0f },  // AI analysis should complete within 100ms
            { "EventProcessing", 5.0f },      // Event processing under 5ms
            { "NetworkSync", 50.0f },         // Network sync under 50ms
            { "DatabaseQuery", 10.0f }        // Database queries under 10ms
        };

        static PerformanceProfiler()
        {
            foreach (var target in DefaultTargets)
            {
                SetPerformanceTarget(target.Key, target.Value);
            }
        }

        /// <summary>
        /// Sets a performance target for a specific operation.
        /// </summary>
        public static void SetPerformanceTarget(string operationName, float targetTimeMs)
        {
            _targets[operationName] = targetTimeMs;
        }

        /// <summary>
        /// Begins profiling an operation with custom profiler marker.
        /// </summary>
        public static IDisposable BeginSample(string operationName)
        {
            if (!_markers.TryGetValue(operationName, out var marker))
            {
                marker = new ProfilerMarker($"SovereignsDilemma.{operationName}");
                _markers[operationName] = marker;
            }

            return new ProfilerScope(marker, operationName);
        }

        /// <summary>
        /// Records a performance measurement for analysis.
        /// </summary>
        public static void RecordMeasurement(string operationName, float timeMs)
        {
            if (!_measurements.TryGetValue(operationName, out var measurements))
            {
                measurements = new List<float>();
                _measurements[operationName] = measurements;
            }

            measurements.Add(timeMs);

            // Keep only last 1000 measurements to prevent memory bloat
            if (measurements.Count > 1000)
            {
                measurements.RemoveAt(0);
            }

            // Check against performance target
            if (_targets.TryGetValue(operationName, out var target) && timeMs > target)
            {
                Debug.LogWarning($"Performance warning: {operationName} took {timeMs:F2}ms (target: {target:F2}ms)");
            }
        }

        /// <summary>
        /// Gets performance statistics for a specific operation.
        /// </summary>
        public static PerformanceStats GetStats(string operationName)
        {
            if (!_measurements.TryGetValue(operationName, out var measurements) || measurements.Count == 0)
            {
                return new PerformanceStats
                {
                    OperationName = operationName,
                    SampleCount = 0,
                    AverageTimeMs = 0,
                    MinTimeMs = 0,
                    MaxTimeMs = 0,
                    Target = _targets.GetValueOrDefault(operationName, 0)
                };
            }

            var sorted = measurements.OrderBy(x => x).ToArray();
            return new PerformanceStats
            {
                OperationName = operationName,
                SampleCount = measurements.Count,
                AverageTimeMs = measurements.Average(),
                MinTimeMs = measurements.Min(),
                MaxTimeMs = measurements.Max(),
                P50TimeMs = sorted[sorted.Length / 2],
                P95TimeMs = sorted[(int)(sorted.Length * 0.95f)],
                P99TimeMs = sorted[(int)(sorted.Length * 0.99f)],
                Target = _targets.GetValueOrDefault(operationName, 0)
            };
        }

        /// <summary>
        /// Gets performance statistics for all tracked operations.
        /// </summary>
        public static Dictionary<string, PerformanceStats> GetAllStats()
        {
            var results = new Dictionary<string, PerformanceStats>();
            foreach (var operation in _measurements.Keys)
            {
                results[operation] = GetStats(operation);
            }
            return results;
        }

        /// <summary>
        /// Clears all performance measurements.
        /// </summary>
        public static void ClearMeasurements()
        {
            _measurements.Clear();
        }

        /// <summary>
        /// Generates a performance report in JSON format.
        /// </summary>
        public static string GenerateReport()
        {
            var stats = GetAllStats();
            var report = new PerformanceReport
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 1000.0f / Time.smoothDeltaTime,
                Operations = stats.Values.ToArray()
            };

            return JsonUtility.ToJson(report, true);
        }

        /// <summary>
        /// Logs a summary of current performance to Unity console.
        /// </summary>
        public static void LogSummary()
        {
            var stats = GetAllStats();
            Debug.Log("=== Performance Summary ===");
            Debug.Log($"Frame Rate: {1000.0f / Time.smoothDeltaTime:F1} FPS");

            foreach (var stat in stats.Values.OrderByDescending(s => s.AverageTimeMs))
            {
                var status = stat.AverageTimeMs <= stat.Target ? "✓" : "⚠";
                Debug.Log($"{status} {stat.OperationName}: {stat.AverageTimeMs:F2}ms avg (target: {stat.Target:F2}ms, samples: {stat.SampleCount})");
            }
        }

        private class ProfilerScope : IDisposable
        {
            private readonly ProfilerMarker.AutoScope _scope;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;

            public ProfilerScope(ProfilerMarker marker, string operationName)
            {
                _scope = marker.Auto();
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                RecordMeasurement(_operationName, (float)_stopwatch.Elapsed.TotalMilliseconds);
                _scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Performance statistics for a specific operation.
    /// </summary>
    [Serializable]
    public struct PerformanceStats
    {
        public string OperationName;
        public int SampleCount;
        public float AverageTimeMs;
        public float MinTimeMs;
        public float MaxTimeMs;
        public float P50TimeMs;
        public float P95TimeMs;
        public float P99TimeMs;
        public float Target;

        public bool IsWithinTarget => AverageTimeMs <= Target;
        public float PerformanceRatio => Target > 0 ? AverageTimeMs / Target : 0;
    }

    /// <summary>
    /// Complete performance report for CI/CD integration.
    /// </summary>
    [Serializable]
    public struct PerformanceReport
    {
        public DateTime Timestamp;
        public float FrameRate;
        public PerformanceStats[] Operations;
    }
}