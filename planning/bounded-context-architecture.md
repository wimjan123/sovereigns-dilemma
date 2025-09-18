# Bounded Context Architecture Design
**Project**: The Sovereign's Dilemma - Dutch Political Simulation
**Version**: 1.0
**Date**: 2025-09-18
**Pattern**: Martin Fowler's Bounded Context + DDD Principles

## Executive Summary

Bounded Context architecture design for The Sovereign's Dilemma, applying Domain-Driven Design principles to create maintainable, scalable, and testable Unity C# systems with clear separation of concerns.

## Bounded Context Overview

### Core Problem
The current monolithic Unity architecture risks tight coupling between political simulation, AI integration, UI management, and data persistence, leading to maintenance challenges and testing difficulties.

### DDD Solution
Apply Martin Fowler's Bounded Context pattern to divide the large political simulation domain into smaller, focused contexts with explicit relationships and clear boundaries.

## Bounded Context Definitions

### Context 1: Political Simulation Core
**Domain**: Voter behavior, political calculations, and simulation logic
**Ubiquitous Language**: Voter, PoliticalSpectrum, Opinion, Memory, Influence
**Responsibility**: Pure political simulation without external dependencies

```csharp
namespace SovereignsDilemma.PoliticalSimulation
{
    // Domain Entities
    public class Voter
    {
        public VoterId Id { get; private set; }
        public PoliticalSpectrum CurrentPosition { get; private set; }
        public VoterMemory Memory { get; private set; }
        public InfluenceNetwork Influences { get; private set; }

        public void UpdatePosition(PoliticalEvent politicalEvent)
        {
            // Pure domain logic - no external dependencies
        }
    }

    public class PoliticalSpectrum
    {
        public int Economic { get; private set; }     // -100 to +100
        public int Social { get; private set; }       // -100 to +100
        public int Immigration { get; private set; }   // -100 to +100
        public int Environment { get; private set; }   // -100 to +100
    }

    // Domain Services
    public interface IVoterSimulationService
    {
        void UpdateAllVoters(PoliticalEvent politicalEvent);
        VoterResponse[] GenerateResponses(PoliticalPost post, VoterId[] voterIds);
        PoliticalAnalysis AnalyzePublicOpinion();
    }

    // Domain Events
    public record VoterOpinionChanged(VoterId VoterId, PoliticalSpectrum OldPosition, PoliticalSpectrum NewPosition);
    public record PoliticalCrisisTriggered(CrisisType Type, int Severity);
}
```

### Context 2: Social Media Engine
**Domain**: Post processing, response generation, and social interaction
**Ubiquitous Language**: Post, Response, Engagement, Virality, SocialNetwork
**Responsibility**: Social media mechanics and interaction patterns

```csharp
namespace SovereignsDilemma.SocialMedia
{
    // Domain Entities
    public class PoliticalPost
    {
        public PostId Id { get; private set; }
        public string Content { get; private set; }
        public DateTime PostedAt { get; private set; }
        public PoliticalAnalysis Analysis { get; private set; }

        public void Analyze(IPoliticalAnalyzer analyzer)
        {
            Analysis = analyzer.Analyze(Content);
        }
    }

    public class SocialMediaFeed
    {
        private readonly List<PoliticalPost> _posts;
        private readonly List<VoterResponse> _responses;

        public void AddPost(PoliticalPost post)
        {
            _posts.Add(post);
            // Trigger response generation
        }
    }

    // Domain Services
    public interface ISocialMediaEngine
    {
        Task<PostAnalysis> ProcessPost(string content);
        Task<VoterResponse[]> GenerateResponses(PoliticalPost post);
        SocialMediaMetrics CalculateEngagement(PostId postId);
    }

    // Integration Interface (Anti-corruption Layer)
    public interface IPoliticalAnalyzer
    {
        Task<PoliticalAnalysis> Analyze(string content);
    }
}
```

### Context 3: AI Integration
**Domain**: External AI service integration, caching, and fallback handling
**Ubiquitous Language**: AIProvider, Analysis, Cache, CircuitBreaker, Fallback
**Responsibility**: Managing AI service integration with resilience patterns

