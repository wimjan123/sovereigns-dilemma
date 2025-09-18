using System.Reflection;
using System.Runtime.CompilerServices;

// Assembly: SovereignsDilemma.Core
// Description: Core systems and infrastructure for The Sovereign's Dilemma
// Version: 1.0.0

[assembly: AssemblyTitle("SovereignsDilemma.Core")]
[assembly: AssemblyDescription("Core systems and infrastructure for Dutch Political Simulation")]
[assembly: AssemblyVersion("1.0.0")]
[assembly: AssemblyFileVersion("1.0.0")]

// Friend assemblies for bounded context access
[assembly: InternalsVisibleTo("SovereignsDilemma.Political")]
[assembly: InternalsVisibleTo("SovereignsDilemma.AI")]
[assembly: InternalsVisibleTo("SovereignsDilemma.UI")]
[assembly: InternalsVisibleTo("SovereignsDilemma.Data")]
[assembly: InternalsVisibleTo("SovereignsDilemma.Tests")]