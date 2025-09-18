using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace SovereignsDilemma.Political.Components
{
    /// <summary>
    /// Component marking a voter as currently having AI analysis in progress.
    /// Used to prevent duplicate analysis requests and manage system load.
    /// </summary>
    public struct AIAnalysisInProgress : IComponentData
    {
        public float RequestTime;
        public AIRequestType RequestType;
        public uint RequestId;
    }

    /// <summary>
    /// Enhanced AI analysis cache with improved caching strategies and performance tracking.
    /// </summary>
    public struct AIAnalysisCache : IComponentData
    {
        public float LastAnalysisTime;
        public PoliticalOpinion LastKnownOpinion;
        public AIAnalysisResult AnalysisResult;
        public float Confidence;
        public uint ValidationTimestamp;
        public AIRequestType LastRequestType;
        public int AnalysisCount;
        public float TotalAnalysisTime;
    }

    /// <summary>
    /// Types of AI analysis requests for different voter scenarios.
    /// </summary>
    public enum AIRequestType : byte
    {
        GeneralAnalysis = 0,
        PartyRecommendation = 1,
        VotingPrediction = 2,
        IssueAnalysis = 3,
        InfluenceAnalysis = 4,
        BehaviorPrediction = 5
    }

    /// <summary>
    /// Predicted voting behavior categories.
    /// </summary>
    public enum PredictedBehavior : byte
    {
        Unlikely = 0,
        Possible = 1,
        Likely = 2,
        Certain = 3,
        Abstain = 4
    }

    /// <summary>
    /// Comprehensive AI analysis result with enhanced prediction capabilities.
    /// </summary>
    [Serializable]
    public struct AIAnalysisResult
    {
        public List<PartyRecommendation> PartyRecommendations;
        public PredictedBehavior PredictedBehavior;
        public List<string> InfluenceFactors;
        public float Confidence;
        public float ReasoningDepth;
        public float ProcessingTime;
        public int BatchSize;
        public VotingLikelihood VotingLikelihood;
        public List<PolicyPreference> PolicyPreferences;
        public SocialInfluencePattern SocialPattern;
    }

    /// <summary>
    /// Party recommendation with confidence and reasoning.
    /// </summary>
    [Serializable]
    public struct PartyRecommendation
    {
        public string PartyId;
        public float Confidence;
        public string Reasoning;
        public float PolicyAlignment;
        public float HistoricalFit;
        public float SocialInfluence;
    }

    /// <summary>
    /// Voting likelihood assessment across different scenarios.
    /// </summary>
    [Serializable]
    public struct VotingLikelihood
    {
        public float GeneralElection;
        public float LocalElection;
        public float Referendum;
        public float EarlyVoting;
        public float WeatherImpact;
        public float MotivationLevel;
    }

    /// <summary>
    /// Policy preference with strength and priority.
    /// </summary>
    [Serializable]
    public struct PolicyPreference
    {
        public string PolicyArea;
        public float Importance;
        public float Position;
        public float Certainty;
        public List<string> KeyIssues;
    }

    /// <summary>
    /// Social influence pattern analysis.
    /// </summary>
    [Serializable]
    public struct SocialInfluencePattern
    {
        public float Susceptibility;
        public float InfluenceStrength;
        public List<string> InfluenceChannels;
        public float PeerPressure;
        public float MediaInfluence;
        public float FamilyInfluence;
    }

    /// <summary>
    /// AI model configuration for different analysis types.
    /// </summary>
    public struct AIModelConfig : IComponentData
    {
        public AIModelType ModelType;
        public float Temperature;
        public int MaxTokens;
        public float ConfidenceThreshold;
        public bool EnableCaching;
        public int BatchSize;
        public float TimeoutSeconds;
    }

    /// <summary>
    /// Types of AI models available for analysis.
    /// </summary>
    public enum AIModelType : byte
    {
        PoliticalAnalysis = 0,
        BehaviorPrediction = 1,
        SentimentAnalysis = 2,
        TrendAnalysis = 3,
        InfluenceMapping = 4
    }

    /// <summary>
    /// AI service connection and performance metrics.
    /// </summary>
    public struct AIServiceMetrics : IComponentData
    {
        public int TotalRequests;
        public int SuccessfulRequests;
        public int FailedRequests;
        public int CachedResponses;
        public float AverageResponseTime;
        public float LastRequestTime;
        public bool IsConnected;
        public float ServiceHealth;
        public int ActiveConnections;
        public float ThroughputPerSecond;
    }

    /// <summary>
    /// Component for tracking AI-driven voter behavior modifications.
    /// </summary>
    public struct AIInfluenceHistory : IBufferElementData
    {
        public float Timestamp;
        public AIRequestType InfluenceType;
        public float InfluenceStrength;
        public float3 OpinionChange;
        public float ConfidenceLevel;
        public uint SourceAnalysisId;
    }

    /// <summary>
    /// AI analysis queue entry for batch processing optimization.
    /// </summary>
    public struct AIAnalysisQueueEntry : IBufferElementData
    {
        public Entity VoterEntity;
        public AIRequestType RequestType;
        public float Priority;
        public float QueueTime;
        public uint RequestId;
        public bool IsHighPriority;
    }

    /// <summary>
    /// Component for AI-driven event response predictions.
    /// </summary>
    public struct AIEventResponsePrediction : IComponentData
    {
        public float EventSensitivity;
        public float ResponseSpeed;
        public float OpinionVolatility;
        public float InfluenceReceptivity;
        public List<EventType> SensitiveEvents;
        public float LastEventImpact;
        public float PredictedRecoveryTime;
    }

    /// <summary>
    /// Types of political events that can influence voter behavior.
    /// </summary>
    public enum EventType : byte
    {
        EconomicNews = 0,
        PoliticalScandal = 1,
        PolicyAnnouncement = 2,
        DebatePerformance = 3,
        MediaCoverage = 4,
        SocialMovement = 5,
        InternationalEvent = 6,
        LocalIssue = 7,
        ClimateEvent = 8,
        HealthCrisis = 9
    }

    /// <summary>
    /// AI-driven voter segmentation for targeted analysis.
    /// </summary>
    public struct AIVoterSegment : IComponentData
    {
        public VoterSegmentType SegmentType;
        public float SegmentConfidence;
        public List<string> SegmentCharacteristics;
        public float AnalysisPriority;
        public float PredictabilityScore;
        public float InfluenceScore;
        public uint LastSegmentUpdate;
    }

    /// <summary>
    /// Voter segment types for AI analysis optimization.
    /// </summary>
    public enum VoterSegmentType : byte
    {
        Undecided = 0,
        StrongPartisan = 1,
        SwingVoter = 2,
        SingleIssue = 3,
        Disengaged = 4,
        HighlyEngaged = 5,
        Influencer = 6,
        Follower = 7,
        Independent = 8,
        Tactical = 9
    }

    /// <summary>
    /// Real-time AI analysis performance tracking.
    /// </summary>
    public struct AIPerformanceTracker : IComponentData
    {
        public float ProcessingTime;
        public float QueueWaitTime;
        public float BatchEfficiency;
        public float CacheHitRatio;
        public int RequestsPerSecond;
        public float ErrorRate;
        public float ResourceUtilization;
        public bool IsOptimal;
        public float LastOptimizationTime;
        public int ConsecutiveFailures;
    }

    /// <summary>
    /// AI model selection and routing for optimal performance.
    /// </summary>
    public struct AIModelRouter : IComponentData
    {
        public AIModelType PrimaryModel;
        public AIModelType FallbackModel;
        public float ModelSwitchThreshold;
        public float LoadBalancingWeight;
        public bool EnableAdaptiveRouting;
        public float ModelPerformanceScore;
        public int ModelSwitchCount;
        public float LastSwitchTime;
    }
}