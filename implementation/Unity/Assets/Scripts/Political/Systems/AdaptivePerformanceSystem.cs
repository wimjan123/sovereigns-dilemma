using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Adaptive performance management system that dynamically adjusts simulation complexity
    /// based on hardware capabilities and current performance metrics.
    /// Ensures smooth gameplay across different hardware configurations.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(VoterSpawningSystem))]
    public partial class AdaptivePerformanceSystem : SystemBase
    {
        private static readonly ProfilerMarker AdaptivePerformanceMarker = new("SovereignsDilemma.AdaptivePerformance");

        private EntityQuery _activeVotersQuery;
        private VoterSpawningSystem _spawningSystem;

        // Performance tracking
        private readonly CircularBuffer<float> _frameTimeHistory = new(60); // 1 second of history at 60fps
        private readonly CircularBuffer<float> _memoryUsageHistory = new(30); // 30 samples of memory
        private float _lastPerformanceCheck;
        private int _targetVoterCount = 1000;
        private int _maxVoterCount = 10000;
        private PerformanceTier _currentTier = PerformanceTier.Medium;

        // Adaptive settings
        private float _updateFrequencyMultiplier = 1.0f;
        private float _aiRequestFrequencyMultiplier = 1.0f;
        private int _socialInfluenceComplexity = 3; // 1-5 scale
        private bool _enableAdvancedBehaviors = true;

        // Performance thresholds
        private const float TARGET_FRAME_TIME_MS = 16.67f; // 60 FPS
        private const float CRITICAL_FRAME_TIME_MS = 33.33f; // 30 FPS
        private const float WARNING_MEMORY_MB = 800f;
        private const float CRITICAL_MEMORY_MB = 1200f;

        protected override void OnCreate()
        {
            _activeVotersQuery = GetEntityQuery(typeof(VoterData), typeof(MemoryPool));
            _spawningSystem = World.GetOrCreateSystemManaged<VoterSpawningSystem>();

            // Detect hardware capabilities and set initial targets
            DetectHardwareCapabilities();
        }

        protected override void OnUpdate()
        {
            using (AdaptivePerformanceMarker.Auto())
            {
                var currentTime = Time.ElapsedTime;

                // Check performance every second
                if (currentTime - _lastPerformanceCheck >= 1.0f)
                {
                    AnalyzePerformance();
                    AdaptSimulationComplexity();
                    _lastPerformanceCheck = (float)currentTime;
                }

                // Track current frame performance
                var frameTimeMs = Time.DeltaTime * 1000f;
                _frameTimeHistory.Add(frameTimeMs);

                // Track memory usage periodically
                if (UnityEngine.Time.frameCount % 60 == 0) // Every second
                {
                    var memoryMB = System.GC.GetTotalMemory(false) / (1024f * 1024f);
                    _memoryUsageHistory.Add(memoryMB);
                }
            }
        }

        /// <summary>
        /// Detects hardware capabilities and sets initial performance targets.
        /// </summary>
        private void DetectHardwareCapabilities()
        {
            var processorCount = SystemInfo.processorCount;
            var systemMemoryMB = SystemInfo.systemMemorySize;
            var graphicsMemoryMB = SystemInfo.graphicsMemorySize;

            Debug.Log($"Hardware detected: {processorCount} cores, {systemMemoryMB}MB RAM, {graphicsMemoryMB}MB VRAM");

            // Classify hardware tier
            if (processorCount >= 8 && systemMemoryMB >= 8192)
            {
                _currentTier = PerformanceTier.High;
                _targetVoterCount = 5000;
                _maxVoterCount = 10000;
            }
            else if (processorCount >= 4 && systemMemoryMB >= 4096)
            {
                _currentTier = PerformanceTier.Medium;
                _targetVoterCount = 2000;
                _maxVoterCount = 5000;
            }
            else
            {
                _currentTier = PerformanceTier.Low;
                _targetVoterCount = 500;
                _maxVoterCount = 1000;
                _enableAdvancedBehaviors = false;
            }

            Debug.Log($"Performance tier: {_currentTier}, Target voters: {_targetVoterCount}");

            // Set quality settings based on tier
            ApplyQualitySettings();
        }

        /// <summary>
        /// Analyzes current performance metrics and identifies bottlenecks.
        /// </summary>
        private void AnalyzePerformance()
        {
            if (_frameTimeHistory.Count < 30) // Need sufficient history
                return;

            // Calculate performance statistics
            var avgFrameTime = _frameTimeHistory.Average();
            var maxFrameTime = _frameTimeHistory.Max();
            var p95FrameTime = _frameTimeHistory.Percentile(0.95f);

            var avgMemory = _memoryUsageHistory.Count > 0 ? _memoryUsageHistory.Average() : 0f;
            var maxMemory = _memoryUsageHistory.Count > 0 ? _memoryUsageHistory.Max() : 0f;

            var currentVoterCount = _activeVotersQuery.CalculateEntityCount();

            // Determine performance state
            var performanceState = PerformanceState.Good;

            if (avgFrameTime > CRITICAL_FRAME_TIME_MS || maxMemory > CRITICAL_MEMORY_MB)
            {
                performanceState = PerformanceState.Critical;
            }
            else if (avgFrameTime > TARGET_FRAME_TIME_MS * 1.5f || p95FrameTime > TARGET_FRAME_TIME_MS * 2f || maxMemory > WARNING_MEMORY_MB)
            {
                performanceState = PerformanceState.Warning;
            }
            else if (avgFrameTime < TARGET_FRAME_TIME_MS * 0.8f && maxMemory < WARNING_MEMORY_MB * 0.7f)
            {
                performanceState = PerformanceState.Excellent;
            }

            // Log performance analysis
            Debug.Log($"Performance Analysis: {performanceState} - Avg: {avgFrameTime:F1}ms, P95: {p95FrameTime:F1}ms, Memory: {avgMemory:F0}MB, Voters: {currentVoterCount}");

            // Record metrics for monitoring
            PerformanceProfiler.RecordMeasurement("AdaptiveFrameTime", avgFrameTime);
            PerformanceProfiler.RecordMeasurement("AdaptiveMemoryUsage", avgMemory);
            PerformanceProfiler.RecordMeasurement("AdaptiveVoterCount", currentVoterCount);

            // Take action based on performance state
            HandlePerformanceState(performanceState, currentVoterCount);
        }

        /// <summary>
        /// Handles different performance states by adjusting simulation parameters.
        /// </summary>
        private void HandlePerformanceState(PerformanceState state, int currentVoterCount)
        {
            switch (state)
            {
                case PerformanceState.Critical:
                    // Aggressive performance recovery
                    Debug.LogWarning("Critical performance detected - reducing simulation complexity");

                    _updateFrequencyMultiplier = math.max(0.25f, _updateFrequencyMultiplier * 0.7f);
                    _aiRequestFrequencyMultiplier = math.max(0.1f, _aiRequestFrequencyMultiplier * 0.5f);
                    _socialInfluenceComplexity = math.max(1, _socialInfluenceComplexity - 1);
                    _enableAdvancedBehaviors = false;

                    // Reduce voter count if necessary
                    if (currentVoterCount > _targetVoterCount * 0.5f)
                    {
                        var targetReduction = math.min(currentVoterCount / 4, 500);
                        ReduceVoterCount(targetReduction);
                    }
                    break;

                case PerformanceState.Warning:
                    // Moderate performance adjustment
                    Debug.LogWarning("Performance warning - making minor adjustments");

                    _updateFrequencyMultiplier = math.max(0.5f, _updateFrequencyMultiplier * 0.9f);
                    _aiRequestFrequencyMultiplier = math.max(0.3f, _aiRequestFrequencyMultiplier * 0.8f);

                    if (currentVoterCount > _targetVoterCount)
                    {
                        var targetReduction = math.min(currentVoterCount / 10, 200);
                        ReduceVoterCount(targetReduction);
                    }
                    break;

                case PerformanceState.Excellent:
                    // Performance headroom available - can increase complexity
                    if (currentVoterCount < _maxVoterCount)
                    {
                        _updateFrequencyMultiplier = math.min(1.0f, _updateFrequencyMultiplier * 1.05f);
                        _aiRequestFrequencyMultiplier = math.min(1.0f, _aiRequestFrequencyMultiplier * 1.1f);
                        _socialInfluenceComplexity = math.min(5, _socialInfluenceComplexity + 1);
                        _enableAdvancedBehaviors = true;

                        // Gradually increase voter count
                        var targetIncrease = math.min((_maxVoterCount - currentVoterCount) / 20, 100);
                        if (targetIncrease > 10)
                        {
                            IncreaseVoterCount(targetIncrease);
                        }
                    }
                    break;

                case PerformanceState.Good:
                    // Maintain current settings but allow minor optimizations
                    _updateFrequencyMultiplier = math.lerp(_updateFrequencyMultiplier, 1.0f, 0.1f);
                    _aiRequestFrequencyMultiplier = math.lerp(_aiRequestFrequencyMultiplier, 1.0f, 0.1f);
                    break;
            }

            // Apply the adjusted settings
            ApplyPerformanceSettings();
        }

        /// <summary>
        /// Applies the current performance settings to relevant systems.
        /// </summary>
        private void ApplyPerformanceSettings()
        {
            // Update system frequencies (would be implemented by other systems reading these values)
            SetGlobalPerformanceProperty("UpdateFrequencyMultiplier", _updateFrequencyMultiplier);
            SetGlobalPerformanceProperty("AIRequestFrequencyMultiplier", _aiRequestFrequencyMultiplier);
            SetGlobalPerformanceProperty("SocialInfluenceComplexity", _socialInfluenceComplexity);
            SetGlobalPerformanceProperty("EnableAdvancedBehaviors", _enableAdvancedBehaviors ? 1f : 0f);

            // Log current settings
            if (UnityEngine.Time.frameCount % 600 == 0) // Every 10 seconds
            {
                Debug.Log($"Adaptive settings: Update={_updateFrequencyMultiplier:F2}x, AI={_aiRequestFrequencyMultiplier:F2}x, " +
                         $"Social={_socialInfluenceComplexity}, Advanced={_enableAdvancedBehaviors}");
            }
        }

        /// <summary>
        /// Reduces the voter count by deactivating voters.
        /// </summary>
        private void ReduceVoterCount(int reduction)
        {
            var activeVoters = _activeVotersQuery.ToEntityArray(Allocator.Temp);
            var actualReduction = math.min(reduction, activeVoters.Length);

            Debug.Log($"Reducing voter count by {actualReduction} (requested: {reduction})");

            // Deactivate voters starting from the end (most recently created)
            for (int i = activeVoters.Length - 1; i >= activeVoters.Length - actualReduction; i--)
            {
                var entity = activeVoters[i];
                var stateRef = World.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                _spawningSystem.DespawnVoter(ref stateRef, entity);
            }

            activeVoters.Dispose();
        }

        /// <summary>
        /// Increases the voter count by spawning new voters.
        /// </summary>
        private void IncreaseVoterCount(int increase)
        {
            Debug.Log($"Increasing voter count by {increase}");

            var stateRef = World.Unmanaged.ResolveSystemStateRef(_spawningSystem);
            _spawningSystem.SpawnVoters(ref stateRef, increase);
        }

        /// <summary>
        /// Applies quality settings based on current performance tier.
        /// </summary>
        private void ApplyQualitySettings()
        {
            switch (_currentTier)
            {
                case PerformanceTier.High:
                    QualitySettings.vSyncCount = 1;
                    QualitySettings.antiAliasing = 2;
                    Application.targetFrameRate = 60;
                    break;

                case PerformanceTier.Medium:
                    QualitySettings.vSyncCount = 1;
                    QualitySettings.antiAliasing = 0;
                    Application.targetFrameRate = 60;
                    break;

                case PerformanceTier.Low:
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.antiAliasing = 0;
                    Application.targetFrameRate = 30;
                    break;
            }

            Debug.Log($"Applied quality settings for {_currentTier} tier");
        }

        /// <summary>
        /// Sets a global performance property that other systems can read.
        /// </summary>
        private void SetGlobalPerformanceProperty(string key, float value)
        {
            // In a real implementation, this would use a global configuration system
            PlayerPrefs.SetFloat($"AdaptivePerformance_{key}", value);
        }

        /// <summary>
        /// Gets adaptive performance metrics for debugging and monitoring.
        /// </summary>
        public AdaptivePerformanceMetrics GetMetrics()
        {
            return new AdaptivePerformanceMetrics
            {
                CurrentTier = _currentTier,
                TargetVoterCount = _targetVoterCount,
                MaxVoterCount = _maxVoterCount,
                UpdateFrequencyMultiplier = _updateFrequencyMultiplier,
                AIRequestFrequencyMultiplier = _aiRequestFrequencyMultiplier,
                SocialInfluenceComplexity = _socialInfluenceComplexity,
                EnableAdvancedBehaviors = _enableAdvancedBehaviors,
                AverageFrameTime = _frameTimeHistory.Count > 0 ? _frameTimeHistory.Average() : 0f,
                AverageMemoryUsage = _memoryUsageHistory.Count > 0 ? _memoryUsageHistory.Average() : 0f
            };
        }

        // Enums and data structures
        public enum PerformanceTier
        {
            Low,
            Medium,
            High
        }

        private enum PerformanceState
        {
            Critical,
            Warning,
            Good,
            Excellent
        }

        public struct AdaptivePerformanceMetrics
        {
            public PerformanceTier CurrentTier;
            public int TargetVoterCount;
            public int MaxVoterCount;
            public float UpdateFrequencyMultiplier;
            public float AIRequestFrequencyMultiplier;
            public int SocialInfluenceComplexity;
            public bool EnableAdvancedBehaviors;
            public float AverageFrameTime;
            public float AverageMemoryUsage;
        }

        /// <summary>
        /// Circular buffer for efficient performance history tracking.
        /// </summary>
        private class CircularBuffer<T>
        {
            private readonly T[] _buffer;
            private int _head;
            private int _count;

            public CircularBuffer(int capacity)
            {
                _buffer = new T[capacity];
                _head = 0;
                _count = 0;
            }

            public void Add(T item)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                _count = math.min(_count + 1, _buffer.Length);
            }

            public int Count => _count;

            public float Average()
            {
                if (_count == 0) return 0f;

                float sum = 0f;
                for (int i = 0; i < _count; i++)
                {
                    sum += Convert.ToSingle(_buffer[i]);
                }
                return sum / _count;
            }

            public float Max()
            {
                if (_count == 0) return 0f;

                float max = Convert.ToSingle(_buffer[0]);
                for (int i = 1; i < _count; i++)
                {
                    var value = Convert.ToSingle(_buffer[i]);
                    if (value > max) max = value;
                }
                return max;
            }

            public float Percentile(float percentile)
            {
                if (_count == 0) return 0f;

                var values = new float[_count];
                for (int i = 0; i < _count; i++)
                {
                    values[i] = Convert.ToSingle(_buffer[i]);
                }

                System.Array.Sort(values);
                var index = (int)((values.Length - 1) * percentile);
                return values[index];
            }
        }
    }

    /// <summary>
    /// Extension methods for other systems to read adaptive performance settings.
    /// </summary>
    public static class AdaptivePerformanceExtensions
    {
        public static float GetAdaptiveMultiplier(string settingName, float defaultValue = 1.0f)
        {
            return PlayerPrefs.GetFloat($"AdaptivePerformance_{settingName}", defaultValue);
        }

        public static bool GetAdvancedBehaviorsEnabled()
        {
            return PlayerPrefs.GetFloat("AdaptivePerformance_EnableAdvancedBehaviors", 1f) > 0.5f;
        }

        public static int GetSocialInfluenceComplexity()
        {
            return (int)PlayerPrefs.GetFloat("AdaptivePerformance_SocialInfluenceComplexity", 3f);
        }
    }
}