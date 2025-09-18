using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Data.Database
{
    /// <summary>
    /// Optimized database service with batch operations, connection pooling, and intelligent indexing
    /// for high-performance voter data management supporting 10,000+ voters.
    /// </summary>
    public class OptimizedDatabaseService : IDisposable
    {
        // Connection pooling configuration
        private const int MIN_POOL_SIZE = 2;
        private const int MAX_POOL_SIZE = 10;
        private const int CONNECTION_TIMEOUT_SECONDS = 30;
        private const int COMMAND_TIMEOUT_SECONDS = 15;

        // Batch operation configuration
        private const int DEFAULT_BATCH_SIZE = 500;
        private const int MAX_BATCH_SIZE = 1000;
        private const int BATCH_TIMEOUT_MS = 100;

        // Cache configuration
        private const int CACHE_SIZE = 1000;
        private const float CACHE_TTL_SECONDS = 300f; // 5 minutes

        // Connection pool
        private readonly ConcurrentQueue<SQLiteConnection> _connectionPool;
        private readonly ConcurrentDictionary<int, DateTime> _connectionLastUsed;
        private readonly object _poolLock = new object();
        private int _activeConnections = 0;
        private int _totalConnections = 0;

        // Batch operation queues
        private readonly ConcurrentQueue<VoterDataUpdate> _voterUpdateQueue;
        private readonly ConcurrentQueue<OpinionUpdate> _opinionUpdateQueue;
        private readonly ConcurrentQueue<BehaviorUpdate> _behaviorUpdateQueue;
        private readonly Dictionary<string, List<object>> _batchQueues;

        // Performance tracking
        private readonly PerformanceMetrics _metrics;
        private float _lastBatchFlush;

        // Cache system
        private readonly Dictionary<int, CachedVoterData> _voterCache;
        private readonly Dictionary<string, CachedQueryResult> _queryCache;

        private string _connectionString;
        private bool _isInitialized = false;

        public OptimizedDatabaseService(string databasePath, bool enableEncryption = true)
        {
            _connectionString = BuildConnectionString(databasePath, enableEncryption);
            _connectionPool = new ConcurrentQueue<SQLiteConnection>();
            _connectionLastUsed = new ConcurrentDictionary<int, DateTime>();

            _voterUpdateQueue = new ConcurrentQueue<VoterDataUpdate>();
            _opinionUpdateQueue = new ConcurrentQueue<OpinionUpdate>();
            _behaviorUpdateQueue = new ConcurrentQueue<BehaviorUpdate>();
            _batchQueues = new Dictionary<string, List<object>>();

            _metrics = new PerformanceMetrics();
            _voterCache = new Dictionary<int, CachedVoterData>();
            _queryCache = new Dictionary<string, CachedQueryResult>();

            InitializeDatabase();
        }

        private string BuildConnectionString(string databasePath, bool enableEncryption)
        {
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                Version = 3,
                DefaultTimeout = CONNECTION_TIMEOUT_SECONDS,
                Pooling = false, // We handle our own pooling
                JournalMode = SQLiteJournalModeEnum.Wal, // Better concurrency
                SynchronousMode = SynchronousEnum.Normal, // Balance safety/performance
                CacheSize = 10000, // 10MB cache
                PageSize = 4096,
                ReadOnly = false
            };

            if (enableEncryption)
            {
                // In production, this would use secure key management
                builder.Password = GetEncryptionKey();
            }

            return builder.ConnectionString;
        }

        private string GetEncryptionKey()
        {
            // In production, retrieve from secure credential storage
            // For development, use a fixed key
            return "dev_encryption_key_2024";
        }

        private void InitializeDatabase()
        {
            try
            {
                CreateConnectionPool();
                CreateTables();
                CreateIndexes();
                CreateTriggers();
                _isInitialized = true;

                Debug.Log("Optimized database service initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize database: {ex.Message}");
                throw;
            }
        }

        private void CreateConnectionPool()
        {
            // Create minimum pool size connections
            for (int i = 0; i < MIN_POOL_SIZE; i++)
            {
                var connection = CreateConnection();
                _connectionPool.Enqueue(connection);
                _connectionLastUsed[connection.GetHashCode()] = DateTime.UtcNow;
                _totalConnections++;
            }

            Debug.Log($"Created database connection pool with {MIN_POOL_SIZE} connections");
        }

        private SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Enable SQLite optimizations
            ExecuteNonQuery(connection, "PRAGMA temp_store = MEMORY;");
            ExecuteNonQuery(connection, "PRAGMA mmap_size = 268435456;"); // 256MB memory map
            ExecuteNonQuery(connection, "PRAGMA cache_size = 10000;");
            ExecuteNonQuery(connection, "PRAGMA synchronous = NORMAL;");
            ExecuteNonQuery(connection, "PRAGMA journal_mode = WAL;");

            return connection;
        }

        private void CreateTables()
        {
            using var connection = GetConnection();

            var createVotersTable = @"
                CREATE TABLE IF NOT EXISTS Voters (
                    VoterId INTEGER PRIMARY KEY,
                    Age INTEGER NOT NULL,
                    EducationLevel INTEGER NOT NULL,
                    IncomeLevel INTEGER NOT NULL,
                    UrbanizationLevel INTEGER NOT NULL,
                    Province TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            var createOpinionsTable = @"
                CREATE TABLE IF NOT EXISTS PoliticalOpinions (
                    VoterId INTEGER PRIMARY KEY,
                    EconomicPosition REAL NOT NULL,
                    SocialPosition REAL NOT NULL,
                    EnvironmentalPosition REAL NOT NULL,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (VoterId) REFERENCES Voters (VoterId) ON DELETE CASCADE
                );";

            var createBehaviorTable = @"
                CREATE TABLE IF NOT EXISTS BehaviorStates (
                    VoterId INTEGER PRIMARY KEY,
                    Satisfaction REAL NOT NULL,
                    PoliticalEngagement REAL NOT NULL,
                    OpinionVolatility REAL NOT NULL,
                    LastVoteIntention TEXT,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (VoterId) REFERENCES Voters (VoterId) ON DELETE CASCADE
                );";

            var createSocialNetworkTable = @"
                CREATE TABLE IF NOT EXISTS SocialNetworks (
                    VoterId INTEGER PRIMARY KEY,
                    ConnectionCount INTEGER NOT NULL DEFAULT 0,
                    InfluenceScore REAL NOT NULL DEFAULT 0.0,
                    NetworkData TEXT, -- JSON blob for connections
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (VoterId) REFERENCES Voters (VoterId) ON DELETE CASCADE
                );";

            var createAnalysisCacheTable = @"
                CREATE TABLE IF NOT EXISTS AIAnalysisCache (
                    VoterId INTEGER PRIMARY KEY,
                    LastAnalysisTime REAL NOT NULL,
                    CacheData TEXT NOT NULL, -- JSON blob for analysis results
                    Confidence REAL NOT NULL,
                    ValidationTimestamp INTEGER NOT NULL,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (VoterId) REFERENCES Voters (VoterId) ON DELETE CASCADE
                );";

            var createPerformanceTable = @"
                CREATE TABLE IF NOT EXISTS PerformanceMetrics (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MetricName TEXT NOT NULL,
                    Value REAL NOT NULL,
                    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            ExecuteNonQuery(connection, createVotersTable);
            ExecuteNonQuery(connection, createOpinionsTable);
            ExecuteNonQuery(connection, createBehaviorTable);
            ExecuteNonQuery(connection, createSocialNetworkTable);
            ExecuteNonQuery(connection, createAnalysisCacheTable);
            ExecuteNonQuery(connection, createPerformanceTable);

            ReturnConnection(connection);
            Debug.Log("Database tables created successfully");
        }

        private void CreateIndexes()
        {
            using var connection = GetConnection();

            var indexes = new[]
            {
                // Primary indexes for fast lookups
                "CREATE INDEX IF NOT EXISTS idx_voters_age ON Voters(Age);",
                "CREATE INDEX IF NOT EXISTS idx_voters_education ON Voters(EducationLevel);",
                "CREATE INDEX IF NOT EXISTS idx_voters_province ON Voters(Province);",
                "CREATE INDEX IF NOT EXISTS idx_voters_updated ON Voters(UpdatedAt);",

                // Composite indexes for complex queries
                "CREATE INDEX IF NOT EXISTS idx_voters_demographics ON Voters(Age, EducationLevel, IncomeLevel);",
                "CREATE INDEX IF NOT EXISTS idx_opinions_spectrum ON PoliticalOpinions(EconomicPosition, SocialPosition);",
                "CREATE INDEX IF NOT EXISTS idx_behavior_engagement ON BehaviorStates(PoliticalEngagement, OpinionVolatility);",

                // Performance-critical indexes
                "CREATE INDEX IF NOT EXISTS idx_social_influence ON SocialNetworks(InfluenceScore);",
                "CREATE INDEX IF NOT EXISTS idx_analysis_time ON AIAnalysisCache(LastAnalysisTime);",
                "CREATE INDEX IF NOT EXISTS idx_performance_metric ON PerformanceMetrics(MetricName, Timestamp);",

                // Update tracking indexes
                "CREATE INDEX IF NOT EXISTS idx_opinions_updated ON PoliticalOpinions(UpdatedAt);",
                "CREATE INDEX IF NOT EXISTS idx_behavior_updated ON BehaviorStates(UpdatedAt);",
                "CREATE INDEX IF NOT EXISTS idx_social_updated ON SocialNetworks(UpdatedAt);"
            };

            foreach (var indexSql in indexes)
            {
                ExecuteNonQuery(connection, indexSql);
            }

            ReturnConnection(connection);
            Debug.Log($"Created {indexes.Length} database indexes for optimization");
        }

        private void CreateTriggers()
        {
            using var connection = GetConnection();

            // Automatic timestamp updates
            var updateTriggers = new[]
            {
                @"CREATE TRIGGER IF NOT EXISTS update_voters_timestamp
                  AFTER UPDATE ON Voters
                  BEGIN UPDATE Voters SET UpdatedAt = CURRENT_TIMESTAMP WHERE VoterId = NEW.VoterId; END;",

                @"CREATE TRIGGER IF NOT EXISTS update_opinions_timestamp
                  AFTER UPDATE ON PoliticalOpinions
                  BEGIN UPDATE PoliticalOpinions SET UpdatedAt = CURRENT_TIMESTAMP WHERE VoterId = NEW.VoterId; END;",

                @"CREATE TRIGGER IF NOT EXISTS update_behavior_timestamp
                  AFTER UPDATE ON BehaviorStates
                  BEGIN UPDATE BehaviorStates SET UpdatedAt = CURRENT_TIMESTAMP WHERE VoterId = NEW.VoterId; END;",

                @"CREATE TRIGGER IF NOT EXISTS update_social_timestamp
                  AFTER UPDATE ON SocialNetworks
                  BEGIN UPDATE SocialNetworks SET UpdatedAt = CURRENT_TIMESTAMP WHERE VoterId = NEW.VoterId; END;",

                @"CREATE TRIGGER IF NOT EXISTS update_analysis_timestamp
                  AFTER UPDATE ON AIAnalysisCache
                  BEGIN UPDATE AIAnalysisCache SET UpdatedAt = CURRENT_TIMESTAMP WHERE VoterId = NEW.VoterId; END;"
            };

            foreach (var triggerSql in updateTriggers)
            {
                ExecuteNonQuery(connection, triggerSql);
            }

            ReturnConnection(connection);
            Debug.Log("Created database triggers for automatic timestamp management");
        }

        public SQLiteConnection GetConnection()
        {
            lock (_poolLock)
            {
                // Try to get connection from pool
                if (_connectionPool.TryDequeue(out var connection))
                {
                    _connectionLastUsed[connection.GetHashCode()] = DateTime.UtcNow;
                    _activeConnections++;
                    return connection;
                }

                // Create new connection if under limit
                if (_totalConnections < MAX_POOL_SIZE)
                {
                    connection = CreateConnection();
                    _connectionLastUsed[connection.GetHashCode()] = DateTime.UtcNow;
                    _totalConnections++;
                    _activeConnections++;
                    return connection;
                }

                // Wait for available connection (should be rare)
                throw new InvalidOperationException("Connection pool exhausted. Consider increasing pool size.");
            }
        }

        public void ReturnConnection(SQLiteConnection connection)
        {
            lock (_poolLock)
            {
                if (connection?.State == ConnectionState.Open)
                {
                    _connectionPool.Enqueue(connection);
                    _connectionLastUsed[connection.GetHashCode()] = DateTime.UtcNow;
                }
                _activeConnections--;
            }
        }

        // Batch Operations
        public void QueueVoterUpdate(int voterId, VoterData voterData)
        {
            _voterUpdateQueue.Enqueue(new VoterDataUpdate
            {
                VoterId = voterId,
                VoterData = voterData,
                Timestamp = Time.realtimeSinceStartup
            });

            InvalidateCache(voterId);
            CheckBatchFlush();
        }

        public void QueueOpinionUpdate(int voterId, PoliticalOpinion opinion)
        {
            _opinionUpdateQueue.Enqueue(new OpinionUpdate
            {
                VoterId = voterId,
                Opinion = opinion,
                Timestamp = Time.realtimeSinceStartup
            });

            InvalidateCache(voterId);
            CheckBatchFlush();
        }

        public void QueueBehaviorUpdate(int voterId, BehaviorState behavior)
        {
            _behaviorUpdateQueue.Enqueue(new BehaviorUpdate
            {
                VoterId = voterId,
                Behavior = behavior,
                Timestamp = Time.realtimeSinceStartup
            });

            InvalidateCache(voterId);
            CheckBatchFlush();
        }

        private void CheckBatchFlush()
        {
            var currentTime = Time.realtimeSinceStartup;
            var totalQueued = _voterUpdateQueue.Count + _opinionUpdateQueue.Count + _behaviorUpdateQueue.Count;

            // Flush if batch size reached or timeout elapsed
            if (totalQueued >= DEFAULT_BATCH_SIZE ||
                (totalQueued > 0 && currentTime - _lastBatchFlush > BATCH_TIMEOUT_MS / 1000f))
            {
                FlushBatchOperations();
            }
        }

        public async Task FlushBatchOperationsAsync()
        {
            await Task.Run(() => FlushBatchOperations());
        }

        public void FlushBatchOperations()
        {
            if (!_isInitialized) return;

            var startTime = Time.realtimeSinceStartup;
            var totalOperations = 0;

            try
            {
                using var connection = GetConnection();
                using var transaction = connection.BeginTransaction();

                // Batch voter updates
                totalOperations += ProcessVoterUpdates(connection, transaction);
                totalOperations += ProcessOpinionUpdates(connection, transaction);
                totalOperations += ProcessBehaviorUpdates(connection, transaction);

                transaction.Commit();
                ReturnConnection(connection);

                var duration = (Time.realtimeSinceStartup - startTime) * 1000f;
                _metrics.RecordBatchOperation(totalOperations, duration);

                PerformanceProfiler.RecordMeasurement("DatabaseBatchOperations", totalOperations);
                PerformanceProfiler.RecordMeasurement("DatabaseBatchTime", duration);

                _lastBatchFlush = Time.realtimeSinceStartup;

                if (totalOperations > 0)
                {
                    Debug.Log($"Flushed {totalOperations} database operations in {duration:F2}ms");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Batch operation failed: {ex.Message}");
                _metrics.RecordError();
                throw;
            }
        }

        private int ProcessVoterUpdates(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (_voterUpdateQueue.IsEmpty) return 0;

            var updates = new List<VoterDataUpdate>();
            while (_voterUpdateQueue.TryDequeue(out var update) && updates.Count < MAX_BATCH_SIZE)
            {
                updates.Add(update);
            }

            if (updates.Count == 0) return 0;

            var sql = @"
                INSERT OR REPLACE INTO Voters
                (VoterId, Age, EducationLevel, IncomeLevel, UrbanizationLevel, Province)
                VALUES (@VoterId, @Age, @EducationLevel, @IncomeLevel, @UrbanizationLevel, @Province)";

            using var command = new SQLiteCommand(sql, connection, transaction);
            command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;

            foreach (var update in updates)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@VoterId", update.VoterId);
                command.Parameters.AddWithValue("@Age", update.VoterData.Age);
                command.Parameters.AddWithValue("@EducationLevel", (int)update.VoterData.EducationLevel);
                command.Parameters.AddWithValue("@IncomeLevel", (int)update.VoterData.IncomeLevel);
                command.Parameters.AddWithValue("@UrbanizationLevel", (int)update.VoterData.UrbanizationLevel);
                command.Parameters.AddWithValue("@Province", update.VoterData.Province.ToString());
                command.ExecuteNonQuery();
            }

            return updates.Count;
        }

        private int ProcessOpinionUpdates(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (_opinionUpdateQueue.IsEmpty) return 0;

            var updates = new List<OpinionUpdate>();
            while (_opinionUpdateQueue.TryDequeue(out var update) && updates.Count < MAX_BATCH_SIZE)
            {
                updates.Add(update);
            }

            if (updates.Count == 0) return 0;

            var sql = @"
                INSERT OR REPLACE INTO PoliticalOpinions
                (VoterId, EconomicPosition, SocialPosition, EnvironmentalPosition)
                VALUES (@VoterId, @Economic, @Social, @Environmental)";

            using var command = new SQLiteCommand(sql, connection, transaction);
            command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;

            foreach (var update in updates)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@VoterId", update.VoterId);
                command.Parameters.AddWithValue("@Economic", update.Opinion.EconomicPosition);
                command.Parameters.AddWithValue("@Social", update.Opinion.SocialPosition);
                command.Parameters.AddWithValue("@Environmental", update.Opinion.EnvironmentalPosition);
                command.ExecuteNonQuery();
            }

            return updates.Count;
        }

        private int ProcessBehaviorUpdates(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (_behaviorUpdateQueue.IsEmpty) return 0;

            var updates = new List<BehaviorUpdate>();
            while (_behaviorUpdateQueue.TryDequeue(out var update) && updates.Count < MAX_BATCH_SIZE)
            {
                updates.Add(update);
            }

            if (updates.Count == 0) return 0;

            var sql = @"
                INSERT OR REPLACE INTO BehaviorStates
                (VoterId, Satisfaction, PoliticalEngagement, OpinionVolatility, LastVoteIntention)
                VALUES (@VoterId, @Satisfaction, @Engagement, @Volatility, @VoteIntention)";

            using var command = new SQLiteCommand(sql, connection, transaction);
            command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;

            foreach (var update in updates)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@VoterId", update.VoterId);
                command.Parameters.AddWithValue("@Satisfaction", update.Behavior.Satisfaction);
                command.Parameters.AddWithValue("@Engagement", update.Behavior.PoliticalEngagement);
                command.Parameters.AddWithValue("@Volatility", update.Behavior.OpinionVolatility);
                command.Parameters.AddWithValue("@VoteIntention", update.Behavior.LastVoteIntention?.ToString() ?? "");
                command.ExecuteNonQuery();
            }

            return updates.Count;
        }

        // Cache management
        private void InvalidateCache(int voterId)
        {
            _voterCache.Remove(voterId);
        }

        private void CleanExpiredCache()
        {
            var currentTime = Time.realtimeSinceStartup;
            var expiredKeys = new List<string>();

            foreach (var kvp in _queryCache)
            {
                if (currentTime - kvp.Value.Timestamp > CACHE_TTL_SECONDS)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _queryCache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                Debug.Log($"Cleaned {expiredKeys.Count} expired cache entries");
            }
        }

        // Helper methods
        private static void ExecuteNonQuery(SQLiteConnection connection, string sql)
        {
            using var command = new SQLiteCommand(sql, connection);
            command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;
            command.ExecuteNonQuery();
        }

        public DatabaseMetrics GetMetrics()
        {
            return new DatabaseMetrics
            {
                ActiveConnections = _activeConnections,
                TotalConnections = _totalConnections,
                QueuedVoterUpdates = _voterUpdateQueue.Count,
                QueuedOpinionUpdates = _opinionUpdateQueue.Count,
                QueuedBehaviorUpdates = _behaviorUpdateQueue.Count,
                CacheSize = _voterCache.Count,
                QueryCacheSize = _queryCache.Count,
                AverageBatchTime = _metrics.AverageBatchTime,
                TotalBatchOperations = _metrics.TotalBatchOperations,
                ErrorRate = _metrics.ErrorRate
            };
        }

        public void Update()
        {
            // Perform maintenance operations
            CheckBatchFlush();
            CleanExpiredCache();

            // Clean up idle connections periodically
            if (Time.frameCount % 3600 == 0) // Every minute at 60 FPS
            {
                CleanupIdleConnections();
            }
        }

        private void CleanupIdleConnections()
        {
            // Implementation for cleaning up idle connections
            // This would close connections that have been idle for too long
        }

        public void Dispose()
        {
            FlushBatchOperations();

            while (_connectionPool.TryDequeue(out var connection))
            {
                connection?.Dispose();
            }

            _voterCache.Clear();
            _queryCache.Clear();

            Debug.Log("Optimized database service disposed");
        }

        // Data structures
        private struct VoterDataUpdate
        {
            public int VoterId;
            public VoterData VoterData;
            public float Timestamp;
        }

        private struct OpinionUpdate
        {
            public int VoterId;
            public PoliticalOpinion Opinion;
            public float Timestamp;
        }

        private struct BehaviorUpdate
        {
            public int VoterId;
            public BehaviorState Behavior;
            public float Timestamp;
        }

        private struct CachedVoterData
        {
            public VoterData VoterData;
            public PoliticalOpinion Opinion;
            public BehaviorState Behavior;
            public float Timestamp;
        }

        private struct CachedQueryResult
        {
            public object Result;
            public float Timestamp;
        }

        private class PerformanceMetrics
        {
            private readonly List<float> _batchTimes = new();
            private int _totalBatchOperations = 0;
            private int _errorCount = 0;

            public float AverageBatchTime => _batchTimes.Count > 0 ? _batchTimes.Average() : 0f;
            public int TotalBatchOperations => _totalBatchOperations;
            public float ErrorRate => _totalBatchOperations > 0 ? (float)_errorCount / _totalBatchOperations : 0f;

            public void RecordBatchOperation(int operationCount, float timeMs)
            {
                _batchTimes.Add(timeMs);
                _totalBatchOperations += operationCount;

                // Keep only recent measurements
                if (_batchTimes.Count > 100)
                {
                    _batchTimes.RemoveAt(0);
                }
            }

            public void RecordError()
            {
                _errorCount++;
            }
        }

        public struct DatabaseMetrics
        {
            public int ActiveConnections;
            public int TotalConnections;
            public int QueuedVoterUpdates;
            public int QueuedOpinionUpdates;
            public int QueuedBehaviorUpdates;
            public int CacheSize;
            public int QueryCacheSize;
            public float AverageBatchTime;
            public int TotalBatchOperations;
            public float ErrorRate;
        }
    }
}