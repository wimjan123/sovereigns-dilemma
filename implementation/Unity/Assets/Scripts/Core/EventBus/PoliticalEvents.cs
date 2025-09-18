using System;
using System.Collections.Generic;
using Unity.Mathematics;
using SovereignsDilemma.Political.Components;

namespace SovereignsDilemma.Core.EventBus
{
    /// <summary>
    /// Political domain events for bounded context communication.
    /// Maintains domain integrity while enabling cross-context integration.
    /// </summary>

    // Voter-related events
    public class VoterOpinionChangedEvent : BaseEvent
    {
        public int VoterId { get; set; }
        public PoliticalOpinion OldOpinion { get; set; }
        public PoliticalOpinion NewOpinion { get; set; }
        public OpinionChangeReason Reason { get; set; }
        public float InfluenceStrength { get; set; }

        public VoterOpinionChangedEvent(int voterId, PoliticalOpinion oldOpinion, PoliticalOpinion newOpinion,
            OpinionChangeReason reason, float influenceStrength) : base("Political")
        {
            VoterId = voterId;
            OldOpinion = oldOpinion;
            NewOpinion = newOpinion;
            Reason = reason;
            InfluenceStrength = influenceStrength;
        }
    }

    public class VoterBehaviorChangedEvent : BaseEvent
    {
        public int VoterId { get; set; }
        public BehaviorState OldBehavior { get; set; }
        public BehaviorState NewBehavior { get; set; }
        public BehaviorChangeReason Reason { get; set; }

        public VoterBehaviorChangedEvent(int voterId, BehaviorState oldBehavior, BehaviorState newBehavior,
            BehaviorChangeReason reason) : base("Political")
        {
            VoterId = voterId;
            OldBehavior = oldBehavior;
            NewBehavior = newBehavior;
            Reason = reason;
        }
    }

    public class SocialNetworkUpdatedEvent : BaseEvent
    {
        public int VoterId { get; set; }
        public SocialNetwork NetworkState { get; set; }
        public SocialNetworkChangeType ChangeType { get; set; }
        public List<int> AffectedConnections { get; set; }

        public SocialNetworkUpdatedEvent(int voterId, SocialNetwork networkState,
            SocialNetworkChangeType changeType, List<int> affectedConnections = null) : base("Political")
        {
            VoterId = voterId;
            NetworkState = networkState;
            ChangeType = changeType;
            AffectedConnections = affectedConnections ?? new List<int>();
        }
    }

    public class PoliticalEventOccurredEvent : BaseEvent
    {
        public string EventName { get; set; }
        public PoliticalEventType EventType { get; set; }
        public float3 OpinionImpact { get; set; } // Economic, Social, Environmental
        public List<int> AffectedVoters { get; set; }
        public float EventStrength { get; set; }
        public string Description { get; set; }

        public PoliticalEventOccurredEvent(string eventName, PoliticalEventType eventType,
            float3 opinionImpact, float eventStrength, string description) : base("Political")
        {
            EventName = eventName;
            EventType = eventType;
            OpinionImpact = opinionImpact;
            EventStrength = eventStrength;
            Description = description;
            AffectedVoters = new List<int>();
        }
    }

    // AI-related events
    public class AIAnalysisRequestedEvent : BaseEvent
    {
        public int VoterId { get; set; }
        public AIRequestType RequestType { get; set; }
        public VoterData VoterData { get; set; }
        public PoliticalOpinion Opinion { get; set; }
        public BehaviorState Behavior { get; set; }
        public string RequestId { get; set; }
        public float Priority { get; set; }

        public AIAnalysisRequestedEvent(int voterId, AIRequestType requestType, VoterData voterData,
            PoliticalOpinion opinion, BehaviorState behavior, float priority = 1.0f) : base("AI")
        {
            VoterId = voterId;
            RequestType = requestType;
            VoterData = voterData;
            Opinion = opinion;
            Behavior = behavior;
            Priority = priority;
            RequestId = Guid.NewGuid().ToString();
        }
    }

    public class AIAnalysisCompletedEvent : BaseEvent
    {
        public string RequestId { get; set; }
        public int VoterId { get; set; }
        public AIAnalysisResult Result { get; set; }
        public float ProcessingTimeMs { get; set; }
        public bool FromCache { get; set; }
        public int BatchSize { get; set; }

        public AIAnalysisCompletedEvent(string requestId, int voterId, AIAnalysisResult result,
            float processingTimeMs, bool fromCache = false, int batchSize = 1) : base("AI")
        {
            RequestId = requestId;
            VoterId = voterId;
            Result = result;
            ProcessingTimeMs = processingTimeMs;
            FromCache = fromCache;
            BatchSize = batchSize;
        }
    }

    public class AIBatchProcessedEvent : BaseEvent
    {
        public string BatchId { get; set; }
        public List<int> VoterIds { get; set; }
        public int BatchSize { get; set; }
        public float TotalProcessingTimeMs { get; set; }
        public float AverageProcessingTimeMs { get; set; }
        public AIBatchingStats Stats { get; set; }

