using System;

namespace SovereignsDilemma.Core.Events
{
    /// <summary>
    /// Base interface for all domain events in the system.
    /// Used for event-driven communication between bounded contexts.
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// Unique identifier for this event instance.
        /// </summary>
        Guid EventId { get; }

        /// <summary>
        /// Timestamp when the event occurred.
        /// </summary>
        DateTime OccurredAt { get; }

        /// <summary>
        /// The bounded context that originated this event.
        /// </summary>
        string SourceContext { get; }

        /// <summary>
        /// Event version for schema evolution support.
        /// </summary>
        int Version { get; }
    }

    /// <summary>
    /// Base implementation of domain event providing common functionality.
    /// </summary>
    public abstract class DomainEventBase : IDomainEvent
    {
        public Guid EventId { get; private set; } = Guid.NewGuid();
        public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
        public abstract string SourceContext { get; }
        public virtual int Version => 1;

        protected DomainEventBase() { }

        protected DomainEventBase(Guid eventId, DateTime occurredAt)
        {
            EventId = eventId;
            OccurredAt = occurredAt;
        }
    }
}