# The Sovereign's Dilemma - Development Guide

## Overview

This guide provides comprehensive development standards, workflows, and best practices for contributing to The Sovereign's Dilemma Unity project. The project emphasizes high-performance political simulation, maintainable code architecture, and collaborative development practices.

## Table of Contents

1. [Development Environment Setup](#development-environment-setup)
2. [Code Standards and Conventions](#code-standards-and-conventions)
3. [Architecture Patterns](#architecture-patterns)
4. [Performance Guidelines](#performance-guidelines)
5. [Testing Framework](#testing-framework)
6. [Git Workflow](#git-workflow)
7. [Documentation Standards](#documentation-standards)
8. [Debugging and Profiling](#debugging-and-profiling)
9. [Deployment Process](#deployment-process)

---

## Development Environment Setup

### Prerequisites

**Required Software:**
- Unity 6.0 LTS (2023.3.0f1 or later)
- Visual Studio 2022 or JetBrains Rider 2023.2+
- Git 2.35+ with Git LFS enabled
- .NET 8.0 SDK

**Recommended Extensions:**
- Unity Tools for Visual Studio
- Burst Inspector
- Unity Profiler
- ReSharper (for Rider users)

### Initial Setup

1. **Clone Repository with LFS:**
   ```bash
   git clone https://github.com/wimjan123/sovereigns-dilemma.git
   cd sovereigns-dilemma/implementation/Unity
   git lfs pull
   ```

2. **Unity Project Configuration:**
   ```bash
   # Open Unity Hub
   # Add project from disk -> Select Unity folder
   # Unity will automatically configure project settings
   ```

3. **IDE Configuration:**
   - **Visual Studio**: Install Unity Tools extension
   - **Rider**: Enable Unity support in settings
   - Configure EditorConfig for consistent formatting

4. **API Credentials Setup:**
   ```csharp
   // Create local AI configuration (not committed)
   // Assets/Settings/AIConfiguration_Local.asset
   // Configure NVIDIA NIM credentials for testing
   ```

### Development Tools

**Essential Unity Packages:**
```json
{
  "com.unity.entities": "1.0.16",
  "com.unity.burst": "1.8.8",
  "com.unity.jobs": "0.70.0",
  "com.unity.test-framework": "1.3.9",
  "com.unity.performance.profile-analyzer": "1.2.2"
}
```

**Code Quality Tools:**
- Unity Code Analysis
- SonarQube integration
- Custom performance analyzers

---

## Code Standards and Conventions

### C# Coding Standards

Follow Microsoft C# Coding Conventions with Unity-specific adaptations:

**Naming Conventions:**
```csharp
// Classes and structs: PascalCase
public class VoterBehaviorSystem { }
public struct PoliticalOpinion { }

// Methods and properties: PascalCase
public void CalculateVotingIntention() { }
public float OpinionStrength { get; set; }

// Fields: camelCase with underscore prefix for private
private readonly EntityQuery _voterQuery;
public int voterCount;

// Constants: PascalCase
private const int MAX_VOTERS = 10000;

// Enums: PascalCase for both enum and values
public enum LODLevel
{
    Dormant,
    Low,
    Medium,
    High
}
```

**File Organization:**
```csharp
// File header (required for all new files)
/*
 * The Sovereign's Dilemma - Political Simulation
 * Copyright (c) 2024 Development Team
 *
 * Component: [Brief description]
 * Purpose: [System purpose and responsibilities]
 */

using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
// ... other imports

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Detailed XML documentation for public classes
    /// Explain purpose, usage, and important considerations
    /// </summary>
    public class VoterBehaviorSystem : SystemBase
    {
        // Class implementation
    }
}
```

### Unity-Specific Standards

**MonoBehaviour Guidelines:**
```csharp
public class DashboardManager : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private VoterAnalyticsPanel analyticsPanel;

    [Header("Configuration")]
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private bool enablePerformanceMode = true;

    // Cache components in Awake()
    private Camera _mainCamera;
    private EventBusSystem _eventBus;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _eventBus = FindObjectOfType<EventBusSystem>();
    }

    private void Start()
    {
        InitializeComponents();
    }

    private void Update()
    {
        // Keep Update() minimal and performance-conscious
        if (Time.time - _lastUpdate > updateInterval)
        {
            UpdateAnalytics();
            _lastUpdate = Time.time;
        }
    }
}
```

**ECS Component Guidelines:**
```csharp
// Use IComponentData for pure data
public struct VoterData : IComponentData
{
    // Use appropriate data types for memory efficiency
    public int VoterId;           // 4 bytes
    public byte Age;              // 1 byte (sufficient for age 0-255)
    public byte EducationLevel;   // 1 byte (1-5 scale)

    // Pack boolean flags for efficiency
    public VoterFlags Flags;      // 1 byte for 8 boolean flags
}

// Use ISystemData for system state
public partial struct VoterBehaviorSystem : ISystem
{
    // System implementation with Burst compilation
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // High-performance system logic
    }
}
```

### Performance-Critical Code Standards

**Burst-Compatible Code:**
```csharp
[BurstCompile]
public struct VoterUpdateJob : IJobEntityBatch
{
    public float DeltaTime;
    public ComponentTypeHandle<PoliticalOpinion> OpinionHandle;

    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
    {
        // Use Unity.Mathematics for Burst compatibility
        var opinions = chunk.GetNativeArray(OpinionHandle);

        for (int i = 0; i < opinions.Length; i++)
        {
            var opinion = opinions[i];
            // Burst-compatible operations only
            opinion.EconomicPosition = math.clamp(
                opinion.EconomicPosition + math.sin(DeltaTime), -100, 100);
            opinions[i] = opinion;
        }
    }
}
```

---

## Architecture Patterns

### Entity Component System (ECS) Patterns

**Component Design:**
```csharp
// Good: Small, focused components
public struct VoterData : IComponentData
{
    public int VoterId;
    public byte Age;
    public byte EducationLevel;
}

public struct PoliticalOpinion : IComponentData
{
    public sbyte EconomicPosition;
    public sbyte SocialPosition;
}

// Avoid: Large, monolithic components
public struct VoterEverything : IComponentData // DON'T DO THIS
{
    public int VoterId;
    public byte Age;
    public sbyte EconomicPosition;
    public float[] PartySupport; // Arrays in components are problematic
    public string Name; // Managed types not Burst-compatible
}
```

**System Design Patterns:**
```csharp
// Pattern 1: Data Processing System
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct VoterOpinionUpdateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Process data transformations
        var job = new VoterOpinionUpdateJob { /* ... */ };
        state.Dependency = job.ScheduleParallel(query, state.Dependency);
    }
}

// Pattern 2: Event Handling System
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class VoterEventResponseSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Handle events and trigger responses
        Entities.ForEach((Entity entity, ref VoterData voter, ref PoliticalOpinion opinion) =>
        {
            // Event processing logic
        }).Schedule();
    }
}

// Pattern 3: Management System (not Burst-compiled)
public partial class VoterManagementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // System coordination, memory management, etc.
        ManageVoterLifecycle();
        OptimizeMemoryUsage();
    }
}
```

### Event-Driven Architecture

**Event Bus Pattern:**
```csharp
// Event definition
public struct VoterOpinionChangedEvent : IEvent
{
    public Entity VoterEntity;
    public PoliticalOpinion OldOpinion;
    public PoliticalOpinion NewOpinion;
    public float ChangeIntensity;
}

// Event publisher
public class VoterBehaviorSystem : SystemBase
{
    private EventBusSystem _eventBus;

    protected override void OnUpdate()
    {
        _eventBus.Publish(new VoterOpinionChangedEvent
        {
            VoterEntity = entity,
            OldOpinion = oldOpinion,
            NewOpinion = newOpinion,
            ChangeIntensity = intensity
        });
    }
}

// Event subscriber
public class UIUpdateSystem : SystemBase, IEventHandler<VoterOpinionChangedEvent>
{
    public void Handle(VoterOpinionChangedEvent eventData)
    {
        // Update UI based on voter opinion change
        UpdatePoliticalSpectrumDisplay(eventData);
    }
}
```

### Dependency Injection Pattern

**Service Locator for Unity Integration:**
```csharp
public class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    public static T Get<T>() where T : class
    {
        return (T)_services[typeof(T)];
    }
}

// Usage in systems
public class AIAnalysisSystem : SystemBase
{
    private IAIService _aiService;

    protected override void OnCreate()
    {
        _aiService = ServiceLocator.Get<IAIService>();
    }
}
```

---

## Performance Guidelines

### ECS Performance Optimization

**Memory Layout Optimization:**
```csharp
// Good: Struct of Arrays approach
public struct VoterData : IComponentData
{
    public int VoterId;     // All VoterIds together in memory
    public byte Age;        // All Ages together in memory
}

// Memory access patterns
[BurstCompile]
public void ProcessVoters(NativeArray<VoterData> voters)
{
    // Sequential memory access is fastest
    for (int i = 0; i < voters.Length; i++)
    {
        var voter = voters[i];
        // Process voter data
    }
}
```

**Job System Best Practices:**
```csharp
// Use appropriate job types for different scenarios
public struct VoterUpdateJob : IJobEntityBatch // For entity processing
{
    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
    {
        // Batch processing for better cache utilization
    }
}

public struct AIBatchJob : IJob // For single-threaded operations
{
    public void Execute()
    {
        // AI API calls that can't be parallelized
    }
}

public struct VoterInfluenceJob : IJobParallelFor // For parallel operations
{
    public void Execute(int index)
    {
        // Independent parallel processing
    }
}
```

### Memory Management

**Object Pooling:**
```csharp
public class VoterMemoryPool
{
    private readonly NativeArray<VoterMemoryBlock> _memoryPool;
    private readonly NativeQueue<int> _availableBlocks;

    public VoterMemoryBlock AllocateVoter()
    {
        if (_availableBlocks.TryDequeue(out int blockIndex))
        {
            return _memoryPool[blockIndex];
        }

        Debug.LogWarning("Voter memory pool exhausted");
        return default;
    }

    public void DeallocateVoter(int blockIndex)
    {
        _availableBlocks.Enqueue(blockIndex);
    }
}
```

**Garbage Collection Avoidance:**
```csharp
// Good: Avoid allocations in hot paths
public void UpdateVoterOpinions()
{
    // Use cached collections
    _tempVoterList.Clear(); // Reuse existing list

    // Avoid LINQ in performance-critical code
    for (int i = 0; i < voters.Length; i++)
    {
        if (voters[i].IsActive)
            _tempVoterList.Add(voters[i]);
    }
}

// Avoid: Allocations in Update() or job execution
public void BadUpdateMethod()
{
    var list = new List<Voter>(); // Allocation every frame!
    var results = voters.Where(v => v.IsActive).ToList(); // LINQ allocation!
}
```

### Profiling Integration

**Custom Profiler Markers:**
```csharp
public class VoterBehaviorSystem : SystemBase
{
    private static readonly ProfilerMarker VoterUpdateMarker =
        new("SovereignsDilemma.VoterUpdate");
    private static readonly ProfilerMarker AIAnalysisMarker =
        new("SovereignsDilemma.AIAnalysis");

    protected override void OnUpdate()
    {
        using (VoterUpdateMarker.Auto())
        {
            // Voter update logic
        }

        using (AIAnalysisMarker.Auto())
        {
            // AI analysis logic
        }
    }
}
```

---

## Testing Framework

### Unit Testing Structure

**Test Organization:**
```
Tests/
├── Runtime/
│   ├── Political/
│   │   ├── VoterBehaviorTests.cs
│   │   ├── PoliticalEventTests.cs
│   │   └── DutchPoliticalContextTests.cs
│   ├── AI/
│   │   ├── AIBatchProcessorTests.cs
│   │   └── AnalysisQualityTests.cs
│   └── UI/
│       ├── DashboardManagerTests.cs
│       └── AccessibilityTests.cs
└── Editor/
    ├── CoreSystemTests.cs
    ├── PerformanceTests.cs
    └── ValidationTests.cs
```

**Test Standards:**
```csharp
[TestFixture]
public class VoterBehaviorTests
{
    private World _testWorld;
    private VoterBehaviorSystem _system;

    [SetUp]
    public void SetUp()
    {
        _testWorld = new World("TestWorld");
        _system = _testWorld.CreateSystemManaged<VoterBehaviorSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        _testWorld?.Dispose();
    }

    [Test]
    public void VoterOpinion_WhenInfluencedByEvent_ChangesAppropriately()
    {
        // Arrange
        var voterEntity = _testWorld.EntityManager.CreateEntity();
        var initialOpinion = new PoliticalOpinion { EconomicPosition = 0 };
        _testWorld.EntityManager.AddComponentData(voterEntity, initialOpinion);

        var politicalEvent = new PoliticalEvent
        {
            Type = PoliticalEventType.EconomicNews,
            Intensity = 0.5f,
            EconomicImpact = 20
        };

        // Act
        _system.ProcessPoliticalEvent(politicalEvent);
        _system.Update();

        // Assert
        var updatedOpinion = _testWorld.EntityManager.GetComponentData<PoliticalOpinion>(voterEntity);
        Assert.Greater(updatedOpinion.EconomicPosition, initialOpinion.EconomicPosition);
        Assert.LessOrEqual(Math.Abs(updatedOpinion.EconomicPosition - 10), 5); // Within expected range
    }

    [Test]
    [Performance]
    public void VoterSystem_With10KVoters_MaintainsTargetFrameRate()
    {
        // Performance test implementation
        Measure.Method(() =>
        {
            _system.Update();
        })
        .WarmupCount(5)
        .MeasurementCount(60)
        .SampleGroup("VoterSystem10K")
        .Run();
    }
}
```

### Integration Testing

**AI Integration Tests:**
```csharp
[TestFixture]
public class AIIntegrationTests
{
    private AIBatchProcessor _processor;
    private MockAIService _mockAI;

    [SetUp]
    public void SetUp()
    {
        _mockAI = new MockAIService();
        _processor = new AIBatchProcessor(_mockAI);
    }

    [Test]
    public async Task AIBatchProcessor_WithSimilarVoters_BatchesEfficiently()
    {
        // Arrange
        var similarVoters = CreateSimilarVoters(20);
        var callbackCount = 0;

        // Act
        foreach (var voter in similarVoters)
        {
            _processor.QueueAIRequest(voter.Entity, voter.Data, voter.Opinion,
                voter.Behavior, AIRequestType.OpinionAnalysis, _ => callbackCount++);
        }

        await WaitForBatchCompletion();

        // Assert
        Assert.AreEqual(20, callbackCount);
        Assert.LessOrEqual(_mockAI.TotalAPICalls, 5); // Should batch into few calls
        Assert.GreaterOrEqual(_processor.GetStats().BatchingEfficiency, 0.8f);
    }
}
```

### Performance Testing

**Automated Benchmarks:**
```csharp
public class PerformanceBenchmarks
{
    [UnityTest]
    public IEnumerator FullScale10K_MaintainsTargetFrameRate()
    {
        // Setup 10,000 voters
        var voterSystem = World.DefaultGameObjectInjectionWorld
            .GetExistingSystemManaged<FullScaleVoterSystem>();

        CreateVoters(10000);

        // Run benchmark
        var frameRates = new List<float>();
        for (int i = 0; i < 300; i++) // 5 seconds at 60fps
        {
            yield return null;
            frameRates.Add(1f / Time.unscaledDeltaTime);
        }

        // Validate performance
        var averageFPS = frameRates.Average();
        var onePercentLow = frameRates.OrderBy(f => f).Take(3).Average();

        Assert.GreaterOrEqual(averageFPS, 60f, "Average FPS below target");
        Assert.GreaterOrEqual(onePercentLow, 45f, "1% low FPS too poor");
    }
}
```

---

## Git Workflow

### Branch Strategy

**Branch Naming Convention:**
```
main                    # Production-ready code
develop                 # Integration branch
feature/voter-ai-v2     # New features
bugfix/memory-leak-fix  # Bug fixes
hotfix/crash-on-startup # Emergency production fixes
release/v1.2.0         # Release preparation
```

**Workflow Process:**

1. **Feature Development:**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/improved-ai-batching

   # Development work
   git add .
   git commit -m "Implement improved AI request batching

   - Reduce API calls by 90% through intelligent clustering
   - Add cache invalidation for stale responses
   - Implement adaptive batch sizing based on performance

   Resolves: #123"

   git push origin feature/improved-ai-batching
   # Create Pull Request
   ```

2. **Pull Request Requirements:**
   - [ ] All tests pass
   - [ ] Performance benchmarks meet requirements
   - [ ] Code coverage >80% for new code
   - [ ] Documentation updated
   - [ ] Peer review approved

### Commit Message Standards

**Format:**
```
<type>(<scope>): <description>

<body>

<footer>
```

**Examples:**
```
feat(voter): implement advanced opinion clustering system

Add sophisticated clustering algorithm for grouping voters with
similar political profiles. This enables more efficient AI batch
processing and reduces API costs by 85%.

- Implement k-means clustering for opinion space
- Add cluster validity metrics
- Integrate with existing AI batch processor

Resolves: #234
Performance: Reduces AI API calls from 1000/min to 150/min
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

### Code Review Process

**Review Checklist:**

1. **Functionality:**
   - [ ] Code solves the intended problem
   - [ ] Edge cases are handled
   - [ ] Error handling is appropriate

2. **Performance:**
   - [ ] No unnecessary allocations in hot paths
   - [ ] Appropriate use of ECS patterns
   - [ ] Burst-compatible where applicable

3. **Code Quality:**
   - [ ] Follows coding standards
   - [ ] Appropriate comments and documentation
   - [ ] No code duplication

4. **Testing:**
   - [ ] Unit tests cover new functionality
   - [ ] Performance tests validate requirements
   - [ ] Integration tests pass

---

## Documentation Standards

### Code Documentation

**XML Documentation (Required for public APIs):**
```csharp
/// <summary>
/// Processes political events and updates voter opinions based on the Dutch political context.
/// Uses sophisticated influence algorithms to model realistic opinion changes.
/// </summary>
/// <param name="politicalEvent">The political event to process</param>
/// <param name="affectedVoters">Collection of voters potentially affected by the event</param>
/// <returns>Number of voters whose opinions were significantly changed</returns>
/// <exception cref="ArgumentNullException">Thrown when politicalEvent is null</exception>
/// <exception cref="VoterSimulationException">Thrown when voter processing fails</exception>
/// <remarks>
/// This method implements the Dutch Political Response Model (DPRM) which accounts for:
/// - Regional voting patterns
/// - Educational demographics
/// - Historical party loyalty patterns
///
/// Performance: O(n) where n is the number of affected voters
/// Memory: Temporary allocation of ~100KB for processing buffers
/// </remarks>
public int ProcessPoliticalEvent(PoliticalEvent politicalEvent,
    IReadOnlyList<Entity> affectedVoters)
{
    // Implementation
}
```

**Inline Comments (For complex logic):**
```csharp
public void CalculateVoterInfluence()
{
    // Apply Dutch-specific demographic weighting factors
    // Based on CBS (Statistics Netherlands) voter behavior studies
    float ageWeight = CalculateAgeInfluenceFactor(voter.Age);
    float educationWeight = GetEducationInfluenceMultiplier(voter.EducationLevel);

    // Urban voters show 23% higher political engagement in Netherlands
    // Source: "Political Participation in the Netherlands" (2023)
    if (voter.IsUrban)
    {
        engagementMultiplier *= 1.23f;
    }

    // Complex influence calculation using validated political science models
    // Algorithm based on "Social Influence in Political Opinion Formation"
    // (Journal of Computational Social Science, 2023)
    float finalInfluence = baseInfluence * ageWeight * educationWeight * engagementMultiplier;
}
```

### Architecture Documentation

**System Documentation Template:**
```markdown
# System Name: VoterBehaviorSystem

## Purpose
Brief description of what this system does and why it exists.

## Responsibilities
- Responsibility 1
- Responsibility 2
- Responsibility 3

## Dependencies
- Input: What this system requires
- Output: What this system produces
- External: External services or APIs

## Performance Characteristics
- Target throughput: X voters/second
- Memory usage: Y MB baseline
- Scalability: Linear/logarithmic/constant

## Configuration
Key configuration parameters and their impacts.

## Monitoring
How to monitor this system's health and performance.

## Troubleshooting
Common issues and their resolution steps.
```

---

## Debugging and Profiling

### Unity Profiler Integration

**Custom Profiling Setup:**
```csharp
public class SimulationProfiler
{
    private static readonly ProfilerMarker VoterUpdateMarker =
        new("Simulation.VoterUpdate");
    private static readonly ProfilerMarker AIProcessingMarker =
        new("Simulation.AIProcessing");
    private static readonly ProfilerMarker UIUpdateMarker =
        new("Simulation.UIUpdate");

    public static void BeginVoterUpdate() => VoterUpdateMarker.Begin();
    public static void EndVoterUpdate() => VoterUpdateMarker.End();

    public static void ProfileAIBatch(int batchSize)
    {
        AIProcessingMarker.Begin();
        Profiler.SetCounterValue("AI.BatchSize", batchSize);
    }

    public static void EndAIBatch(float responseTime)
    {
        Profiler.SetCounterValue("AI.ResponseTime", responseTime);
        AIProcessingMarker.End();
    }
}
```

**Performance Monitoring:**
```csharp
public class PerformanceMonitor : MonoBehaviour
{
    [Header("Performance Targets")]
    public float targetFrameRate = 60f;
    public float memoryWarningThreshold = 6000f; // MB

    private void Update()
    {
        // Monitor frame rate
        float currentFPS = 1f / Time.unscaledDeltaTime;
        if (currentFPS < targetFrameRate * 0.9f)
        {
            Debug.LogWarning($"Frame rate below target: {currentFPS:F1} < {targetFrameRate}");
            TriggerPerformanceOptimization();
        }

        // Monitor memory usage
        long memoryUsage = Profiler.GetTotalAllocatedMemory(false) / (1024 * 1024);
        if (memoryUsage > memoryWarningThreshold)
        {
            Debug.LogWarning($"Memory usage high: {memoryUsage}MB");
            TriggerMemoryOptimization();
        }
    }

    private void TriggerPerformanceOptimization()
    {
        // Reduce LOD levels, disable non-essential systems
        var voterSystem = World.DefaultGameObjectInjectionWorld
            .GetExistingSystemManaged<FullScaleVoterSystem>();
        voterSystem.ReduceProcessingLoad();
    }
}
```

### Debugging Utilities

**Debug Visualization:**
```csharp
public class VoterDebugVisualizer : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showVoterOpinions = false;
    public bool showSocialNetworks = false;
    public bool showAIAnalysisResults = false;

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (showVoterOpinions)
        {
            DrawVoterOpinionSpectrum();
        }

        if (showSocialNetworks)
        {
            DrawSocialNetworkConnections();
        }
    }

    private void DrawVoterOpinionSpectrum()
    {
        // Visualize voter opinions in 3D space
        var voters = FindObjectsOfType<VoterEntity>();
        foreach (var voter in voters)
        {
            Vector3 position = new Vector3(
                voter.Opinion.EconomicPosition / 100f * 10f,
                voter.Opinion.SocialPosition / 100f * 10f,
                voter.Opinion.EnvironmentalStance / 100f * 10f
            );

            Color opinionColor = GetOpinionColor(voter.Opinion);
            Gizmos.color = opinionColor;
            Gizmos.DrawSphere(position, 0.1f);
        }
    }
}
```

**Development Console:**
```csharp
public class DevelopmentConsole : MonoBehaviour
{
    private bool _showConsole = false;
    private string _input = "";

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote)) // Tilde key
        {
            _showConsole = !_showConsole;
        }
    }

    private void OnGUI()
    {
        if (!_showConsole) return;

        GUI.Box(new Rect(10, 10, Screen.width - 20, 150), "Development Console");

        _input = GUI.TextField(new Rect(20, 130, Screen.width - 100, 20), _input);

        if (GUI.Button(new Rect(Screen.width - 70, 130, 60, 20), "Execute"))
        {
            ExecuteCommand(_input);
            _input = "";
        }
    }

    private void ExecuteCommand(string command)
    {
        string[] parts = command.Split(' ');
        switch (parts[0].ToLower())
        {
            case "spawn":
                if (int.TryParse(parts[1], out int count))
                    SpawnVoters(count);
                break;
            case "ai":
                if (parts[1] == "test")
                    TestAIIntegration();
                break;
            case "performance":
                ShowPerformanceStats();
                break;
        }
    }
}
```

---

## Deployment Process

### Build Configuration

**Build Profiles:**
```csharp
public static class BuildConfiguration
{
    public enum BuildType
    {
        Development,  // Full debugging, local AI simulation
        Staging,      // Performance testing, live AI integration
        Production    // Optimized build, analytics integration
    }

    public static void ConfigureBuild(BuildType buildType)
    {
        switch (buildType)
        {
            case BuildType.Development:
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    EditorUserBuildSettings.selectedBuildTargetGroup,
                    "DEVELOPMENT_BUILD;ENABLE_PROFILER;UNITY_ASSERTIONS");
                break;

            case BuildType.Staging:
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    EditorUserBuildSettings.selectedBuildTargetGroup,
                    "STAGING_BUILD;ENABLE_PROFILER");
                break;

            case BuildType.Production:
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    EditorUserBuildSettings.selectedBuildTargetGroup,
                    "PRODUCTION_BUILD");
                break;
        }
    }
}
```

### Continuous Integration

**GitHub Actions Workflow:**
```yaml
name: Unity Build and Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
      with:
        lfs: true

    - uses: actions/cache@v3
      with:
        path: Library
        key: Library-${{ hashFiles('Assets/**', 'Packages/**') }}

    - uses: game-ci/unity-test-runner@v2
      with:
        projectPath: implementation/Unity
        testMode: all
        artifactsPath: test-results

    - uses: game-ci/unity-builder@v2
      with:
        projectPath: implementation/Unity
        targetPlatform: StandaloneWindows64
        buildsPath: builds
```

### Release Process

**Release Checklist:**

1. **Pre-Release:**
   - [ ] All tests pass
   - [ ] Performance benchmarks meet requirements
   - [ ] Security scan completed
   - [ ] Documentation updated
   - [ ] Change log prepared

2. **Build:**
   - [ ] Production build configuration
   - [ ] Code signing completed
   - [ ] Build verification tests pass

3. **Deployment:**
   - [ ] Staging deployment successful
   - [ ] User acceptance testing completed
   - [ ] Production deployment
   - [ ] Post-deployment verification

4. **Post-Release:**
   - [ ] Monitor error rates
   - [ ] Performance metrics within targets
   - [ ] User feedback collection
   - [ ] Issue triage process active

---

## Conclusion

This development guide provides the foundation for maintaining high code quality, performance, and collaboration standards in The Sovereign's Dilemma project. Regular updates to these guidelines ensure they remain relevant as the project evolves.

For questions or suggestions regarding these development standards, please create an issue in the project repository or contact the core development team.

**Key Resources:**
- [Unity DOTS Documentation](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html)
- [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Unity Performance Best Practices](https://docs.unity3d.com/Manual/BestPracticeGuides.html)
- [Project Architecture Documentation](./ARCHITECTURE.md)
- [API Reference](./API_REFERENCE.md)