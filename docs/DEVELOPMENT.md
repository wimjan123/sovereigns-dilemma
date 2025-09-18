# Development Guide
**The Sovereign's Dilemma - Dutch Political Simulation**

## Quick Start

### Prerequisites
- Unity 6.0 LTS
- C# development environment
- NVIDIA NIM API access
- Git with LFS enabled

### Setup
```bash
git clone https://github.com/wimjan123/sovereigns-dilemma.git
cd sovereigns-dilemma
git lfs pull
```

## Development Workflow

### Branching Strategy
- `main` - Production-ready code
- `develop` - Integration branch for features
- `feature/*` - Individual feature development
- `hotfix/*` - Critical production fixes

### Coding Standards
- Follow Unity C# conventions
- Use XML documentation for public APIs
- Maintain 60 FPS performance target
- Include unit tests for core political logic

## Architecture Overview

### Core Systems
- **PoliticalSimulation**: Voter behavior and political analysis
- **SocialMediaEngine**: Post processing and response generation
- **UIManagement**: UGUI interface coordination
- **DataPersistence**: SQLite integration and save/load
- **AIIntegration**: NVIDIA NIM API wrapper

### Performance Requirements
- 10,000+ simulated voters
- <1GB memory usage
- 60 FPS stable frame rate
- <2 second AI response time

## Testing Strategy

### Unit Tests
- Political algorithm validation
- Database operation correctness
- API integration functionality

### Integration Tests
- Cross-platform build validation
- Performance regression testing
- AI response quality validation

### Quality Gates
- Code review required for all changes
- Automated testing on CI/CD pipeline
- Performance profiling for optimization features
- Political accuracy validation by experts

## Deployment

### Build Targets
- Windows (x64)
- macOS (Universal)
- Linux (x64)

### Release Process
1. Feature freeze and testing
2. Beta testing with political experts
3. Performance validation
4. Steam/distribution platform approval
5. Launch coordination

See [Implementation Workflow](../planning/implementation-workflow.md) for detailed development phases.