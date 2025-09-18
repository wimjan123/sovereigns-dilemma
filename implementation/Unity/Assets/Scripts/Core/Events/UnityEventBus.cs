using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SovereignsDilemma.Core.Events
{
    /// <summary>
    /// Unity-specific implementation of the event bus.
    /// Thread-safe event publishing and subscription for bounded contexts.
    /// </summary>
    public class UnityEventBus : MonoBehaviour, IEventBus
    {
        private readonly Dictionary<Type, List<IEventSubscription>> _subscriptions = new();
        private readonly Queue<IDomainEvent> _eventQueue = new();
        private readonly object _lockObject = new object();

        [Header("Event Bus Settings")]
        [SerializeField] private bool enableEventLogging = true;
        [SerializeField] private int maxQueueSize = 1000;
        [SerializeField] private bool processEventsOnMainThread = true;

        private void Update()
        {
            if (processEventsOnMainThread)
            {
                ProcessQueuedEvents();
            }
        }

        public void Publish<T>(T domainEvent) where T : IDomainEvent
        {
            if (domainEvent == null)
            {
                Debug.LogWarning("Attempted to publish null domain event");
                return;
            }

            if (enableEventLogging)
            {
                Debug.Log($"Publishing event: {typeof(T).Name} from {domainEvent.SourceContext}");
            }

            lock (_lockObject)
            {
                if (_eventQueue.Count >= maxQueueSize)
                {
                    Debug.LogWarning($"Event queue full ({maxQueueSize}), dropping oldest event");
                    _eventQueue.Dequeue();
                }

                _eventQueue.Enqueue(domainEvent);
            }

            if (!processEventsOnMainThread)
            {
                ProcessEventImmediate(domainEvent);
            }
        }

        public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
        {
            if (domainEvent == null)
            {
                Debug.LogWarning("Attempted to publish null domain event");
                return;
            }

            if (enableEventLogging)
            {
                Debug.Log($"Publishing async event: {typeof(T).Name} from {domainEvent.SourceContext}");
            }

            await ProcessEventAsync(domainEvent);
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : IDomainEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new EventSubscription<T>(handler, this);

            lock (_lockObject)
            {
                var eventType = typeof(T);
                if (!_subscriptions.ContainsKey(eventType))
                {
                    _subscriptions[eventType] = new List<IEventSubscription>();
                }
                _subscriptions[eventType].Add(subscription);
            }

            if (enableEventLogging)
            {
                Debug.Log($"Subscribed to event type: {typeof(T).Name}");
            }

            return subscription;
        }

        public IDisposable Subscribe<T>(Func<T, Task> handler) where T : IDomainEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new AsyncEventSubscription<T>(handler, this);

            lock (_lockObject)
            {
                var eventType = typeof(T);
                if (!_subscriptions.ContainsKey(eventType))
                {
                    _subscriptions[eventType] = new List<IEventSubscription>();
                }
                _subscriptions[eventType].Add(subscription);
            }

            if (enableEventLogging)
            {
                Debug.Log($"Subscribed to async event type: {typeof(T).Name}");
            }

            return subscription;
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _subscriptions.Clear();
                _eventQueue.Clear();
            }

            Debug.Log("Event bus cleared");
        }

        internal void RemoveSubscription(IEventSubscription subscription)
        {
            lock (_lockObject)
            {
                if (_subscriptions.TryGetValue(subscription.EventType, out var subscriptions))
                {
                    subscriptions.Remove(subscription);
                    if (subscriptions.Count == 0)
                    {
                        _subscriptions.Remove(subscription.EventType);
                    }
                }
            }
        }

        private void ProcessQueuedEvents()
        {
            while (true)
            {
                IDomainEvent eventToProcess = null;

                lock (_lockObject)
                {
                    if (_eventQueue.Count == 0)
                        break;

                    eventToProcess = _eventQueue.Dequeue();
                }

                ProcessEventImmediate(eventToProcess);
            }
        }

        private void ProcessEventImmediate(IDomainEvent domainEvent)
        {
            var eventType = domainEvent.GetType();
            List<IEventSubscription> subscriptions = null;

            lock (_lockObject)
            {
                if (_subscriptions.TryGetValue(eventType, out var subs))
                {
                    subscriptions = subs.ToList(); // Create copy to avoid lock during execution
                }
            }

            if (subscriptions == null || subscriptions.Count == 0)
                return;

            foreach (var subscription in subscriptions.Where(s => s.IsActive))
            {
                try
                {
                    if (subscription is EventSubscription<IDomainEvent> syncSub)
                    {
                        syncSub.Handler(domainEvent);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error handling event {eventType.Name}: {ex.Message}");
                }
            }
        }

        private async Task ProcessEventAsync(IDomainEvent domainEvent)
        {
            var eventType = domainEvent.GetType();
            List<IEventSubscription> subscriptions = null;

            lock (_lockObject)
            {
                if (_subscriptions.TryGetValue(eventType, out var subs))
                {
                    subscriptions = subs.ToList();
                }
            }

            if (subscriptions == null || subscriptions.Count == 0)
                return;

            var tasks = subscriptions.Where(s => s.IsActive).Select(async subscription =>
            {
                try
                {
                    if (subscription is AsyncEventSubscription<IDomainEvent> asyncSub)
                    {
                        await asyncSub.Handler(domainEvent);
                    }
                    else if (subscription is EventSubscription<IDomainEvent> syncSub)
                    {
                        syncSub.Handler(domainEvent);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error handling async event {eventType.Name}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        private void OnDestroy()
        {
            Clear();
        }
    }

    /// <summary>
    /// Synchronous event subscription implementation.
    /// </summary>
    internal class EventSubscription<T> : IEventSubscription where T : IDomainEvent
    {
        public Action<T> Handler { get; }
        public bool IsActive { get; private set; } = true;
        public Type EventType => typeof(T);

        private readonly UnityEventBus _eventBus;

        public EventSubscription(Action<T> handler, UnityEventBus eventBus)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void Dispose()
        {
            if (IsActive)
            {
                IsActive = false;
                _eventBus.RemoveSubscription(this);
            }
        }
    }

    /// <summary>
    /// Asynchronous event subscription implementation.
    /// </summary>
    internal class AsyncEventSubscription<T> : IEventSubscription where T : IDomainEvent
    {
        public Func<T, Task> Handler { get; }
        public bool IsActive { get; private set; } = true;
        public Type EventType => typeof(T);

        private readonly UnityEventBus _eventBus;

        public AsyncEventSubscription(Func<T, Task> handler, UnityEventBus eventBus)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void Dispose()
        {
            if (IsActive)
            {
                IsActive = false;
                _eventBus.RemoveSubscription(this);
            }
        }
    }
}