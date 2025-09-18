# The Sovereign's Dilemma - API Reference

## Overview

This document provides comprehensive API documentation for The Sovereign's Dilemma Unity implementation. The API is organized into several main categories: Political Simulation, AI Integration, User Interface, and Core Infrastructure.

## Table of Contents

1. [Political Simulation Components](#political-simulation-components)
2. [ECS Systems](#ecs-systems)
3. [AI Integration](#ai-integration)
4. [User Interface](#user-interface)
5. [Core Infrastructure](#core-infrastructure)
6. [Data Structures](#data-structures)
7. [Configuration](#configuration)
8. [Events](#events)

---

## Political Simulation Components

### VoterData Component

Core demographic and identity data for individual voters.

```csharp
public struct VoterData : IComponentData
{
    public int VoterId;                 // Unique identifier
    public int Age;                     // Voter age in years
    public byte EducationLevel;         // Education level (1-5 scale)
    public byte IncomePercentile;       // Income level (0-100)
    public byte Region;                 // Dutch province index
    public bool IsUrban;                // Urban vs rural residence
    public byte Gender;                 // Gender identity (0=M, 1=F, 2=Other)
    public byte Religion;               // Religious affiliation index
    public VoterFlags Flags;            // Packed demographic characteristics
}
```

**VoterFlags Enumeration:**
```csharp
[Flags]
public enum VoterFlags : byte
{
    None = 0,
    IsUrban = 1 << 0,           // Lives in urban area
    HasChildren = 1 << 1,       // Has dependent children
    IsEmployed = 1 << 2,        // Currently employed
    IsHomeowner = 1 << 3,       // Owns residence
    IsImmigrant = 1 << 4,       // First/second generation immigrant
    IsDisabled = 1 << 5,        // Has disability
    IsStudent = 1 << 6,         // Currently studying
    IsRetired = 1 << 7          // Retired from work
}
```

### PoliticalOpinion Component

Dynamic political preferences and party support levels.

```csharp
public struct PoliticalOpinion : IComponentData
{
    // Political spectrum positions (-100 to +100)
    public sbyte EconomicPosition;      // Left to Right economic views
    public sbyte SocialPosition;        // Conservative to Progressive
    public sbyte ImmigrationStance;     // Restrictive to Open
    public sbyte EnvironmentalStance;   // Skeptical to Activist

    // Dutch party support levels (0-255)
    public byte VVDSupport;             // Liberal conservative
    public byte PVVSupport;             // Populist right
    public byte CDASupport;             // Christian democratic
    public byte D66Support;             // Social liberal
    public byte SPSupport;              // Socialist
    public byte PvdASupport;            // Social democratic
    public byte GLSupport;              // Green left
    public byte CUSupport;              // Christian union

    public byte Confidence;             // Opinion confidence (0-255)
    public uint LastUpdated;            // Frame number of last update
}
```

### BehaviorState Component

Personality traits and decision-making characteristics.

```csharp
public struct BehaviorState : IComponentData
{
    // Big Five personality traits (0-255)
    public byte Openness;               // Openness to new ideas
    public byte Conscientiousness;      // Organized thinking
    public byte Extraversion;           // Social engagement
    public byte Agreeableness;          // Cooperative nature
    public byte Neuroticism;            // Emotional stability

    // Information processing behavior
    public byte MediaConsumption;       // News consumption level
    public byte SocialInfluence;        // Susceptibility to peer pressure
    public byte AuthorityTrust;         // Trust in institutions
    public byte ChangeResistance;       // Resistance to opinion changes

    // Current emotional state
    public byte Satisfaction;           // Government satisfaction
    public byte Anxiety;                // Future anxiety level
    public byte Anger;                  // Current anger level
    public byte Hope;                   // Hope for positive change

    public BehaviorFlags Flags;         // Behavioral characteristics
}
```

### SocialNetwork Component

Social connections and influence relationships.

```csharp
public struct SocialNetwork : IComponentData
{
    public byte NetworkSize;            // Number of connections
    public byte InfluenceScore;         // Influence on others
    public byte SusceptibilityScore;    // Susceptibility to influence

    // Connection types
    public byte FamilyConnections;      // Family network strength
    public byte WorkConnections;        // Professional network
    public byte SocialConnections;      // Friends and social groups
    public byte OnlineConnections;      // Social media engagement

    // Echo chamber metrics
    public byte EchoChamberStrength;    // Ideological isolation
    public byte DiversityExposure;     // Exposure to different views

    public uint LastInteraction;        // Last social interaction frame
}
```

---

## ECS Systems

### FullScaleVoterSystem

Primary system for managing 10,000+ voters with Level-of-Detail optimization.

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct FullScaleVoterSystem : ISystem
{
    // Configuration
    private const float HIGH_DETAIL_DISTANCE = 50f;
    private const float MEDIUM_DETAIL_DISTANCE = 150f;
    private const float LOW_DETAIL_DISTANCE = 500f;

    private const int MAX_HIGH_DETAIL_VOTERS = 500;
    private const int MAX_MEDIUM_DETAIL_VOTERS = 2000;

    // Performance tracking
    public VoterSystemMetrics GetMetrics();
    public void ForceHighDetailMode(ref SystemState state);
    public void OptimizeMemory();
}
```

**VoterSystemMetrics Structure:**
```csharp
public struct VoterSystemMetrics
{
    public int TotalVoters;
    public int HighDetailVotersProcessed;
    public int MediumDetailVotersProcessed;
    public int LowDetailVotersProcessed;
    public int DormantVotersProcessed;
    public int MemoryUsage;
    public float FrameRate;
    public int MemoryCompactions;
    public int ProcessingLoadReductions;
    public int ProcessingLoadIncreases;
}
```

### VoterLODLevel Component

Level-of-detail tracking for performance optimization.

```csharp
public struct VoterLODLevel : IComponentData
{
    public LODLevel CurrentLevel;       // Current detail level
    public float DistanceToCamera;      // Distance from focus point
    public float LastLODUpdate;         // Last LOD update time
    public int FramesSinceUpdate;       // Frames since last update
}

public enum LODLevel : byte
{
    Dormant = 0,    // No processing
    Low = 1,        // Minimal processing
    Medium = 2,     // Standard processing
    High = 3        // Full processing with social networks
}
```

### AIBehaviorInfluenceSystem

System for applying AI analysis results to voter behavior.

```csharp
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial struct AIBehaviorInfluenceSystem : ISystem
{
    // Processes AI analysis results and updates voter opinions
    // Handles influence propagation through social networks
    // Manages opinion clustering and representative analysis
}
```

### PoliticalEventSystem

Generates and manages political events affecting voter opinions.

```csharp
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PoliticalEventSystem : ISystem
{
    public PoliticalEventMetrics GetMetrics();
    public void TriggerEvent(PoliticalEventType eventType, float intensity);
    public List<ActivePoliticalEvent> GetActiveEvents();
}
```

---

## AI Integration

### AIBatchProcessor

Advanced AI request batching system for efficient API usage.

```csharp
public class AIBatchProcessor : IDisposable
{
    // Configuration constants
    private const int MAX_BATCH_SIZE = 50;
    private const int MIN_BATCH_SIZE = 5;
    private const float BATCH_TIMEOUT_SECONDS = 2.0f;
    private const int CACHE_SIZE = 500;
    private const float CACHE_TTL_HOURS = 24.0f;

    // Public interface
    public void QueueAIRequest(Entity voter, VoterData voterData,
        PoliticalOpinion opinion, BehaviorState behavior,
        AIRequestType requestType, Action<AIAnalysisResult> callback);

    public void Update();
    public AIBatchingStats GetStats();

    // Performance metrics
    public float CacheHitRatio { get; }
    public float BatchingEfficiency { get; }
    public int ActiveCacheEntries { get; }
}
```

**AIBatchingStats Structure:**
```csharp
public struct AIBatchingStats
{
    public int TotalRequests;
    public int CacheHits;
    public int BatchedRequests;
    public float CacheHitRatio;
    public float BatchingEfficiency;
    public int ActiveCacheEntries;
    public int ActiveBatches;
    public float AvgBatchSize;
}
```

### AIAnalysisResult

Result structure for AI analysis responses.

```csharp
public class AIAnalysisResult
{
    public List<PartyRecommendation> PartyRecommendations;
    public PredictedBehavior PredictedBehavior;
    public List<string> InfluenceFactors;
    public float Confidence;              // Analysis confidence (0-1)
    public float ReasoningDepth;          // Depth of analysis (0-1)
    public float ProcessingTime;          // API response time
    public int BatchSize;                 // Size of batch processed
}

public class PartyRecommendation
{
    public string PartyId;                // Dutch party identifier
    public float Confidence;              // Recommendation confidence
    public string Reasoning;              // AI reasoning for recommendation
}
```

### DutchPoliticalContext

Comprehensive Dutch political landscape modeling.

```csharp
public class DutchPoliticalContext
{
    // Party analysis
    public DutchPoliticalParty CalculateVotingIntention(PoliticalOpinion opinion);
    public PartyProfile GetPartyProfile(DutchPoliticalParty party);
    public List<DutchPoliticalParty> GetCoalitionPartners(DutchPoliticalParty party);

    // Issue management
    public List<PoliticalIssue> GetCurrentIssues();
    public PoliticalIssue GetMostImportantIssue();
    public void UpdateIssueImportance(string issueName, float newImportance);

    // Support calculation
    public float CalculatePartySupport(DutchPoliticalParty party,
        List<PoliticalOpinion> voterOpinions);
}
```

**PartyProfile Structure:**
```csharp
public class PartyProfile
{
    public string PartyName;
    public float EconomicPosition;        // -1 (left) to +1 (right)
    public float SocialPosition;          // -1 (conservative) to +1 (progressive)
    public float EnvironmentalPosition;   // -1 (skeptical) to +1 (activist)
    public IncomeLevel[] CoreVoterBase;
    public EducationLevel[] EducationAppeal;
    public string[] KeyIssues;
    public float MarketShare;             // Typical vote percentage
    public CoalitionType Coalition;
}
```

---

## User Interface

### DashboardManager

Central UI coordination system with multi-canvas architecture.

```csharp
public class DashboardManager : MonoBehaviour, IEventHandler
{
    // Canvas management
    [SerializeField] private Canvas backgroundCanvas;    // Sort order: 0
    [SerializeField] private Canvas mainUICanvas;        // Sort order: 10
    [SerializeField] private Canvas overlayCanvas;       // Sort order: 20
    [SerializeField] private Canvas performanceCanvas;   // Sort order: 30

    // Panel references
    [SerializeField] private VoterAnalyticsPanel voterAnalyticsPanel;
    [SerializeField] private PoliticalSpectrumPanel politicalSpectrumPanel;
    [SerializeField] private SocialMediaPanel socialMediaPanel;
    [SerializeField] private PerformanceMonitorPanel performanceMonitorPanel;

    // Public interface
    public void SetPanelVisibility(string panelName, bool visible);
    public void TogglePerformanceMonitor();
    public DashboardState GetCurrentState();
    public void ForceLayoutRefresh();

    // Event handlers
    public void Handle(VoterOpinionChangedEvent eventData);
    public void Handle(PoliticalEventOccurredEvent eventData);
    public void Handle(PerformanceMetricsUpdatedEvent eventData);
}
```

**UILayoutMode Enumeration:**
```csharp
public enum UILayoutMode
{
    Mobile,      // <1024px width, vertical stacking
    Portrait,    // Aspect ratio <1.5, height-optimized
    Standard,    // 16:9 typical desktop layout
    Ultrawide    // Aspect ratio >2.1, horizontal expansion
}
```

### ResponsiveLayoutManager

Handles responsive design and layout adaptation.

```csharp
public class ResponsiveLayoutManager : MonoBehaviour
{
    public void SetLayoutMode(UILayoutMode mode);
    public UILayoutMode GetCurrentLayoutMode();
    public void RegisterResponsivePanel(IResponsivePanel panel);
    public void UnregisterResponsivePanel(IResponsivePanel panel);

    // Layout calculations
    public Vector2 CalculateOptimalPanelSize(UILayoutMode mode, PanelType type);
    public Vector3 CalculateOptimalPanelPosition(UILayoutMode mode, PanelType type);
}
```

### AccessibilityManager

Comprehensive WCAG AA compliance system.

```csharp
public class AccessibilityManager : MonoBehaviour
{
    // Accessibility settings
    public bool IsAccessibilityModeEnabled { get; }
    public bool IsKeyboardNavigationEnabled { get; }
    public bool IsScreenReaderEnabled { get; }
    public bool IsHighContrastEnabled { get; }

    // Configuration
    public void SetAccessibilityMode(bool enabled);
    public void SetKeyboardNavigation(bool enabled);
    public void SetScreenReader(bool enabled);
    public void SetHighContrast(bool enabled);
    public void SetReducedMotion(bool enabled);
    public void SetFontSizeMultiplier(float multiplier);

    // Element registration
    public void RegisterAccessibleElement(GameObject element, string label,
        string description = "");
    public void SpeakText(string text);
    public void FocusElement(Selectable element);

    // Events
    public event Action<bool> OnAccessibilityModeChanged;
    public event Action<Selectable> OnNavigationChanged;
    public event Action<string> OnScreenReaderSpeak;
}
```

**AccessibilityInfo Structure:**
```csharp
public class AccessibilityInfo
{
    public string Label;           // Screen reader label
    public string Description;     // Detailed description
    public bool IsInteractable;    // Can be activated
    public string Role;            // UI role (button, slider, etc.)
}
```

### UI Data Structures

**VoterAnalyticsData:**
```csharp
public struct VoterAnalyticsData
{
    public bool HasData;
    public int TotalVoters;
    public int ActiveVoters;
    public float[] AgeDistribution;        // 5 age groups
    public float[] EducationDistribution;  // 4 education levels
    public float[] IncomeDistribution;     // 3 income brackets
}
```

**PoliticalSpectrumData:**
```csharp
public struct PoliticalSpectrumData
{
    public bool HasData;
    public float[] EconomicSpectrum;       // 10 buckets
    public float[] SocialSpectrum;         // 10 buckets
    public float[] EnvironmentalSpectrum;  // 10 buckets
    public Dictionary<string, float> PartySupport;
}
```

**SocialMediaData:**
```csharp
public struct SocialMediaData
{
    public bool HasData;
    public int ActiveEvents;
    public float PoliticalTension;         // Current tension level
    public List<SocialMediaPost> RecentPosts;
}

public class SocialMediaPost
{
    public string Content;
    public float Engagement;               // Engagement level (0-1)
    public DateTime Timestamp;
    public List<string> Tags;
}
```

---

## Core Infrastructure

### EventBusSystem

High-performance event communication system.

```csharp
public class EventBusSystem : SystemBase, IEventBus
{
    // Event subscription
    public void Subscribe<T>(IEventHandler<T> handler, string channel = "default")
        where T : IEvent;
    public void Unsubscribe<T>(IEventHandler<T> handler) where T : IEvent;

    // Event publishing
    public void Publish<T>(T eventData, string channel = "default") where T : IEvent;
    public void PublishImmediate<T>(T eventData, string channel = "default") where T : IEvent;

    // Metrics
    public EventBusMetrics GetMetrics();
}
```

**EventBusMetrics Structure:**
```csharp
public struct EventBusMetrics
{
    public int QueueSize;
    public int TotalEventsProcessed;
    public float AverageProcessingTime;
    public int ActiveSubscribers;
    public Dictionary<string, int> EventTypesCounts;
}
```

### PerformanceProfiler

Real-time performance monitoring and optimization.

```csharp
public static class PerformanceProfiler
{
    // Measurement recording
    public static void RecordMeasurement(string metricName, float value);
    public static void RecordMeasurement(string metricName, int value);

    // Performance queries
    public static float GetAverageMetric(string metricName, int sampleCount = 60);
    public static float GetPeakMetric(string metricName, int sampleCount = 60);
    public static PerformanceReport GenerateReport();

    // Adaptive performance
    public static float GetAdaptivePerformanceMultiplier();
    public static void SetPerformanceTarget(string metricName, float target);
}
```

**PerformanceReport Structure:**
```csharp
public struct PerformanceReport
{
    public float AverageFrameRate;
    public float OnePercentLowFrameRate;
    public float MemoryUsageMB;
    public float VoterProcessingTime;
    public float UIUpdateTime;
    public float AIResponseTime;
    public Dictionary<string, float> CustomMetrics;
    public List<string> PerformanceWarnings;
}
```

---

## Data Structures

### Dutch Political Parties

```csharp
public enum DutchPoliticalParty
{
    None = 0,
    VVD = 1,        // Liberal conservative
    PVV = 2,        // Populist right
    CDA = 3,        // Christian democratic
    D66 = 4,        // Social liberal
    GL = 5,         // Green left
    PvdA = 6,       // Social democratic
    SP = 7,         // Socialist
    ChristenUnie = 8, // Christian union
    FvD = 9,        // Right-wing populist
    PvdD = 10,      // Party for the Animals
    Volt = 11,      // Pro-European
    BBB = 12        // Farmer-Citizen Movement
}
```

### Education and Income Levels

```csharp
public enum EducationLevel : byte
{
    Primary = 1,        // Primary education
    Secondary = 2,      // Secondary education
    Vocational = 3,     // Vocational training
    Higher = 4,         // Higher education
    University = 5      // University degree
}

public enum IncomeLevel : byte
{
    Low = 1,           // Bottom 33%
    Middle = 2,        // Middle 34%
    High = 3           // Top 33%
}
```

### Event Types

```csharp
public enum AIRequestType
{
    OpinionAnalysis,
    BehaviorPrediction,
    InfluenceAssessment,
    PartyRecommendation,
    SentimentAnalysis
}

public enum PoliticalEventType
{
    ElectionAnnouncement,
    PolicyProposal,
    ScandalBreak,
    EconomicNews,
    InternationalEvent,
    SocialMovement,
    CoalitionChange,
    DebateEvent
}
```

---

## Configuration

### AI Configuration

```csharp
[CreateAssetMenu(fileName = "AIConfig", menuName = "Sovereign's Dilemma/AI Configuration")]
public class AIConfiguration : ScriptableObject
{
    [Header("NVIDIA NIM Configuration")]
    public string nimApiKey = "";
    public string nimEndpoint = "https://api.nvcf.nvidia.com";

    [Header("Performance Settings")]
    public int maxConcurrentRequests = 5;
    public float cacheExpirationMinutes = 30f;
    public bool enableBatchProcessing = true;
    public float batchTimeoutSeconds = 2.0f;

    [Header("Quality Settings")]
    public float confidenceThreshold = 0.7f;
    public int maxRetries = 3;
    public bool enableResponseValidation = true;
}
```

### Simulation Configuration

```csharp
[CreateAssetMenu(fileName = "SimConfig", menuName = "Sovereign's Dilemma/Simulation Configuration")]
public class SimulationConfiguration : ScriptableObject
{
    [Header("Voter Simulation")]
    public int targetVoterCount = 10000;
    public float simulationSpeed = 1.0f;
    public bool enableLODSystem = true;

    [Header("Performance Limits")]
    public int maxVotersPerFrame = 1000;
    public float targetFrameRate = 60f;
    public bool adaptivePerformance = true;

    [Header("Political Context")]
    public bool enableRealTimeEvents = true;
    public float eventFrequencyMultiplier = 1.0f;
    public bool enableSocialNetworks = true;

    [Header("Debug Settings")]
    public bool enableDetailedLogging = false;
    public bool enablePerformanceLogging = true;
    public int logIntervalFrames = 1800; // 30 seconds at 60fps
}
```

---

## Events

### Core Political Events

```csharp
public interface IEvent { }

// Voter-related events
public struct VoterOpinionChangedEvent : IEvent
{
    public Entity VoterEntity;
    public PoliticalOpinion OldOpinion;
    public PoliticalOpinion NewOpinion;
    public float ChangeIntensity;
    public string Reason;
}

public struct VoterBehaviorChangedEvent : IEvent
{
    public Entity VoterEntity;
    public BehaviorState OldBehavior;
    public BehaviorState NewBehavior;
    public List<string> InfluenceFactors;
}

// Political events
public struct PoliticalEventOccurredEvent : IEvent
{
    public string EventId;
    public PoliticalEventType EventType;
    public string Description;
    public float Intensity;
    public List<string> AffectedDemographics;
    public Dictionary<DutchPoliticalParty, float> PartyImpact;
}

// Performance events
public struct PerformanceMetricsUpdatedEvent : IEvent
{
    public float CurrentFPS;
    public float MemoryUsageMB;
    public int ActiveVoters;
    public float AIResponseTime;
    public Dictionary<string, float> AdditionalMetrics;
}

// AI events
public struct AIAnalysisCompletedEvent : IEvent
{
    public string BatchId;
    public int VotersAnalyzed;
    public float ProcessingTime;
    public float AverageConfidence;
    public List<AIAnalysisResult> Results;
}
```

### UI Events

```csharp
// Dashboard events
public struct DashboardLayoutChangedEvent : IEvent
{
    public UILayoutMode NewLayout;
    public UILayoutMode PreviousLayout;
    public Vector2 ScreenSize;
}

public struct PanelVisibilityChangedEvent : IEvent
{
    public string PanelName;
    public bool IsVisible;
    public PanelChangeReason Reason;
}

// Accessibility events
public struct AccessibilityModeChangedEvent : IEvent
{
    public bool AccessibilityEnabled;
    public bool KeyboardNavigationEnabled;
    public bool ScreenReaderEnabled;
    public bool HighContrastEnabled;
}
```

---

## Error Handling

### Exception Types

```csharp
public class VoterSimulationException : Exception
{
    public int AffectedVoterCount { get; }
    public string SystemName { get; }

    public VoterSimulationException(string message, string systemName,
        int affectedVoterCount = 0) : base(message)
    {
        SystemName = systemName;
        AffectedVoterCount = affectedVoterCount;
    }
}

public class AIIntegrationException : Exception
{
    public string APIEndpoint { get; }
    public int RetryCount { get; }

    public AIIntegrationException(string message, string apiEndpoint,
        int retryCount = 0) : base(message)
    {
        APIEndpoint = apiEndpoint;
        RetryCount = retryCount;
    }
}

public class PerformanceException : Exception
{
    public float TargetFPS { get; }
    public float ActualFPS { get; }

    public PerformanceException(string message, float targetFPS,
        float actualFPS) : base(message)
    {
        TargetFPS = targetFPS;
        ActualFPS = actualFPS;
    }
}
```

---

This API reference provides comprehensive documentation for all major components of The Sovereign's Dilemma political simulation system. For additional details on specific implementations, refer to the source code and inline documentation within each component.