        public AIBatchProcessedEvent(string batchId, List<int> voterIds, float totalProcessingTimeMs,
            AIBatchingStats stats) : base("AI")
        {
            BatchId = batchId;
            VoterIds = voterIds;
            BatchSize = voterIds.Count;
            TotalProcessingTimeMs = totalProcessingTimeMs;
            AverageProcessingTimeMs = BatchSize > 0 ? totalProcessingTimeMs / BatchSize : 0f;
            Stats = stats;
        }
    }

    public class AIModelUpdatedEvent : BaseEvent
    {
        public string ModelId { get; set; }
        public string ModelVersion { get; set; }
        public Dictionary<string, float> PerformanceMetrics { get; set; }
        public string UpdateReason { get; set; }

        public AIModelUpdatedEvent(string modelId, string modelVersion, Dictionary<string, float> performanceMetrics,
            string updateReason) : base("AI")
        {
            ModelId = modelId;
            ModelVersion = modelVersion;
            PerformanceMetrics = performanceMetrics;
            UpdateReason = updateReason;
        }
    }

    // Database-related events
    public class VoterDataSavedEvent : BaseEvent
    {
        public List<int> VoterIds { get; set; }
        public int RecordCount { get; set; }
        public float SaveTimeMs { get; set; }
        public bool WasBatchOperation { get; set; }
        public DatabaseOperation OperationType { get; set; }

        public VoterDataSavedEvent(List<int> voterIds, float saveTimeMs, bool wasBatchOperation,
            DatabaseOperation operationType) : base("Database")
        {
            VoterIds = voterIds;
            RecordCount = voterIds.Count;
            SaveTimeMs = saveTimeMs;
            WasBatchOperation = wasBatchOperation;
            OperationType = operationType;
        }
    }

    public class DatabaseBatchCompletedEvent : BaseEvent
    {
        public string BatchId { get; set; }
        public int OperationCount { get; set; }
        public float TotalTimeMs { get; set; }
        public DatabaseOperationType OperationType { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public DatabaseBatchCompletedEvent(string batchId, int operationCount, float totalTimeMs,
            DatabaseOperationType operationType, bool success, string errorMessage = null) : base("Database")
        {
            BatchId = batchId;
            OperationCount = operationCount;
            TotalTimeMs = totalTimeMs;
            OperationType = operationType;
            Success = success;
            ErrorMessage = errorMessage;
        }
    }

    public class DatabaseConnectionEvent : BaseEvent
    {
        public string ConnectionId { get; set; }
        public DatabaseConnectionStatus Status { get; set; }
        public int ActiveConnections { get; set; }
        public int TotalConnections { get; set; }
        public string ErrorMessage { get; set; }

        public DatabaseConnectionEvent(string connectionId, DatabaseConnectionStatus status,
            int activeConnections, int totalConnections, string errorMessage = null) : base("Database")
        {
            ConnectionId = connectionId;
            Status = status;
            ActiveConnections = activeConnections;
            TotalConnections = totalConnections;
            ErrorMessage = errorMessage;
        }
    }

    public class DataCorruptionDetectedEvent : BaseEvent
    {
        public string TableName { get; set; }
        public List<int> AffectedRecords { get; set; }
        public DataCorruptionType CorruptionType { get; set; }
        public string Description { get; set; }
        public bool AutoCorrected { get; set; }

        public DataCorruptionDetectedEvent(string tableName, List<int> affectedRecords,
            DataCorruptionType corruptionType, string description, bool autoCorrected) : base("Database")
        {
            TableName = tableName;
            AffectedRecords = affectedRecords;
            CorruptionType = corruptionType;
            Description = description;
            AutoCorrected = autoCorrected;
        }
    }

    // UI-related events
    public class UIDataUpdatedEvent : BaseEvent
    {
        public string ComponentName { get; set; }
        public UIUpdateType UpdateType { get; set; }
        public Dictionary<string, object> UpdatedData { get; set; }
        public float UpdateTimeMs { get; set; }

        public UIDataUpdatedEvent(string componentName, UIUpdateType updateType,
            Dictionary<string, object> updatedData, float updateTimeMs) : base("UI")
        {
            ComponentName = componentName;
            UpdateType = updateType;
            UpdatedData = updatedData;
            UpdateTimeMs = updateTimeMs;
        }
    }

    public class UserInteractionEvent : BaseEvent
    {
        public string InteractionType { get; set; }
        public string ComponentName { get; set; }
        public Dictionary<string, object> InteractionData { get; set; }
        public float3 ScreenPosition { get; set; }

        public UserInteractionEvent(string interactionType, string componentName,
            Dictionary<string, object> interactionData, float3 screenPosition) : base("UI")
        {
            InteractionType = interactionType;
            ComponentName = componentName;
            InteractionData = interactionData;
            ScreenPosition = screenPosition;
        }
    }

    public class VisualizationUpdatedEvent : BaseEvent
    {
        public string VisualizationType { get; set; }
        public int DataPointCount { get; set; }
        public float RenderTimeMs { get; set; }
        public bool OptimizationApplied { get; set; }

