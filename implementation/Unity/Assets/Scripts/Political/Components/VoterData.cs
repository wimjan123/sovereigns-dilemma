using System;
using Unity.Entities;
using Unity.Mathematics;

namespace SovereignsDilemma.Political.Components
{
    /// <summary>
    /// Core voter identity and demographic data component.
    /// Immutable data that doesn't change during simulation.
    /// </summary>
    public struct VoterData : IComponentData
    {
        public int VoterId;
        public int Age;
        public byte EducationLevel;        // 1-5 scale
        public byte IncomePercentile;      // 0-100
        public byte Region;                // Dutch province index
        public bool IsUrban;
        public byte Gender;                // 0=M, 1=F, 2=Other
        public byte Religion;              // Religious affiliation index

        // Packed flags for efficiency
        public VoterFlags Flags;
    }

    /// <summary>
    /// Packed voter characteristics and traits for memory efficiency.
    /// </summary>
    [Flags]
    public enum VoterFlags : byte
    {
        None = 0,
        IsUrban = 1 << 0,
        HasChildren = 1 << 1,
        IsEmployed = 1 << 2,
        IsHomeowner = 1 << 3,
        IsImmigrant = 1 << 4,
        IsDisabled = 1 << 5,
        IsStudent = 1 << 6,
        IsRetired = 1 << 7
    }

    /// <summary>
    /// Current political opinions and positions.
    /// Dynamic data that changes based on events and AI analysis.
    /// </summary>
    public struct PoliticalOpinion : IComponentData
    {
        // Political spectrum positions (-100 to +100)
        public sbyte EconomicPosition;     // Left to Right
        public sbyte SocialPosition;       // Conservative to Progressive
        public sbyte ImmigrationStance;    // Restrictive to Open
        public sbyte EnvironmentalStance;  // Skeptical to Activist

        // Party preferences (0-255, higher = more support)
        public byte VVDSupport;            // Liberal conservative
        public byte PVVSupport;            // Populist right
        public byte CDASupport;            // Christian democratic
        public byte D66Support;            // Social liberal
        public byte SPSupport;             // Socialist
        public byte PvdASupport;           // Social democratic
        public byte GLSupport;             // Green left
        public byte CUSupport;             // Christian union
        public byte SGPSupport;            // Reformed political
        public byte DENKSupport;           // Immigrant interests
        public byte FvDSupport;            // Right-wing populist
        public byte VoltSupport;           // Pro-European

        // Confidence in current opinions (0-255)
        public byte Confidence;

        // Last update timestamp (frame number)
        public uint LastUpdated;
    }

    /// <summary>
    /// Behavioral state and decision-making data.
    /// Controls how voter responds to political events.
    /// </summary>
    public struct BehaviorState : IComponentData
    {
        // Personality traits (0-255)
        public byte Openness;              // Open to new ideas
        public byte Conscientiousness;     // Structured thinking
        public byte Extraversion;          // Social engagement
        public byte Agreeableness;         // Cooperative nature
        public byte Neuroticism;           // Emotional stability

        // Information processing
        public byte MediaConsumption;      // How much news they consume
        public byte SocialInfluence;       // Susceptible to peer pressure
        public byte AuthorityTrust;        // Trust in institutions
        public byte ChangeResistance;      // Resistance to opinion changes

        // Current emotional state
        public byte Satisfaction;          // With current government
        public byte Anxiety;               // About future
        public byte Anger;                 // About current issues
        public byte Hope;                  // For positive change

        // Behavioral flags
        public BehaviorFlags Flags;
    }

    /// <summary>
    /// Voter behavioral characteristics and current states.
    /// </summary>
    [Flags]
    public enum BehaviorFlags : byte
    {
        None = 0,
        IsEngaged = 1 << 0,               // Actively follows politics
        IsInfluencer = 1 << 1,            // Influences others
        IsVolatile = 1 << 2,              // Changes opinions quickly
        IsLoyalist = 1 << 3,              // Loyal to one party
        IsProtester = 1 << 4,             // Likely to protest
        IsActivist = 1 << 5,              // Political activist
        IsApathetic = 1 << 6,             // Doesn't care about politics
        IsEarlyAdopter = 1 << 7           // Adopts new ideas early
    }

