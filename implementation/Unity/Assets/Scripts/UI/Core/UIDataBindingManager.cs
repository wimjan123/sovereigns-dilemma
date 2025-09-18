using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Unity.Profiling;

namespace SovereignsDilemma.UI.Core
{
    /// <summary>
    /// Efficient data binding manager for UI components.
    /// Provides reactive updates with performance optimization and caching.
    /// </summary>
    public class UIDataBindingManager : IDisposable
    {
        private readonly ProfilerMarker _bindingUpdateMarker = new("UIDataBinding.Update");

        // Data binding subscriptions
        private readonly Dictionary<Type, List<IUIComponent>> _componentSubscriptions;
        private readonly Dictionary<Type, object> _cachedData;
        private readonly Dictionary<Type, DateTime> _lastUpdateTimes;
        private readonly Dictionary<Type, float> _updateIntervals;

        // Update queuing system for performance
        private readonly ConcurrentQueue<Type> _updateQueue;
        private readonly HashSet<Type> _queuedTypes;
        private readonly object _queueLock = new object();

        // Performance optimization
        private const int MAX_UPDATES_PER_FRAME = 10;
        private int _updatesThisFrame;
        private float _lastFrameTime;

        // Reactive data change detection
        private readonly Dictionary<Type, IDataChangeDetector> _changeDetectors;

        public UIDataBindingManager()
        {
            _componentSubscriptions = new Dictionary<Type, List<IUIComponent>>();
            _cachedData = new Dictionary<Type, object>();
            _lastUpdateTimes = new Dictionary<Type, DateTime>();
            _updateIntervals = new Dictionary<Type, float>();
            _updateQueue = new ConcurrentQueue<Type>();
            _queuedTypes = new HashSet<Type>();
            _changeDetectors = new Dictionary<Type, IDataChangeDetector>();

            InitializeDefaultIntervals();
            Debug.Log("UIDataBindingManager initialized");
        }

        private void InitializeDefaultIntervals()
        {
            // Set default update intervals for different data types
            SetUpdateInterval<VoterAnalyticsData>(0.5f); // 2 FPS
            SetUpdateInterval<PoliticalSpectrumData>(0.3f); // ~3 FPS
            SetUpdateInterval<SocialMediaData>(1.0f); // 1 FPS
            SetUpdateInterval<PerformanceData>(0.1f); // 10 FPS
            SetUpdateInterval<UIConfigurationData>(2.0f); // 0.5 FPS
            SetUpdateInterval<AIServiceConfigurationData>(1.0f); // 1 FPS
        }

        #region Public Interface

        /// <summary>
        /// Subscribe a UI component to receive updates for a specific data type.
        /// </summary>
        public void Subscribe<T>(IUIComponent component) where T : IUIData
        {
            var dataType = typeof(T);

            if (!_componentSubscriptions.ContainsKey(dataType))
            {
                _componentSubscriptions[dataType] = new List<IUIComponent>();
                _changeDetectors[dataType] = CreateChangeDetector<T>();
            }

            if (!_componentSubscriptions[dataType].Contains(component))
            {
                _componentSubscriptions[dataType].Add(component);
                Debug.Log($"Component {component.GetType().Name} subscribed to {dataType.Name}");
            }
        }

        /// <summary>
        /// Unsubscribe a UI component from data updates.
        /// </summary>
        public void Unsubscribe<T>(IUIComponent component) where T : IUIData
        {
            var dataType = typeof(T);

            if (_componentSubscriptions.ContainsKey(dataType))
            {
                _componentSubscriptions[dataType].Remove(component);

                if (_componentSubscriptions[dataType].Count == 0)
                {
                    _componentSubscriptions.Remove(dataType);
                    _changeDetectors.Remove(dataType);
                    _cachedData.Remove(dataType);
                }
            }
        }

        /// <summary>
        /// Set the update interval for a specific data type.
        /// </summary>
        public void SetUpdateInterval<T>(float interval) where T : IUIData
        {
            var dataType = typeof(T);
            _updateIntervals[dataType] = interval;
        }

        /// <summary>
        /// Queue an update for a specific data type.
        /// </summary>
        public void QueueUpdate<T>() where T : IUIData
        {
            var dataType = typeof(T);

            lock (_queueLock)
            {
                if (!_queuedTypes.Contains(dataType))
                {
                    _updateQueue.Enqueue(dataType);
                    _queuedTypes.Add(dataType);
                }
            }
        }