```csharp
namespace SovereignsDilemma.AIIntegration
{
    // Domain Services
    public interface IAIAnalysisService
    {
        Task<PoliticalAnalysis> AnalyzeContent(string content);
        Task<bool> IsHealthy();
        AIServiceStatus GetStatus();
    }

    // Implementation with Circuit Breaker
    public class NVIDIANIMService : IAIAnalysisService
    {
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ICacheService _cache;
        private readonly IFallbackService _fallback;
        private readonly HttpClient _httpClient;

        public async Task<PoliticalAnalysis> AnalyzeContent(string content)
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                // Check cache first
                var cached = await _cache.GetAsync<PoliticalAnalysis>(content);
                if (cached != null) return cached;

                // Call NVIDIA NIM API
                var analysis = await CallNVIDIAAPI(content);

                // Cache result
                await _cache.SetAsync(content, analysis, TimeSpan.FromHours(1));

                return analysis;
            });
        }

        private async Task<PoliticalAnalysis> CallNVIDIAAPI(string content)
        {
            var request = new
            {
                model = "nvidia/llama-3.1-nemotron-70b-instruct",
                messages = new[]
                {
                    new { role = "system", content = DutchPoliticalPrompts.SystemPrompt },
                    new { role = "user", content = content }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/chat/completions", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<NIMResponse>();
            return MapToAnalysis(result);
        }
    }

    // Circuit Breaker Implementation
    public class CircuitBreaker : ICircuitBreaker
    {
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private int _failureCount = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime < TimeSpan.FromSeconds(30))
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
                _state = CircuitBreakerState.HalfOpen;
            }

            try
            {
                var result = await operation();
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure();
                throw;
            }
        }

        private void OnSuccess()
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }

        private void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= 5)
            {
                _state = CircuitBreakerState.Open;
            }
        }
    }
}
```

### Context 4: UI Management
**Domain**: User interface coordination, dashboard management, and player interaction
**Ubiquitous Language**: Dashboard, Panel, Interaction, Visualization, UserAction
**Responsibility**: Unity UGUI coordination and user experience

```csharp
namespace SovereignsDilemma.UI
{
    // Domain Services
    public interface IPoliticalDashboard
    {
        void UpdateVoterAnalytics(VoterAnalytics analytics);
        void DisplayPoliticalPost(PoliticalPost post);
        void ShowVoterResponses(VoterResponse[] responses);
        void UpdateCoalitionStatus(CoalitionStatus status);
    }

    // Unity MonoBehaviour Integration
    public class PoliticalDashboardController : MonoBehaviour, IPoliticalDashboard
    {
        [SerializeField] private VoterAnalyticsPanel _analyticsPanel;
        [SerializeField] private SocialMediaPanel _socialMediaPanel;
        [SerializeField] private CoalitionPanel _coalitionPanel;

        // Event System Integration
        private void OnEnable()
        {
            EventBus.Subscribe<VoterOpinionChanged>(OnVoterOpinionChanged);
            EventBus.Subscribe<PoliticalCrisisTriggered>(OnPoliticalCrisis);
        }

        public void UpdateVoterAnalytics(VoterAnalytics analytics)
        {
            _analyticsPanel.UpdateData(analytics);
        }

        private void OnVoterOpinionChanged(VoterOpinionChanged evt)
        {
            // Update UI based on voter opinion changes
        }
    }

    // Anti-corruption Layer for Political Simulation Context
    public class PoliticalSimulationUIAdapter
    {
        private readonly IPoliticalDashboard _dashboard;
        private readonly IVoterSimulationService _voterService;

        public PoliticalSimulationUIAdapter(IPoliticalDashboard dashboard, IVoterSimulationService voterService)
        {
            _dashboard = dashboard;
            _voterService = voterService;
        }

        public void HandlePlayerPost(string content)
        {
            // Convert UI action to domain operation
            var post = new PoliticalPost(content);
            var analysis = _voterService.AnalyzePublicOpinion();

            // Convert domain result back to UI
            _dashboard.UpdateVoterAnalytics(MapToUIAnalytics(analysis));
        }
    }
}
```

### Context 5: Data Persistence
**Domain**: Game state persistence, voter data storage, and data integrity
**Ubiquitous Language**: Repository, Entity, Migration, Backup, Integrity
**Responsibility**: SQLite integration and data management

```csharp
namespace SovereignsDilemma.DataPersistence
{
    // Repository Pattern
    public interface IVoterRepository
    {
        Task<Voter> GetByIdAsync(VoterId id);
        Task<Voter[]> GetByDemographicClusterAsync(DemographicClusterId clusterId);
        Task SaveAsync(Voter voter);
        Task SaveBatchAsync(Voter[] voters);
        Task<int> GetVoterCountAsync();
    }

    // SQLite Implementation
    public class SQLiteVoterRepository : IVoterRepository
    {
        private readonly IDbConnection _connection;

        public async Task<Voter> GetByIdAsync(VoterId id)
        {
            const string sql = @"
                SELECT v.*, vm.*
                FROM Voters v
                LEFT JOIN VoterMemories vm ON v.Id = vm.VoterId
                WHERE v.Id = @Id";

            var result = await _connection.QueryAsync<VoterEntity, VoterMemoryEntity, Voter>(
                sql,
                (voter, memory) => MapToDomain(voter, memory),
                new { Id = id.Value },
                splitOn: "VoterId"
            );

            return result.FirstOrDefault();
        }

        public async Task SaveBatchAsync(Voter[] voters)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var voter in voters)
                {
                    await SaveVoterEntity(voter, transaction);
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    // Event Sourcing for Voter State Changes
    public class VoterEventStore
    {
        private readonly IDbConnection _connection;

        public async Task AppendEventAsync(VoterId voterId, DomainEvent domainEvent)
        {
            var eventData = JsonSerializer.Serialize(domainEvent);
            const string sql = @"
                INSERT INTO VoterEvents (VoterId, EventType, EventData, Timestamp)
                VALUES (@VoterId, @EventType, @EventData, @Timestamp)";

            await _connection.ExecuteAsync(sql, new
            {
                VoterId = voterId.Value,
                EventType = domainEvent.GetType().Name,
                EventData = eventData,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task<DomainEvent[]> GetEventsAsync(VoterId voterId)
        {
            const string sql = @"
                SELECT EventType, EventData
                FROM VoterEvents
                WHERE VoterId = @VoterId
                ORDER BY Timestamp";

            var events = await _connection.QueryAsync(sql, new { VoterId = voterId.Value });

            return events.Select(e => DeserializeEvent(e.EventType, e.EventData)).ToArray();
        }
    }
}
```

