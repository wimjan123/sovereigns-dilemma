using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SovereignsDilemma.Core.Database
{
    /// <summary>
    /// Database service interface for cross-platform data persistence.
    /// Supports encrypted SQLite storage with connection pooling.
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// Initializes the database service and creates schema if needed.
        /// </summary>
        /// <param name="connectionString">Database connection string with encryption settings</param>
        Task InitializeAsync(string connectionString);

        /// <summary>
        /// Executes a query and returns results as strongly-typed objects.
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="query">SQL query string</param>
        /// <param name="parameters">Query parameters</param>
        Task<IEnumerable<T>> QueryAsync<T>(string query, object parameters = null);

        /// <summary>
        /// Executes a single query and returns the first result or default.
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="query">SQL query string</param>
        /// <param name="parameters">Query parameters</param>
        Task<T> QuerySingleOrDefaultAsync<T>(string query, object parameters = null);

        /// <summary>
        /// Executes a command and returns the number of affected rows.
        /// </summary>
        /// <param name="command">SQL command string</param>
        /// <param name="parameters">Command parameters</param>
        Task<int> ExecuteAsync(string command, object parameters = null);

        /// <summary>
        /// Executes multiple commands in a transaction.
        /// </summary>
        /// <param name="commands">List of SQL commands with parameters</param>
        Task<int> ExecuteTransactionAsync(IEnumerable<DatabaseCommand> commands);

        /// <summary>
        /// Saves voter state data efficiently using batch operations.
        /// </summary>
        /// <param name="voterStates">Collection of voter states to save</param>
        Task SaveVoterStatesAsync(IEnumerable<VoterStateRecord> voterStates);

        /// <summary>
        /// Loads voter state data for session restoration.
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        Task<IEnumerable<VoterStateRecord>> LoadVoterStatesAsync(string sessionId);

        /// <summary>
        /// Saves political event data and responses.
        /// </summary>
        /// <param name="eventData">Political event information</param>
        Task SavePoliticalEventAsync(PoliticalEventRecord eventData);

        /// <summary>
        /// Loads political events within a time range.
        /// </summary>
        /// <param name="startTime">Start time for event range</param>
        /// <param name="endTime">End time for event range</param>
        Task<IEnumerable<PoliticalEventRecord>> LoadPoliticalEventsAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Saves AI analysis results for caching.
        /// </summary>
        /// <param name="analysis">AI analysis data</param>
        Task SaveAIAnalysisAsync(AIAnalysisRecord analysis);

        /// <summary>
        /// Retrieves cached AI analysis results.
        /// </summary>
        /// <param name="contentHash">Hash of analyzed content</param>
        /// <param name="maxAge">Maximum age of cached results</param>
        Task<AIAnalysisRecord> GetCachedAnalysisAsync(uint contentHash, TimeSpan maxAge);

        /// <summary>
        /// Performs database maintenance operations.
        /// </summary>
        Task MaintenanceAsync();

        /// <summary>
        /// Gets database performance metrics.
        /// </summary>
        Task<DatabaseMetrics> GetMetricsAsync();

        /// <summary>
        /// Closes all connections and cleans up resources.
        /// </summary>
        Task CloseAsync();

        /// <summary>
        /// Database connection status.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Current database schema version.
        /// </summary>
        int SchemaVersion { get; }
    }

    /// <summary>
    /// Database command with parameters for batch operations.
    /// </summary>
    public struct DatabaseCommand
    {
        public string CommandText { get; set; }
        public object Parameters { get; set; }
    }

    /// <summary>
    /// Voter state data record for persistence.
    /// </summary>
    public struct VoterStateRecord
    {
        public int VoterId { get; set; }
        public string SessionId { get; set; }
        public DateTime Timestamp { get; set; }

        // Political opinions (JSON serialized)
        public string PoliticalOpinions { get; set; }

        // Behavior state (JSON serialized)
        public string BehaviorState { get; set; }

        // Social network data (JSON serialized)
        public string SocialNetwork { get; set; }

        // Event responses (JSON serialized)
        public string EventResponses { get; set; }

        // Compressed binary data for performance
        public byte[] CompressedData { get; set; }
    }

    /// <summary>
    /// Political event record for historical tracking.
    /// </summary>
    public struct PoliticalEventRecord
    {
        public string EventId { get; set; }
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }

        // Impact metrics
        public float SentimentImpact { get; set; }
        public float EngagementImpact { get; set; }
        public int AffectedVoters { get; set; }

        // Event data (JSON serialized)
        public string EventData { get; set; }

        // AI analysis results
        public string AIAnalysis { get; set; }
    }

    /// <summary>
    /// AI analysis record for caching and performance.
    /// </summary>
    public struct AIAnalysisRecord
    {
        public string AnalysisId { get; set; }
        public uint ContentHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        // Analysis results (JSON serialized)
        public string Results { get; set; }

        // Metadata
        public string Provider { get; set; }
        public string Model { get; set; }
        public float Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }

        // Usage tracking
        public int HitCount { get; set; }
        public DateTime LastAccessed { get; set; }
    }

    /// <summary>
    /// Database performance and health metrics.
    /// </summary>
    public struct DatabaseMetrics
    {
        public int ActiveConnections { get; set; }
        public int TotalConnections { get; set; }
        public double AverageQueryTime { get; set; }
        public double MaxQueryTime { get; set; }
        public long TotalQueries { get; set; }
        public long TotalBytes { get; set; }
        public double HitRatio { get; set; }
        public DateTime LastMaintenanceRun { get; set; }
        public string DatabaseSize { get; set; }
        public bool IsHealthy { get; set; }
    }

    /// <summary>
    /// Database connection configuration.
    /// </summary>
    public struct DatabaseConfig
    {
        public string DatabasePath { get; set; }
        public string EncryptionKey { get; set; }
        public int MaxConnections { get; set; }
        public int ConnectionTimeout { get; set; }
        public int CommandTimeout { get; set; }
        public bool EnableWAL { get; set; }
        public bool EnableCompression { get; set; }
        public int CacheSize { get; set; }
        public bool AutoVacuum { get; set; }
    }

    /// <summary>
    /// Database operation result with timing information.
    /// </summary>
    public struct DatabaseResult<T>
    {
        public T Data { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int RecordsAffected { get; set; }
    }

    /// <summary>
    /// Exception thrown for database-specific errors.
    /// </summary>
    public class DatabaseException : Exception
    {
        public string Operation { get; }
        public TimeSpan ExecutionTime { get; }

        public DatabaseException(string operation, string message) : base(message)
        {
            Operation = operation;
        }

        public DatabaseException(string operation, string message, Exception innerException)
            : base(message, innerException)
        {
            Operation = operation;
        }

        public DatabaseException(string operation, string message, TimeSpan executionTime)
            : base(message)
        {
            Operation = operation;
            ExecutionTime = executionTime;
        }
    }
}