        /// <summary>
        /// Update data for a specific type and notify subscribed components.
        /// </summary>
        public void UpdateData<T>(T data) where T : IUIData
        {
            var dataType = typeof(T);

            // Check if data has actually changed
            if (_changeDetectors.ContainsKey(dataType))
            {
                var detector = _changeDetectors[dataType] as IDataChangeDetector<T>;
                if (detector != null && !detector.HasChanged(data, GetCachedData<T>()))
                {
                    return; // No change, skip update
                }
            }

            // Cache the new data
            _cachedData[dataType] = data;
            _lastUpdateTimes[dataType] = DateTime.UtcNow;

            // Notify subscribed components
            if (_componentSubscriptions.ContainsKey(dataType))
            {
                foreach (var component in _componentSubscriptions[dataType])
                {
                    try
                    {
                        component.UpdateData(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error updating component {component.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Get cached data for a specific type.
        /// </summary>
        public T GetCachedData<T>() where T : IUIData
        {
            var dataType = typeof(T);

            if (_cachedData.ContainsKey(dataType) && _cachedData[dataType] is T data)
            {
                return data;
            }

            return default(T);
        }

        /// <summary>
        /// Check if cached data exists for a type and is within update interval.
        /// </summary>
        public bool HasValidCachedData<T>() where T : IUIData
        {
            var dataType = typeof(T);

            if (!_cachedData.ContainsKey(dataType) || !_lastUpdateTimes.ContainsKey(dataType))
                return false;

            var interval = _updateIntervals.ContainsKey(dataType) ? _updateIntervals[dataType] : 1.0f;
            var timeSinceUpdate = (DateTime.UtcNow - _lastUpdateTimes[dataType]).TotalSeconds;

            return timeSinceUpdate < interval;
        }

        #endregion

        #region Update Processing

        /// <summary>
        /// Main update method - should be called from MonoBehaviour Update.
        /// </summary>
        public void Update()
        {
            using (_bindingUpdateMarker.Auto())
            {
                ResetFrameCounters();
                ProcessUpdateQueue();
                ProcessScheduledUpdates();
            }
        }

        private void ResetFrameCounters()
        {
            if (Time.time != _lastFrameTime)
            {
                _updatesThisFrame = 0;
                _lastFrameTime = Time.time;
            }
        }

        private void ProcessUpdateQueue()
        {
            // Process queued updates with frame budget
            while (_updatesThisFrame < MAX_UPDATES_PER_FRAME && _updateQueue.TryDequeue(out var dataType))
            {
                lock (_queueLock)
                {
                    _queuedTypes.Remove(dataType);
                }

                ProcessDataTypeUpdate(dataType);
                _updatesThisFrame++;
            }
        }

        private void ProcessScheduledUpdates()
        {
            // Process scheduled updates based on intervals
            var currentTime = DateTime.UtcNow;

            foreach (var kvp in _updateIntervals)
            {
                if (_updatesThisFrame >= MAX_UPDATES_PER_FRAME) break;

                var dataType = kvp.Key;
                var interval = kvp.Value;

                if (!_lastUpdateTimes.ContainsKey(dataType))
                {
                    _lastUpdateTimes[dataType] = currentTime.AddSeconds(-interval); // Force initial update
                }

                var timeSinceUpdate = (currentTime - _lastUpdateTimes[dataType]).TotalSeconds;

                if (timeSinceUpdate >= interval)
                {
                    QueueUpdateForType(dataType);
                }
            }
        }

        private void ProcessDataTypeUpdate(Type dataType)
        {
            // This method would typically gather fresh data from simulation systems
            // For now, it's a placeholder that would be implemented based on the specific data type

            try
            {
                if (dataType == typeof(PerformanceData))
                {
                    var perfData = GatherPerformanceData();
                    UpdateData(perfData);
                }
                else if (dataType == typeof(VoterAnalyticsData))
                {
                    // Would gather from voter system
                    // var voterData = GatherVoterAnalyticsData();
                    // UpdateData(voterData);
                }
                // Add other data type processing as needed
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing update for {dataType.Name}: {ex.Message}");
            }
        }

        private void QueueUpdateForType(Type dataType)
        {
            lock (_queueLock)
            {
                if (!_queuedTypes.Contains(dataType))
                {
                    _updateQueue.Enqueue(dataType);
                    _queuedTypes.Add(dataType);
                }
            }
        }

        #endregion

        #region Data Gathering

        private PerformanceData GatherPerformanceData()
        {
            var data = new PerformanceData(true);
            data.CurrentFPS = 1f / Time.unscaledDeltaTime;
            data.MemoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(false) / (1024f * 1024f);
            data.HasData = true;
            data.LastUpdated = DateTime.UtcNow;
            return data;
        }

        #endregion

        #region Change Detection

        private IDataChangeDetector CreateChangeDetector<T>() where T : IUIData
        {
            // Create appropriate change detector based on data type
            var dataType = typeof(T);

            if (dataType == typeof(PerformanceData))
                return new PerformanceDataChangeDetector() as IDataChangeDetector;
            else if (dataType == typeof(VoterAnalyticsData))
                return new VoterAnalyticsDataChangeDetector() as IDataChangeDetector;
            else if (dataType == typeof(PoliticalSpectrumData))
                return new PoliticalSpectrumDataChangeDetector() as IDataChangeDetector;

            // Default change detector
            return new DefaultDataChangeDetector<T>();
        }

        private interface IDataChangeDetector
        {
            bool HasChanged(object newData, object cachedData);
        }

        private interface IDataChangeDetector<T> : IDataChangeDetector where T : IUIData
        {
            bool HasChanged(T newData, T cachedData);
        }

        private class DefaultDataChangeDetector<T> : IDataChangeDetector<T> where T : IUIData
        {
            public bool HasChanged(T newData, T cachedData)
            {
                // Default implementation - always consider changed if new data exists
                return newData.HasData;
            }

            public bool HasChanged(object newData, object cachedData)
            {
                if (newData is T typedNewData && cachedData is T typedCachedData)
                    return HasChanged(typedNewData, typedCachedData);
                return true;
            }
        }

        private class PerformanceDataChangeDetector : IDataChangeDetector<PerformanceData>
        {
            private const float FPS_CHANGE_THRESHOLD = 2f;
            private const float MEMORY_CHANGE_THRESHOLD = 10f; // MB

            public bool HasChanged(PerformanceData newData, PerformanceData cachedData)
            {
                if (!newData.HasData) return false;
                if (!cachedData.HasData) return true;

                return Math.Abs(newData.CurrentFPS - cachedData.CurrentFPS) > FPS_CHANGE_THRESHOLD ||
                       Math.Abs(newData.MemoryUsage - cachedData.MemoryUsage) > MEMORY_CHANGE_THRESHOLD ||
                       newData.EventQueueSize != cachedData.EventQueueSize;
            }

            public bool HasChanged(object newData, object cachedData)
            {
                if (newData is PerformanceData typedNewData && cachedData is PerformanceData typedCachedData)
                    return HasChanged(typedNewData, typedCachedData);
                return true;
            }
        }

        private class VoterAnalyticsDataChangeDetector : IDataChangeDetector<VoterAnalyticsData>
        {
            public bool HasChanged(VoterAnalyticsData newData, VoterAnalyticsData cachedData)
            {
                if (!newData.HasData) return false;
                if (!cachedData.HasData) return true;

                return newData.TotalVoters != cachedData.TotalVoters ||
                       newData.ActiveVoters != cachedData.ActiveVoters ||
                       Math.Abs(newData.AveragePoliticalEngagement - cachedData.AveragePoliticalEngagement) > 0.01f;
            }

            public bool HasChanged(object newData, object cachedData)
            {
                if (newData is VoterAnalyticsData typedNewData && cachedData is VoterAnalyticsData typedCachedData)
                    return HasChanged(typedNewData, typedCachedData);
                return true;
            }
        }

        private class PoliticalSpectrumDataChangeDetector : IDataChangeDetector<PoliticalSpectrumData>
        {
            private const float SPECTRUM_CHANGE_THRESHOLD = 0.005f; // 0.5% change threshold

            public bool HasChanged(PoliticalSpectrumData newData, PoliticalSpectrumData cachedData)
            {
                if (!newData.HasData) return false;
                if (!cachedData.HasData) return true;

                // Check if political tension changed significantly
                if (Math.Abs(newData.PoliticalTension - cachedData.PoliticalTension) > 0.02f)
                    return true;

                // Check if median position shifted
                if (Vector2.Distance(newData.MedianPosition, cachedData.MedianPosition) > 0.01f)
                    return true;

                // Check spectrum distribution changes
                return HasSpectrumChanged(newData.EconomicSpectrum, cachedData.EconomicSpectrum) ||
                       HasSpectrumChanged(newData.SocialSpectrum, cachedData.SocialSpectrum) ||
                       HasSpectrumChanged(newData.EnvironmentalSpectrum, cachedData.EnvironmentalSpectrum);
            }

            private bool HasSpectrumChanged(float[] newSpectrum, float[] cachedSpectrum)
            {
                if (newSpectrum == null || cachedSpectrum == null) return true;
                if (newSpectrum.Length != cachedSpectrum.Length) return true;

                for (int i = 0; i < newSpectrum.Length; i++)
                {
                    if (Math.Abs(newSpectrum[i] - cachedSpectrum[i]) > SPECTRUM_CHANGE_THRESHOLD)
                        return true;
                }

                return false;
            }

            public bool HasChanged(object newData, object cachedData)
            {
                if (newData is PoliticalSpectrumData typedNewData && cachedData is PoliticalSpectrumData typedCachedData)
                    return HasChanged(typedNewData, typedCachedData);
                return true;
            }
        }

        #endregion

        #region Statistics and Monitoring

        public UIDataBindingStats GetStatistics()
        {
            return new UIDataBindingStats
            {
                SubscriptionCount = _componentSubscriptions.Count,
                CachedDataTypes = _cachedData.Count,
                QueuedUpdates = _updateQueue.Count,
                UpdatesThisFrame = _updatesThisFrame,
                TotalRegisteredTypes = _updateIntervals.Count
            };
        }

        public struct UIDataBindingStats
        {
            public int SubscriptionCount;
            public int CachedDataTypes;
            public int QueuedUpdates;
            public int UpdatesThisFrame;
            public int TotalRegisteredTypes;
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _componentSubscriptions.Clear();
            _cachedData.Clear();
            _lastUpdateTimes.Clear();
            _updateIntervals.Clear();
            _changeDetectors.Clear();

            // Clear queue
            while (_updateQueue.TryDequeue(out _)) { }
            _queuedTypes.Clear();

            Debug.Log("UIDataBindingManager disposed");
        }

        #endregion
    }
}