        public VisualizationUpdatedEvent(string visualizationType, int dataPointCount,
            float renderTimeMs, bool optimizationApplied) : base("UI")
        {
            VisualizationType = visualizationType;
            DataPointCount = dataPointCount;
            RenderTimeMs = renderTimeMs;
            OptimizationApplied = optimizationApplied;
        }
    }

    public class PerformanceDisplayEvent : BaseEvent
    {
        public Dictionary<string, float> Metrics { get; set; }
        public PerformanceLevel PerformanceLevel { get; set; }
        public List<string> Warnings { get; set; }

        public PerformanceDisplayEvent(Dictionary<string, float> metrics, PerformanceLevel performanceLevel,
            List<string> warnings = null) : base("UI")
        {
            Metrics = metrics;
            PerformanceLevel = performanceLevel;
            Warnings = warnings ?? new List<string>();
        }
    }

    // Performance-related events
    public class PerformanceMetricsUpdatedEvent : BaseEvent
    {
        public string SystemName { get; set; }
        public Dictionary<string, float> Metrics { get; set; }
        public PerformanceLevel PerformanceLevel { get; set; }
        public bool ThresholdExceeded { get; set; }

        public PerformanceMetricsUpdatedEvent(string systemName, Dictionary<string, float> metrics,
            PerformanceLevel performanceLevel, bool thresholdExceeded) : base("Performance")
        {
            SystemName = systemName;
            Metrics = metrics;
            PerformanceLevel = performanceLevel;
            ThresholdExceeded = thresholdExceeded;
        }
    }

    public class AdaptiveScalingEvent : BaseEvent
    {
        public string SystemName { get; set; }
        public ScalingAction Action { get; set; }
        public float OldValue { get; set; }
        public float NewValue { get; set; }
        public string Reason { get; set; }

        public AdaptiveScalingEvent(string systemName, ScalingAction action, float oldValue, float newValue, string reason) : base("Performance")
        {
            SystemName = systemName;
            Action = action;
            OldValue = oldValue;
            NewValue = newValue;
            Reason = reason;
        }
    }

    public class SystemHealthChangedEvent : BaseEvent
    {
        public string SystemName { get; set; }
        public SystemHealthStatus OldStatus { get; set; }
        public SystemHealthStatus NewStatus { get; set; }
        public string Details { get; set; }

        public SystemHealthChangedEvent(string systemName, SystemHealthStatus oldStatus,
            SystemHealthStatus newStatus, string details) : base("Performance")
        {
            SystemName = systemName;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Details = details;
        }
    }

    public class ResourceUtilizationEvent : BaseEvent
    {
        public string ResourceType { get; set; }
        public float UtilizationPercentage { get; set; }
        public float Threshold { get; set; }
        public bool IsWarning { get; set; }

        public ResourceUtilizationEvent(string resourceType, float utilizationPercentage,
            float threshold, bool isWarning) : base("Performance")
        {
            ResourceType = resourceType;
            UtilizationPercentage = utilizationPercentage;
            Threshold = threshold;
            IsWarning = isWarning;
        }
    }

    // Enums for event classification
    public enum OpinionChangeReason
    {
        SocialInfluence,
        MediaExposure,
        PersonalExperience,
        PoliticalEvent,
        AIRecommendation,
        RandomDrift
    }

    public enum BehaviorChangeReason
    {
        OpinionShift,
        LifeEvent,
        SocialPressure,
        PoliticalCampaign,
        EconomicChange,
        HealthEvent
    }

    public enum SocialNetworkChangeType
    {
        ConnectionAdded,
        ConnectionRemoved,
        InfluenceChanged,
        NetworkExpanded,
        NetworkContracted
    }

    public enum PoliticalEventType
    {
        ElectionCampaign,
        PolicyAnnouncement,
        PoliticalScandal,
        EconomicNews,
        SocialMovement,
        InternationalEvent,
        EnvironmentalCrisis,
        HealthCrisis
    }

    public enum DatabaseOperation
    {
        Insert,
        Update,
        Delete,
        Batch
    }

    public enum DatabaseOperationType
    {
        VoterData,
        OpinionData,
        BehaviorData,
        SocialNetworkData,
        AIAnalysisCache,
        PerformanceMetrics
    }

    public enum DatabaseConnectionStatus
    {
        Connected,
        Disconnected,
        Reconnecting,
        Error
    }

    public enum DataCorruptionType
    {
        InvalidData,
        MissingRecords,
        ConstraintViolation,
        IndexCorruption,
        DataInconsistency
    }

    public enum UIUpdateType
    {
        DataRefresh,
        LayoutChange,
        VisualizationUpdate,
        InteractionResponse,
        PerformanceUpdate
    }

    public enum PerformanceLevel
    {
        Excellent,
        Good,
        Warning,
        Critical
    }

    public enum ScalingAction
    {
        ScaleUp,
        ScaleDown,
        Optimize,
        Maintain
    }

    public enum SystemHealthStatus
    {
        Healthy,
        Degraded,
        Critical,
        Failed
    }

    // Supporting data structures
    public struct AIBatchingStats
    {
        public int TotalRequests;
        public int CacheHits;
        public float CacheHitRatio;
        public int BatchesCreated;
        public float AverageBatchSize;
    }
}