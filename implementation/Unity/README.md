# The Sovereign's Dilemma - Unity Implementation

## Overview

**The Sovereign's Dilemma** is a comprehensive political simulation game built with Unity 6.0 LTS that simulates the Dutch political landscape with 10,000+ AI-driven voters. Players experience the complexities of political decision-making through realistic voter behavior modeling, social media dynamics, and AI-powered political analysis.

### Key Features

- **10K+ Voter Simulation**: Real-time simulation of 10,000+ individual Dutch voters with unique demographics, political opinions, and behavioral patterns
- **AI-Powered Analysis**: Integration with NVIDIA NIM API for sophisticated political opinion analysis and voter behavior prediction
- **Dutch Political Context**: Accurate representation of 12+ Dutch political parties with realistic ideological positions and voter base modeling
- **Multi-Canvas UGUI**: Responsive dashboard interface with accessibility compliance (WCAG 2.1 AA)
- **Entity Component System**: High-performance ECS architecture using Unity DOTS for voter simulation at scale
- **Performance Optimized**: LOD system, memory pooling, and adaptive performance scaling for smooth 60+ FPS gameplay
- **Real-time Analytics**: Live voter analytics, political spectrum visualization, and social media sentiment tracking

## Technology Stack

### Core Technologies
- **Unity 6.0 LTS** - Game engine and rendering
- **Unity DOTS (ECS)** - High-performance voter simulation system
- **C# 9.0** - Primary programming language
- **Unity UI Toolkit (UGUI)** - User interface framework
- **Unity Job System** - Parallel processing for voter calculations

### AI Integration
- **NVIDIA NIM API** - Political opinion analysis and natural language processing
- **Custom AI Behavior System** - Voter decision-making algorithms
- **Machine Learning Models** - Sentiment analysis and opinion clustering

### Performance & Optimization
- **Level-of-Detail (LOD) System** - Dynamic voter detail scaling
- **Memory Pooling** - Efficient voter entity management
- **Adaptive Performance** - Dynamic quality adjustment
- **Custom Profiling Framework** - Real-time performance monitoring

## Project Structure

```
Unity/
├── Assets/
│   ├── Scripts/
│   │   ├── Political/          # Political simulation core
│   │   │   ├── Components/     # ECS components for voter data
│   │   │   ├── Systems/        # ECS systems for simulation logic
│   │   │   ├── Jobs/          # Unity Job System implementations
│   │   │   └── AI/            # AI integration and analysis
│   │   ├── UI/                # User interface systems
│   │   │   ├── Core/          # Dashboard and layout management
│   │   │   ├── Components/    # Reusable UI components
│   │   │   └── Panels/        # Specific UI panels
│   │   ├── Data/              # Data management and persistence
│   │   │   └── Database/      # Database integration
│   │   ├── Testing/           # Performance testing framework
│   │   │   └── PerformanceFramework/  # Benchmarking tools
│   │   └── Core/              # Core game systems
│   ├── Prefabs/               # Unity prefabs and scene objects
│   ├── Materials/             # Materials and visual assets
│   ├── Settings/              # Unity project settings
│   └── StreamingAssets/       # Runtime data and configurations
├── Packages/                  # Unity package dependencies
├── ProjectSettings/           # Unity project configuration
├── Tests/                     # Unit and integration tests
│   ├── Editor/               # Editor-time tests
│   └── Runtime/              # Runtime tests
└── Documentation/            # Technical documentation
```

## Quick Start

### Prerequisites

