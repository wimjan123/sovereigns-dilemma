# The Sovereign's Dilemma
**A Dutch Political Simulation Game**

[![Unity](https://img.shields.io/badge/Unity-6.0%20LTS-blue)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-green)](https://github.com/wimjan123/sovereigns-dilemma)
[![Language](https://img.shields.io/badge/Language-C%23-purple)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![AI](https://img.shields.io/badge/AI-NVIDIA%20NIM-76B900)](https://developer.nvidia.com/nim)

## Overview

**The Sovereign's Dilemma** is an innovative political simulation game where you govern entirely through a fictional desktop interface. Navigate the complex world of Dutch politics as you rise to power, win elections, and attempt to fix real issues while the world argues back.

### Core Concept

Experience authentic Dutch political dynamics through:
- **Real-time Social Media**: Write free-form posts that trigger AI-generated conversations
- **Dynamic Voter Simulation**: 10,000+ simulated voters with memory, loyalties, and evolving opinions
- **Coalition Politics**: Navigate Netherlands-specific coalition formation and government mechanics
- **Consequential Decisions**: Every choice has delayed, tangible effects on society and stability

## Key Features

### 🗳️ **Authentic Dutch Politics**
- 17+ major political parties with accurate ideological positioning
- Regional demographic variations (urban vs rural, noord vs zuid)
- Coalition formation mechanics true to Dutch parliamentary system
- Real Dutch political issues: immigration, climate, economy, housing

### 🤖 **AI-Powered Interactions**
- NVIDIA NIM API integration for Dutch language processing
- Intelligent voter responses based on political spectrum analysis
- Adaptive AI opponents with strategic campaign behavior
- Procedural content generation for news, scandals, and crises

### 📊 **Deep Simulation Systems**
- Multi-dimensional political spectrum (economic, social, immigration, environment)
- Voter memory and influence networks
- Long-term consequence modeling
- Performance-optimized for large-scale voter simulation

### 🎮 **Engaging Gameplay**
- Weekly time progression with election cycles
- Multiple victory conditions and legacy scoring
- Emergent storytelling through player choices
- Replayability through dynamic political landscapes

## Technical Architecture

### **Game Engine**: Unity 6.0 LTS
- **Cross-platform deployment**: Windows, macOS, Linux
- **UI System**: Unity UGUI (optimized for reliability)
- **Language**: C# for robust game logic
- **Performance**: Optimized for 10,000+ concurrent voter simulation

### **AI Integration**: NVIDIA NIM API
- **Base URL**: `https://integrate.api.nvidia.com/v1`
- **Models**: Llama Nemotron family for Dutch language processing
- **Features**: Political sentiment analysis, voter response generation
- **Optimization**: Response caching and rate limiting

### **Data Management**: SQLite
- **Embedded database** for voter demographics and political state
- **Performance optimized** for large-scale political simulation
- **Cross-platform compatibility** with no external dependencies

## Project Structure

```
influence-and-politics/
├── planning/                     # Project planning and documentation
│   ├── research/                # Technology verification and analysis
│   ├── implementation-workflow.md   # Development roadmap
│   ├── quality-gates-framework.md  # QA and validation framework
│   └── persona-coordination.md     # Team coordination strategy
├── docs/                        # Additional documentation
├── assets/                      # Game assets and resources
└── src/                        # Source code (Unity project)
```

## Development Timeline

### **Phase 1: Foundation & MVP** (Weeks 1-12)
- Unity 6.0 LTS setup and core architecture
- NVIDIA NIM API integration
- Basic voter simulation system
- Social media posting and response mechanics

### **Phase 2: Core Political Mechanics** (Weeks 13-24)
- Advanced voter intelligence with memory systems
- AI opposition and coalition formation
- Political event system and crisis management
- Performance optimization for large-scale simulation

### **Phase 3: Polish & Enhancement** (Weeks 25-36)
- Advanced UI and data visualization
- Procedural content generation
- Audio design and atmospheric polish
- Cross-platform optimization

### **Phase 4: Testing & Release** (Weeks 37-48)
- Beta testing with Dutch political experts
- Final optimization and bug fixing
- Steam/distribution platform preparation
- Launch and post-launch support

## Getting Started

### Prerequisites
- **Unity 6.0 LTS** or later
- **C# development environment** (Visual Studio/Rider)
- **Git LFS** for asset management
- **NVIDIA NIM API access** for AI features

### Installation
```bash
# Clone the repository
git clone https://github.com/wimjan123/sovereigns-dilemma.git
cd sovereigns-dilemma

# Install Git LFS (if not already installed)
git lfs install
git lfs pull

# Open in Unity 6.0 LTS
# File → Open Project → Select project folder
```

### Configuration
1. **NVIDIA NIM API**: Add your API key to project settings
2. **Build Settings**: Configure target platforms in Unity
3. **Performance**: Adjust voter simulation scale based on hardware

## Research & Verification

This project is built on comprehensive technical research:

- ✅ **Unity 6.0 LTS verified** for cross-platform development
- ✅ **NVIDIA NIM Dutch language support confirmed**
- ✅ **Performance requirements validated** for large-scale simulation
- ✅ **Political accuracy research** with Dutch political context

See [`planning/research/`](planning/research/) for detailed technical analysis.

## Contributing

We welcome contributions from:
- **Game developers** familiar with Unity and C#
- **Political scientists** with Dutch political expertise
- **UX designers** experienced in data-heavy interfaces
- **Localization experts** for Dutch language accuracy

### Development Standards
- **Code Quality**: Follow Unity best practices and C# conventions
- **Performance**: Maintain 60 FPS with full voter simulation
- **Political Accuracy**: All political content validated by experts
- **Cross-Platform**: Test on Windows, macOS, and Linux

## Political Accuracy & Ethics

This simulation aims for educational value and political understanding:

- **Balanced Representation**: All major Dutch political parties represented fairly
- **Educational Purpose**: Helps users understand political complexity
- **Cultural Sensitivity**: Respectful treatment of Dutch political culture
- **Expert Validation**: Political accuracy verified by Dutch political scientists

## Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Game Engine** | Unity 6.0 LTS | Cross-platform game development |
| **Language** | C# | Robust game logic and simulation |
| **UI Framework** | Unity UGUI | Reliable UI for complex political dashboards |
| **AI Processing** | NVIDIA NIM API | Dutch political analysis and response generation |
| **Database** | SQLite | Embedded voter and political data storage |
| **Version Control** | Git + LFS | Source code and asset management |

## Performance Targets

- **Voter Simulation**: 10,000+ concurrent simulated voters
- **Frame Rate**: Stable 60 FPS with full simulation load
- **Memory Usage**: <1GB total memory footprint
- **Load Times**: <10 seconds for game startup and save loading
- **Response Time**: <2 seconds for AI-generated voter responses

## Roadmap

### **Immediate Goals** (Next 6 Months)
- [ ] Complete Unity 6.0 LTS foundation
- [ ] Implement NVIDIA NIM integration
- [ ] Basic voter simulation system
- [ ] MVP social media mechanics

### **Short-term Goals** (6-12 Months)
- [ ] Advanced voter intelligence
- [ ] AI opposition system
- [ ] Coalition formation mechanics
- [ ] Beta testing with political experts

### **Long-term Vision** (12+ Months)
- [ ] Public release on Steam
- [ ] Post-launch content updates
- [ ] International expansion (other countries)
- [ ] Educational institution partnerships

## License

This project is intended for educational and entertainment purposes. Political content is presented for simulation purposes only and does not represent endorsement of any political positions.

## Contact & Community

- **GitHub Issues**: Report bugs and request features
- **Discussions**: Share ideas and feedback
- **Email**: [Project contact information]

---

**Built with passion for political education and authentic simulation.**
*Helping people understand the complexity and nuance of democratic governance.*