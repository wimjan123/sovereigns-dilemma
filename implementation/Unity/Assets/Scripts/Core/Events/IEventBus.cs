using System;
using System.Threading.Tasks;

namespace SovereignsDilemma.Core.Events
{
    /// <summary>
    /// Event bus interface for cross-context communication.
    /// Implements publish-subscribe pattern for bounded context isolation.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publishes a domain event synchronously.
        /// </summary>
        /// <typeparam name="T">The type of domain event</typeparam>
        /// <param name="domainEvent">The event to publish</param>
        void Publish<T>(T domainEvent) where T : IDomainEvent;

        /// <summary>
        /// Publishes a domain event asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of domain event</typeparam>
        /// <param name="domainEvent">The event to publish</param>
        Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;

        /// <summary>
        /// Subscribes to a specific type of domain event.
        /// </summary>
        /// <typeparam name="T">The type of domain event to subscribe to</typeparam>
        /// <param name="handler">The handler function to execute when event is received</param>
        /// <returns>Subscription token for unsubscribing</returns>
        IDisposable Subscribe<T>(Action<T> handler) where T : IDomainEvent;

        /// <summary>
        /// Subscribes to a specific type of domain event with async handler.
        /// </summary>
        /// <typeparam name="T">The type of domain event to subscribe to</typeparam>
        /// <param name="handler">The async handler function to execute when event is received</param>
        /// <returns>Subscription token for unsubscribing</returns>
        IDisposable Subscribe<T>(Func<T, Task> handler) where T : IDomainEvent;

        /// <summary>
        /// Clears all subscriptions and pending events.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Subscription token for managing event subscriptions.
    /// </summary>
    public interface IEventSubscription : IDisposable
    {
        /// <summary>
        /// Whether this subscription is still active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// The event type this subscription is listening for.
        /// </summary>
        Type EventType { get; }
    }
}