## Context Integration

### Event-Driven Communication
```csharp
namespace SovereignsDilemma.Infrastructure
{
    // Event Bus for Cross-Context Communication
    public interface IEventBus
    {
        void Publish<T>(T domainEvent) where T : IDomainEvent;
        void Subscribe<T>(Action<T> handler) where T : IDomainEvent;
        Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;
    }

    public class UnityEventBus : MonoBehaviour, IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Publish<T>(T domainEvent) where T : IDomainEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                foreach (var handler in handlers.Cast<Action<T>>())
                {
                    try
                    {
                        handler(domainEvent);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error handling event {typeof(T).Name}: {ex.Message}");
                    }
                }
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : IDomainEvent
        {
            var eventType = typeof(T);
            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Delegate>();
            }
            _handlers[eventType].Add(handler);
        }
    }
}
```

### Context Integration Mapping
```csharp
// Integration between Political Simulation and Social Media contexts
public class PoliticalSocialMediaIntegration
{
    private readonly IVoterSimulationService _voterService;
    private readonly ISocialMediaEngine _socialMediaEngine;
    private readonly IEventBus _eventBus;

    public PoliticalSocialMediaIntegration(
        IVoterSimulationService voterService,
        ISocialMediaEngine socialMediaEngine,
        IEventBus eventBus)
    {
        _voterService = voterService;
        _socialMediaEngine = socialMediaEngine;
        _eventBus = eventBus;

        // Subscribe to events from other contexts
        _eventBus.Subscribe<PostPublished>(OnPostPublished);
    }

    private async void OnPostPublished(PostPublished evt)
    {
        // Convert Social Media event to Political Simulation action
        var politicalEvent = new PoliticalStatement(evt.Content, evt.PostedAt);

        // Update voter simulation
        _voterService.UpdateAllVoters(politicalEvent);

        // Generate voter responses
        var responses = _voterService.GenerateResponses(evt.Post, GetActiveVoterIds());

        // Send back to Social Media context
        await _socialMediaEngine.AddResponses(evt.PostId, responses);
    }
}
```

## Unity-Specific Implementation

### Assembly Definition Structure
```
SovereignsDilemma.PoliticalSimulation.asmdef
SovereignsDilemma.SocialMedia.asmdef
SovereignsDilemma.AIIntegration.asmdef
SovereignsDilemma.UI.asmdef
SovereignsDilemma.DataPersistence.asmdef
SovereignsDilemma.Infrastructure.asmdef (shared)
```

### Dependency Injection with VContainer
```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Political Simulation Context
        builder.Register<IVoterSimulationService, VoterSimulationService>(Lifetime.Singleton);
        builder.Register<IVoterRepository, SQLiteVoterRepository>(Lifetime.Singleton);

        // AI Integration Context
        builder.Register<IAIAnalysisService, NVIDIANIMService>(Lifetime.Singleton);
        builder.Register<ICircuitBreaker, CircuitBreaker>(Lifetime.Singleton);
        builder.Register<ICacheService, MemoryCacheService>(Lifetime.Singleton);

        // Social Media Context
        builder.Register<ISocialMediaEngine, SocialMediaEngine>(Lifetime.Singleton);

        // Infrastructure
        builder.Register<IEventBus, UnityEventBus>(Lifetime.Singleton);

        // Context Integration
        builder.Register<PoliticalSocialMediaIntegration>(Lifetime.Singleton);
    }
}
```

## Benefits of Bounded Context Architecture

### Maintainability
- **Clear Separation**: Each context has focused responsibility
- **Independent Evolution**: Contexts can evolve independently
- **Reduced Coupling**: Explicit interfaces between contexts

### Testability
- **Unit Testing**: Each context can be tested in isolation
- **Mock Integration**: Clear interfaces enable easy mocking
- **Focused Testing**: Tests focus on specific domain logic

### Scalability
- **Performance Isolation**: Context boundaries enable performance optimization
- **Resource Management**: Each context manages its own resources
- **Parallel Development**: Teams can work on different contexts independently

### Production Readiness
- **Fault Isolation**: Failures in one context don't cascade
- **Monitoring**: Context-specific metrics and observability
- **Deployment**: Independent deployment of context improvements

This bounded context architecture provides a solid foundation for maintainable, scalable, and testable Unity C# development following Martin Fowler's DDD principles.