    /// <summary>
    /// Social network connections and influence relationships.
    /// Represents how voters influence each other.
    /// </summary>
    public struct SocialNetwork : IComponentData
    {
        // Network metrics
        public byte NetworkSize;           // Number of connections
        public byte InfluenceScore;        // How much they influence others
        public byte SusceptibilityScore;   // How easily influenced

        // Connection quality
        public byte FamilyConnections;     // Strong family ties
        public byte WorkConnections;       // Professional network
        public byte SocialConnections;     // Friends and social groups
        public byte OnlineConnections;     // Social media engagement

        // Echo chamber metrics
        public byte EchoChamberStrength;   // How isolated their views are
        public byte DiversityExposure;    // Exposure to different viewpoints

        // Last social interaction frame
        public uint LastInteraction;
    }

    /// <summary>
    /// Response to political events and content.
    /// Tracks how voter reacts to specific stimuli.
    /// </summary>
    public struct EventResponse : IComponentData
    {
        // Response intensities (0-255)
        public byte EmotionalResponse;     // How emotional their reaction
        public byte RationalResponse;      // How analytical their reaction
        public byte SocialResponse;        // How much they share/discuss

        // Response types
        public ResponseType LastResponseType;
        public byte ResponseStrength;      // Intensity of last response

        // Memory decay
        public byte AttentionSpan;         // How long they remember events
        public uint LastEventFrame;        // When they last responded to event

        // Accumulation effects
        public sbyte CumulativeImpact;     // Net opinion change over time
    }

    /// <summary>
    /// Types of responses voters can have to political content.
    /// </summary>
    public enum ResponseType : byte
    {
        None = 0,
        Support = 1,
        Opposition = 2,
        Questioning = 3,
        Sharing = 4,
        Ignoring = 5,
        Emotional = 6,
        Analytical = 7
    }

    /// <summary>
    /// AI analysis cache for voter behavior prediction.
    /// Reduces API calls by caching recent analysis results.
    /// </summary>
    public struct AIAnalysisCache : IComponentData
    {
        // Cached predictions (0-255)
        public byte PredictedPartySupport; // Most likely party support
        public byte PredictedEngagement;   // Likelihood to engage
        public byte PredictedVolatility;   // Likelihood to change opinion

        // Cache metadata
        public uint CachedAtFrame;         // When analysis was cached
        public byte CacheConfidence;       // Confidence in cached data
        public AnalysisFlags Flags;        // Cache status flags

        // Content hash for cache validation
        public uint ContentHash;           // Hash of analyzed content
    }

    /// <summary>
    /// AI analysis cache status flags.
    /// </summary>
    [Flags]
    public enum AnalysisFlags : byte
    {
        None = 0,
        HasCachedData = 1 << 0,
        NeedsRefresh = 1 << 1,
        AnalysisInProgress = 1 << 2,
        HighConfidence = 1 << 3,
        Representative = 1 << 4,          // This voter represents a cluster
        Outlier = 1 << 5,                 // Unusual voting pattern
        Synthetic = 1 << 6,               // AI-generated voter
        Validated = 1 << 7                // Expert-validated behavior
    }

    /// <summary>
    /// Spatial position for visualization and neighborhood effects.
    /// </summary>
    public struct SpatialPosition : IComponentData
    {
        public float3 Position;            // World position for visualization
        public float3 Velocity;            // Movement velocity
        public byte Cluster;               // Spatial cluster ID
        public byte Density;               // Local neighborhood density
    }

    /// <summary>
    /// Memory pool management for performance optimization.
    /// Tracks voter lifecycle and memory allocation.
    /// </summary>
    public struct MemoryPool : IComponentData
    {
        public uint AllocationFrame;       // When voter was allocated
        public byte PoolIndex;             // Memory pool index
        public byte ReferenceCount;        // Reference counting
        public LifecycleState State;       // Current lifecycle state
    }

    /// <summary>
    /// Voter entity lifecycle states.
    /// </summary>
    public enum LifecycleState : byte
    {
        Unallocated = 0,
        Allocated = 1,
        Active = 2,
        Sleeping = 3,
        PendingDestroy = 4,
        Destroyed = 5
    }
}