1. **Unity 6.0 LTS** - [Download from Unity Hub](https://unity3d.com/get-unity/download)
2. **Git with LFS** - Large file support for Unity assets
3. **NVIDIA NIM API Credentials** - AI service access
4. **Visual Studio 2022** or **JetBrains Rider** - IDE with C# support
5. **Minimum System Requirements**:
   - OS: Windows 10/11 64-bit, macOS 10.15+, or Ubuntu 18.04+
   - RAM: 16GB+ (32GB recommended for full simulation)
   - CPU: Intel i7-8700K / AMD Ryzen 7 2700X or equivalent
   - GPU: GTX 1060 / RX 580 or equivalent (4GB VRAM)

### Installation

1. **Clone the Repository**
   ```bash
   git clone https://github.com/wimjan123/sovereigns-dilemma.git
   cd sovereigns-dilemma/implementation/Unity
   ```

2. **Open in Unity**
   - Launch Unity Hub
   - Click "Open" and select the `Unity` folder
   - Unity will automatically import and configure the project

3. **Configure AI Credentials**
   - Go to `Edit → Project Settings → Sovereign's Dilemma → AI Configuration`
   - Enter your NVIDIA NIM API key and endpoint URL
   - Test connection to verify credentials

4. **Initial Build Test**
   ```bash
   # Build and run performance validation
   Unity -batchmode -projectPath . -executeMethod BuildValidator.RunFullValidation -quit
   ```

### First Run

1. **Open Main Scene**: Navigate to `Assets/Scenes/MainDashboard.unity`
2. **Enter Play Mode**: Press the Play button or F5
3. **Initialize Simulation**: Click "Start 10K Simulation" in the dashboard
4. **Monitor Performance**: Use the Performance Monitor panel to track system metrics

## Core Systems Overview

### Voter Simulation System

The heart of the application is a high-performance ECS-based voter simulation that models individual Dutch citizens:

- **VoterData Component**: Demographics, education, income, region
- **PoliticalOpinion Component**: Party preferences, ideological positions
- **BehaviorState Component**: Personality traits, decision-making patterns
- **SocialNetwork Component**: Influence relationships and information flow

### Dutch Political Context

Accurate modeling of the Dutch political landscape:

- **12 Major Parties**: VVD, PVV, CDA, D66, GL, PvdA, SP, CU, FvD, PvdD, Volt, BBB
- **Realistic Voter Bases**: Income, education, and regional demographic modeling
- **Current Issues**: Climate policy, housing crisis, immigration, healthcare
- **Coalition Dynamics**: Government formation and opposition behavior

### AI Integration Framework

Sophisticated AI analysis for voter behavior and opinion dynamics:

- **Opinion Analysis**: Real-time analysis of political content impact
- **Behavior Prediction**: ML models for voter decision-making
- **Sentiment Tracking**: Social media sentiment and viral event modeling
- **Performance Optimization**: Batch processing and caching for API efficiency

### Dashboard Interface

Multi-canvas responsive UI designed for political analysts and researchers:

- **Voter Analytics Panel**: Demographics, opinion distributions, trend analysis
- **Political Spectrum View**: Real-time ideological mapping and party support
- **Social Media Monitor**: Trending topics, sentiment analysis, viral events
- **Performance Dashboard**: System metrics, simulation health, API usage

## Performance Specifications

### Target Performance Metrics

- **Simulation Scale**: 10,000 active voters minimum
- **Frame Rate**: 60+ FPS during normal operation
- **Memory Usage**: <8GB for full simulation
- **AI Response Time**: <500ms for voter analysis
- **UI Responsiveness**: <16ms frame time for 60 FPS

### Scalability Features

- **Dynamic LOD**: Automatic detail reduction based on performance
- **Voter Pooling**: Efficient memory management for large populations
- **Adaptive AI**: Reduced AI calls during performance constraints
- **Background Processing**: Non-critical calculations on background threads

## Development Workflow

### Coding Standards

- **C# Conventions**: Follow Microsoft C# coding standards
- **Unity Patterns**: Use Unity best practices for MonoBehaviour and ScriptableObject
- **ECS Architecture**: Prefer ECS components and systems for performance-critical code
- **Async/Await**: Use modern C# async patterns for AI integration

### Testing Framework

- **Unit Tests**: Individual component and system testing
- **Integration Tests**: End-to-end simulation testing
- **Performance Tests**: Automated benchmarking and profiling
- **AI Tests**: Validation of AI response quality and performance

### Version Control

- **Git LFS**: All Unity assets stored with Large File Support
- **Branch Strategy**: Feature branches with PR reviews
- **Asset Management**: Unity .meta files committed for proper asset tracking

## API Documentation

### Key Interfaces

```csharp
// Voter simulation core
IVoterBehaviorSystem        // Voter decision-making logic
IPoliticalEventSystem      // Political event generation and handling
IAIAnalysisService         // AI service integration

// UI framework
IDashboardManager          // Central UI coordination
IResponsiveLayoutManager   // Multi-canvas layout management
IAccessibilityManager     // WCAG compliance features

// Data management
IVoterDatabase            // Voter data persistence
IPerformanceProfiler      // Performance monitoring
IEventBusSystem           // Event communication
```

### Component Architecture

```csharp
// ECS Components for voter modeling
VoterData                 // Immutable demographic data
PoliticalOpinion         // Dynamic political preferences
BehaviorState            // Personality and decision patterns
SocialNetwork            // Influence and information flow
EventResponse            // Reactions to political events
AIAnalysisCache          // Cached AI analysis results
```

## Configuration

### AI Service Configuration

Configure NVIDIA NIM integration in Unity Project Settings:

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
}
```

### Performance Configuration

Adjust simulation parameters for your hardware:

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
}
```

## Deployment

### Build Configuration

The project supports multiple deployment targets:

- **Development**: Full debugging and profiling enabled
- **Staging**: Performance testing with reduced logging
- **Production**: Optimized build with analytics integration

### Platform Support

- **Windows**: Primary target platform (x64)
- **macOS**: Intel and Apple Silicon support
- **Linux**: Ubuntu 18.04+ compatible
- **Web**: WebGL build for browser deployment (limited to 1K voters)

## Contributing

### Development Setup

1. Fork the repository and create a feature branch
2. Set up Unity 6.0 LTS with identical project settings
3. Configure AI credentials in local settings (not committed)
4. Run full test suite before submitting PRs

### Code Review Process

- All changes require PR review from core team
- Automated testing must pass (unit, integration, performance)
- Code coverage must maintain >80% for new features
- Performance benchmarks must meet or exceed baseline

### Issue Reporting

Use GitHub Issues with appropriate labels:
- `bug` - Functionality problems
- `performance` - Performance degradation
- `enhancement` - New feature requests
- `documentation` - Documentation improvements

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

## Support

### Documentation
- [Architecture Guide](./ARCHITECTURE.md) - Detailed technical architecture
- [API Reference](./API_REFERENCE.md) - Complete API documentation
- [Development Guide](./DEVELOPMENT.md) - Development workflows and standards

### Community
- **Discord**: [Join our development community](https://discord.gg/sovereigns-dilemma)
- **GitHub Issues**: Bug reports and feature requests
- **Email**: support@sovereigns-dilemma.dev

### Academic Use

This simulation is designed for educational and research purposes. For academic licensing and collaboration opportunities, please contact our research team.

---

**The Sovereign's Dilemma** - Understanding democracy through simulation