using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Core.EventBus
{
    /// <summary>
    /// High-performance event bus system for cross-bounded context communication.
    /// Implements Domain-Driven Design principles with anti-corruption layers
    /// and maintains loose coupling between political, AI, database, and UI contexts.
    /// </summary>
    public class EventBusSystem : IDisposable
    {
        private static readonly ProfilerMarker EventBusMarker = new("SovereignsDilemma.EventBus");

        // Event subscription management
        private readonly ConcurrentDictionary<Type, List<IEventHandler>> _handlers;
        private readonly ConcurrentQueue<IEvent> _eventQueue;
        private readonly Dictionary<string, BoundedContext> _contexts;

        // Performance configuration
        private const int MAX_EVENTS_PER_FRAME = 100;
        private const int EVENT_QUEUE_SIZE_WARNING = 1000;
        private const float EVENT_PROCESSING_TIME_WARNING_MS = 5.0f;

        // Metrics tracking
        private int _eventsProcessedThisFrame;
        private int _totalEventsProcessed;
        private float _lastPerformanceUpdate;
        private readonly Dictionary<Type, EventMetrics> _eventMetrics;

        // Context isolation
        private readonly object _handlerLock = new object();
        private bool _isProcessingEvents = false;

        public EventBusSystem()
        {
            _handlers = new ConcurrentDictionary<Type, List<IEventHandler>>();
            _eventQueue = new ConcurrentQueue<IEvent>();
            _contexts = new Dictionary<string, BoundedContext>();
            _eventMetrics = new Dictionary<Type, EventMetrics>();

            InitializeBoundedContexts();

            Debug.Log("Event Bus System initialized with bounded context isolation");
        }

        private void InitializeBoundedContexts()
        {
            // Political Context - Voter behavior, opinions, social networks
            RegisterContext(new BoundedContext
            {
                Name = "Political",
                Description = "Voter simulation and political dynamics",
                AllowedEventTypes = new HashSet<Type>
                {
                    typeof(VoterOpinionChangedEvent),
                    typeof(VoterBehaviorChangedEvent),
                    typeof(SocialNetworkUpdatedEvent),
                    typeof(PoliticalEventOccurredEvent)
                },
                AntiCorruptionLayer = new PoliticalAntiCorruptionLayer()
            });

            // AI Context - AI analysis, predictions, and recommendations
            RegisterContext(new BoundedContext
            {
                Name = "AI",
                Description = "AI analysis and behavior prediction",
                AllowedEventTypes = new HashSet<Type>
                {
                    typeof(AIAnalysisRequestedEvent),
                    typeof(AIAnalysisCompletedEvent),
                    typeof(AIBatchProcessedEvent),
                    typeof(AIModelUpdatedEvent)
                },
                AntiCorruptionLayer = new AIAntiCorruptionLayer()
            });

            // Database Context - Data persistence and retrieval
            RegisterContext(new BoundedContext
            {
                Name = "Database",
                Description = "Data persistence and synchronization",
                AllowedEventTypes = new HashSet<Type>
                {
                    typeof(VoterDataSavedEvent),
                    typeof(DatabaseBatchCompletedEvent),
                    typeof(DatabaseConnectionEvent),
                    typeof(DataCorruptionDetectedEvent)
                },
                AntiCorruptionLayer = new DatabaseAntiCorruptionLayer()
            });

            // UI Context - User interface and visualization
            RegisterContext(new BoundedContext
            {
                Name = "UI",
                Description = "User interface and data visualization",
                AllowedEventTypes = new HashSet<Type>
                {
                    typeof(UIDataUpdatedEvent),
                    typeof(UserInteractionEvent),
                    typeof(VisualizationUpdatedEvent),
                    typeof(PerformanceDisplayEvent)
                },
                AntiCorruptionLayer = new UIAntiCorruptionLayer()
            });

            // Performance Context - System monitoring and optimization
            RegisterContext(new BoundedContext
            {
                Name = "Performance",
                Description = "System performance monitoring and adaptation",
                AllowedEventTypes = new HashSet<Type>
                {
                    typeof(PerformanceMetricsUpdatedEvent),
                    typeof(AdaptiveScalingEvent),
                    typeof(SystemHealthChangedEvent),
                    typeof(ResourceUtilizationEvent)
                },
                AntiCorruptionLayer = new PerformanceAntiCorruptionLayer()
            });

            Debug.Log($"Initialized {_contexts.Count} bounded contexts");
        }

        private void RegisterContext(BoundedContext context)
        {
            _contexts[context.Name] = context;
            Debug.Log($"Registered bounded context: {context.Name} - {context.Description}");
        }

        /// <summary>
        /// Subscribe to events with bounded context validation.
        /// </summary>
        public void Subscribe<T>(IEventHandler<T> handler, string boundedContext = null) where T : class, IEvent
        {
            var eventType = typeof(T);

            // Validate bounded context permissions
            if (boundedContext != null && !ValidateContextPermission(boundedContext, eventType))
            {
                throw new UnauthorizedAccessException(
                    $"Context '{boundedContext}' not authorized to handle event type '{eventType.Name}'");
            }

            lock (_handlerLock)
            {
                if (!_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType] = new List<IEventHandler>();
                }

                _handlers[eventType].Add(handler);
            }

            Debug.Log($"Subscribed {handler.GetType().Name} to {eventType.Name} (Context: {boundedContext ?? "Global"})");
        }

        /// <summary>
        /// Unsubscribe from events.
        /// </summary>
        public void Unsubscribe<T>(IEventHandler<T> handler) where T : class, IEvent
        {
            var eventType = typeof(T);

            lock (_handlerLock)
            {
                if (_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType].Remove(handler);

                    if (_handlers[eventType].Count == 0)
                    {
                        _handlers.TryRemove(eventType, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Publish event with bounded context validation and anti-corruption layer processing.
        /// </summary>
        public void Publish<T>(T eventData, string sourceBoundedContext = null) where T : class, IEvent
        {
            if (eventData == null) return;

            // Apply anti-corruption layer transformation if source context specified
            var processedEvent = ApplyAntiCorruptionLayer(eventData, sourceBoundedContext);

            // Queue event for processing
            _eventQueue.Enqueue(processedEvent);

            // Check queue size for performance warnings
            if (_eventQueue.Count > EVENT_QUEUE_SIZE_WARNING)
            {
                Debug.LogWarning($"Event queue size ({_eventQueue.Count}) exceeds warning threshold");
            }

            PerformanceProfiler.RecordMeasurement("EventsPublished", 1f);
        }

        /// <summary>
        /// Process queued events with performance monitoring.
        /// </summary>
        public void Update()
        {
            if (_isProcessingEvents) return; // Prevent re-entrance

            using (EventBusMarker.Auto())
            {
                _isProcessingEvents = true;
                _eventsProcessedThisFrame = 0;

                var processingStartTime = Time.realtimeSinceStartup;

                try
                {
                    ProcessEventQueue();
                    UpdatePerformanceMetrics();
                }
                finally
                {
                    _isProcessingEvents = false;
                }

                var processingTime = (Time.realtimeSinceStartup - processingStartTime) * 1000f;
                if (processingTime > EVENT_PROCESSING_TIME_WARNING_MS)
                {
                    Debug.LogWarning($"Event processing took {processingTime:F2}ms (>{EVENT_PROCESSING_TIME_WARNING_MS}ms warning threshold)");
                }

                PerformanceProfiler.RecordMeasurement("EventProcessingTimeMs", processingTime);
            }
        }

        private void ProcessEventQueue()
        {
            while (_eventQueue.TryDequeue(out var eventData) && _eventsProcessedThisFrame < MAX_EVENTS_PER_FRAME)
            {
                ProcessSingleEvent(eventData);
                _eventsProcessedThisFrame++;
                _totalEventsProcessed++;
            }

            if (_eventsProcessedThisFrame >= MAX_EVENTS_PER_FRAME && !_eventQueue.IsEmpty)
            {
                Debug.LogWarning($"Event processing limit reached ({MAX_EVENTS_PER_FRAME} events/frame). {_eventQueue.Count} events remaining.");
            }
        }

        private void ProcessSingleEvent(IEvent eventData)
        {
            var eventType = eventData.GetType();
            var eventStartTime = Time.realtimeSinceStartup;

            try
            {
                if (_handlers.ContainsKey(eventType))
                {
                    var handlers = _handlers[eventType].ToArray(); // Copy to avoid modification during iteration

                    foreach (var handler in handlers)
                    {
                        try
                        {
                            handler.Handle(eventData);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Event handler error for {eventType.Name}: {ex.Message}");
                            RecordEventError(eventType, ex);
                        }
                    }
                }

                var processingTime = (Time.realtimeSinceStartup - eventStartTime) * 1000f;
                RecordEventMetrics(eventType, processingTime, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Event processing error for {eventType.Name}: {ex.Message}");
                RecordEventMetrics(eventType, 0f, false);
            }
        }

        private bool ValidateContextPermission(string contextName, Type eventType)
        {
            if (!_contexts.ContainsKey(contextName))
            {
                Debug.LogWarning($"Unknown bounded context: {contextName}");
                return false;
            }

            var context = _contexts[contextName];
            return context.AllowedEventTypes.Contains(eventType);
        }

        private IEvent ApplyAntiCorruptionLayer(IEvent eventData, string sourceContext)
        {
            if (sourceContext == null || !_contexts.ContainsKey(sourceContext))
                return eventData;

            var context = _contexts[sourceContext];
            return context.AntiCorruptionLayer?.Transform(eventData) ?? eventData;
        }

        private void RecordEventMetrics(Type eventType, float processingTimeMs, bool success)
        {
            if (!_eventMetrics.ContainsKey(eventType))
            {
                _eventMetrics[eventType] = new EventMetrics();
            }

            var metrics = _eventMetrics[eventType];
            metrics.TotalEvents++;
            if (success)
            {
                metrics.SuccessfulEvents++;
                metrics.TotalProcessingTime += processingTimeMs;
            }
            else
            {
                metrics.FailedEvents++;
            }
        }

        private void RecordEventError(Type eventType, Exception ex)
        {
            // In production, this would log to a proper error tracking system
            Debug.LogError($"Event processing error: {eventType.Name} - {ex}");
        }

        private void UpdatePerformanceMetrics()
        {
            var currentTime = Time.realtimeSinceStartup;

            if (currentTime - _lastPerformanceUpdate >= 1.0f)
            {
                PerformanceProfiler.RecordMeasurement("EventQueueSize", _eventQueue.Count);
                PerformanceProfiler.RecordMeasurement("EventsProcessedPerSecond", _eventsProcessedThisFrame);
                PerformanceProfiler.RecordMeasurement("TotalEventsProcessed", _totalEventsProcessed);

                // Log performance statistics periodically
                if (UnityEngine.Time.frameCount % 3600 == 0) // Every minute
                {
                    LogPerformanceStatistics();
                }

                _lastPerformanceUpdate = currentTime;
            }
        }

        private void LogPerformanceStatistics()
        {
            Debug.Log($"Event Bus Statistics:");
            Debug.Log($"  Total Events Processed: {_totalEventsProcessed}");
            Debug.Log($"  Queue Size: {_eventQueue.Count}");
            Debug.Log($"  Active Handlers: {_handlers.Values.Sum(list => list.Count)}");
            Debug.Log($"  Bounded Contexts: {_contexts.Count}");

            foreach (var kvp in _eventMetrics.Take(5)) // Top 5 event types
            {
                var metrics = kvp.Value;
                var avgTime = metrics.SuccessfulEvents > 0 ? metrics.TotalProcessingTime / metrics.SuccessfulEvents : 0f;
                Debug.Log($"  {kvp.Key.Name}: {metrics.TotalEvents} events, {avgTime:F2}ms avg, {metrics.SuccessRate:P} success");
            }
        }

        /// <summary>
        /// Get current event bus metrics for monitoring.
        /// </summary>
        public EventBusMetrics GetMetrics()
        {
            return new EventBusMetrics
            {
                QueueSize = _eventQueue.Count,
                TotalEventsProcessed = _totalEventsProcessed,
                EventsProcessedThisFrame = _eventsProcessedThisFrame,
                ActiveHandlerCount = _handlers.Values.Sum(list => list.Count),
                BoundedContextCount = _contexts.Count,
                EventTypeCount = _eventMetrics.Count
            };
        }

        /// <summary>
        /// Flush all pending events (for shutdown or testing).
        /// </summary>
        public void FlushEvents()
        {
            Debug.Log($"Flushing {_eventQueue.Count} pending events");

            while (_eventQueue.TryDequeue(out var eventData))
            {
                ProcessSingleEvent(eventData);
            }

            Debug.Log("Event queue flushed");
        }

        public void Dispose()
        {
            FlushEvents();

            lock (_handlerLock)
            {
                _handlers.Clear();
            }

            _contexts.Clear();
            _eventMetrics.Clear();

            Debug.Log("Event Bus System disposed");
        }

        // Data structures
        public struct EventBusMetrics
        {
            public int QueueSize;
            public int TotalEventsProcessed;
            public int EventsProcessedThisFrame;
            public int ActiveHandlerCount;
            public int BoundedContextCount;
            public int EventTypeCount;
        }

        private class EventMetrics
        {
            public int TotalEvents;
            public int SuccessfulEvents;
            public int FailedEvents;
            public float TotalProcessingTime;

            public float SuccessRate => TotalEvents > 0 ? (float)SuccessfulEvents / TotalEvents : 0f;
        }

        private class BoundedContext
        {
            public string Name;
            public string Description;
            public HashSet<Type> AllowedEventTypes;
            public IAntiCorruptionLayer AntiCorruptionLayer;
        }
    }

    // Interfaces
    public interface IEvent
    {
        DateTime Timestamp { get; }
        string EventId { get; }
        string SourceContext { get; }
    }

    public interface IEventHandler
    {
        void Handle(IEvent eventData);
    }

    public interface IEventHandler<in T> : IEventHandler where T : IEvent
    {
        void Handle(T eventData);
    }

    public interface IAntiCorruptionLayer
    {
        IEvent Transform(IEvent eventData);
    }

    // Base event implementation
    public abstract class BaseEvent : IEvent
    {
        public DateTime Timestamp { get; protected set; } = DateTime.UtcNow;
        public string EventId { get; protected set; } = Guid.NewGuid().ToString();
        public string SourceContext { get; protected set; }

        protected BaseEvent(string sourceContext = null)
        {
            SourceContext = sourceContext;
        }
    }
}