using System;
using System.Threading.Tasks;
using SovereignsDilemma.Core.Events;

namespace SovereignsDilemma.AI.Services
{
    /// <summary>
    /// AI service interface for political content analysis.
    /// Supports multiple providers (NVIDIA NIM, OpenAI, custom endpoints).
    /// </summary>
    public interface IAIAnalysisService
    {
        /// <summary>
        /// Analyzes political content and returns political analysis.
        /// </summary>
        /// <param name="content">The political content to analyze</param>
        /// <returns>Political analysis result</returns>
        Task<PoliticalAnalysis> AnalyzeContentAsync(string content);

        /// <summary>
        /// Batch analyzes multiple political contents for efficiency.
        /// </summary>
        /// <param name="contents">Array of political contents to analyze</param>
        /// <returns>Array of political analysis results</returns>
        Task<PoliticalAnalysis[]> AnalyzeBatchAsync(string[] contents);

        /// <summary>
        /// Generates voter responses to political content.
        /// </summary>
        /// <param name="content">The political content</param>
        /// <param name="voterProfiles">Voter demographic profiles</param>
        /// <returns>Generated voter responses</returns>
        Task<VoterResponse[]> GenerateVoterResponsesAsync(string content, VoterProfile[] voterProfiles);

        /// <summary>
        /// Checks if the AI service is healthy and responsive.
        /// </summary>
        /// <returns>Service health status</returns>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Gets current service status and metrics.
        /// </summary>
        /// <returns>Service status information</returns>
        AIServiceStatus GetStatus();

        /// <summary>
        /// The type of AI provider being used.
        /// </summary>
        AIProviderType ProviderType { get; }

        /// <summary>
        /// Whether the service is currently available.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Event raised when service availability changes.
        /// </summary>
        event EventHandler<ServiceAvailabilityChangedEventArgs> AvailabilityChanged;
    }

    /// <summary>
    /// Political analysis result from AI service.
    /// </summary>
    public class PoliticalAnalysis
    {
        /// <summary>
        /// Political sentiment (-1.0 to 1.0, negative to positive).
        /// </summary>
        public float Sentiment { get; set; }

        /// <summary>
        /// Political spectrum position (-1.0 to 1.0, left to right).
        /// </summary>
        public float PoliticalLean { get; set; }

        /// <summary>
        /// Key political topics identified in the content.
        /// </summary>
        public string[] Topics { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Economic position (-1.0 to 1.0, progressive to conservative).
        /// </summary>
        public float EconomicPosition { get; set; }

        /// <summary>
        /// Social position (-1.0 to 1.0, traditional to progressive).
        /// </summary>
        public float SocialPosition { get; set; }

        /// <summary>
        /// Immigration stance (-1.0 to 1.0, restrictive to open).
        /// </summary>
        public float ImmigrationStance { get; set; }

        /// <summary>
        /// Environmental stance (-1.0 to 1.0, skeptical to activist).
        /// </summary>
        public float EnvironmentalStance { get; set; }

        /// <summary>
        /// Confidence level of the analysis (0.0 to 1.0).
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Analysis timestamp.
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Processing time for the analysis.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Voter demographic profile for targeted analysis.
    /// </summary>
    public class VoterProfile
    {
        /// <summary>
        /// Unique voter identifier.
        /// </summary>
        public string VoterId { get; set; }

        /// <summary>
        /// Voter age group.
        /// </summary>
        public int Age { get; set; }

        /// <summary>
        /// Income level (percentile 0-100).
        /// </summary>
        public int IncomePercentile { get; set; }

        /// <summary>
        /// Education level (1-5 scale).
        /// </summary>
        public int EducationLevel { get; set; }

        /// <summary>
        /// Current political position.
        /// </summary>
        public PoliticalSpectrum CurrentPosition { get; set; }

        /// <summary>
        /// Geographic region in Netherlands.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Urban vs rural classification.
        /// </summary>
        public bool IsUrban { get; set; }
    }

    /// <summary>
    /// Political spectrum position (Dutch context).
    /// </summary>
    public class PoliticalSpectrum
    {
        /// <summary>
        /// Economic position (-100 to +100).
        /// </summary>
        public int Economic { get; set; }

        /// <summary>
        /// Social position (-100 to +100).
        /// </summary>
        public int Social { get; set; }

        /// <summary>
        /// Immigration position (-100 to +100).
        /// </summary>
        public int Immigration { get; set; }

        /// <summary>
        /// Environment position (-100 to +100).
        /// </summary>
        public int Environment { get; set; }
    }

    /// <summary>
    /// Generated voter response to political content.
    /// </summary>
    public class VoterResponse
    {
        /// <summary>
        /// Voter who generated this response.
        /// </summary>
        public string VoterId { get; set; }

        /// <summary>
        /// Response content text.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Response sentiment (-1.0 to 1.0).
        /// </summary>
        public float Sentiment { get; set; }

        /// <summary>
        /// Engagement level (0.0 to 1.0).
        /// </summary>
        public float EngagementLevel { get; set; }

        /// <summary>
        /// Response type classification.
        /// </summary>
        public ResponseType Type { get; set; }

        /// <summary>
        /// Time taken to generate this response.
        /// </summary>
        public TimeSpan GenerationTime { get; set; }

        /// <summary>
        /// Response timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of voter responses.
    /// </summary>
    public enum ResponseType
    {
        Support,
        Opposition,
        Question,
        Neutral,
        Emotional,
        Factual
    }

    /// <summary>
    /// AI service provider types.
    /// </summary>
    public enum AIProviderType
    {
        NvidiaNIM,
        OpenAI,
        Custom,
        Offline
    }

    /// <summary>
    /// AI service status information.
    /// </summary>
    public class AIServiceStatus
    {
        /// <summary>
        /// Whether the service is currently operational.
        /// </summary>
        public bool IsOperational { get; set; }

        /// <summary>
        /// Current average response time.
        /// </summary>
        public TimeSpan AverageResponseTime { get; set; }

        /// <summary>
        /// Number of requests processed today.
        /// </summary>
        public int RequestsToday { get; set; }

        /// <summary>
        /// Number of failed requests today.
        /// </summary>
        public int FailedRequestsToday { get; set; }

        /// <summary>
        /// Last successful request timestamp.
        /// </summary>
        public DateTime? LastSuccessfulRequest { get; set; }

        /// <summary>
        /// Circuit breaker state.
        /// </summary>
        public CircuitBreakerState CircuitBreakerState { get; set; }

        /// <summary>
        /// Cache hit rate percentage.
        /// </summary>
        public float CacheHitRate { get; set; }
    }

    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    public enum CircuitBreakerState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Event arguments for service availability changes.
    /// </summary>
    public class ServiceAvailabilityChangedEventArgs : EventArgs
    {
        public bool IsAvailable { get; }
        public string Reason { get; }
        public DateTime ChangedAt { get; }

        public ServiceAvailabilityChangedEventArgs(bool isAvailable, string reason)
        {
            IsAvailable = isAvailable;
            Reason = reason;
            ChangedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Domain event for AI analysis completion.
    /// </summary>
    public class AIAnalysisCompletedEvent : DomainEventBase
    {
        public override string SourceContext => "AI";

        public string Content { get; }
        public PoliticalAnalysis Analysis { get; }
        public TimeSpan ProcessingTime { get; }

        public AIAnalysisCompletedEvent(string content, PoliticalAnalysis analysis, TimeSpan processingTime)
        {
            Content = content;
            Analysis = analysis;
            ProcessingTime = processingTime;
        }
    }
}