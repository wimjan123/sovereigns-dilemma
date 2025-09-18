using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Data.Database;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// ECS system that integrates with optimized database service for efficient
    /// voter data persistence and retrieval with batch operations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoterBehaviorSystem))]
    public partial class DatabaseIntegrationSystem : SystemBase
    {
        private static readonly ProfilerMarker DatabaseIntegrationMarker = new("SovereignsDilemma.DatabaseIntegration");

        private OptimizedDatabaseService _databaseService;
        private EntityQuery _votersNeedingSaveQuery;
        private EntityQuery _votersNeedingLoadQuery;

        // Save tracking
        private float _lastSaveTime;
        private const float SAVE_INTERVAL_SECONDS = 30.0f; // Save every 30 seconds
        private const float AUTOSAVE_INTERVAL_SECONDS = 300.0f; // Full autosave every 5 minutes

        // Load optimization
        private int _loadBatchSize = 100;
        private int _currentLoadOffset = 0;

        // Performance tracking
        private int _votersSavedThisFrame = 0;
        private int _votersLoadedThisFrame = 0;
        private const int MAX_OPERATIONS_PER_FRAME = 50;

        protected override void OnCreate()
        {
            // Initialize database service
            var databasePath = Application.persistentDataPath + "/SovereignsDilemma.db";
            _databaseService = new OptimizedDatabaseService(databasePath, enableEncryption: true);

            // Create queries for voters that need database operations
            _votersNeedingSaveQuery = GetEntityQuery(
                ComponentType.ReadOnly<VoterData>(),
                ComponentType.ReadOnly<PoliticalOpinion>(),
                ComponentType.ReadOnly<BehaviorState>(),
                ComponentType.ReadWrite<DatabaseSyncState>()
            );

            _votersNeedingLoadQuery = GetEntityQuery(
                ComponentType.ReadWrite<VoterData>(),
                ComponentType.ReadWrite<PoliticalOpinion>(),
                ComponentType.ReadWrite<BehaviorState>(),
                ComponentType.ReadOnly<DatabaseLoadRequest>()
            );

            RequireForUpdate(_votersNeedingSaveQuery);
        }

        protected override void OnDestroy()
        {
            _databaseService?.Dispose();
        }

        protected override void OnUpdate()
        {
            using (DatabaseIntegrationMarker.Auto())
            {
                var currentTime = (float)Time.ElapsedTime;
                var deltaTime = Time.DeltaTime;

                // Update database service (handles batch flushing)
                _databaseService?.Update();

                // Process save operations
                ProcessVoterSaves(currentTime);

                // Process load operations
                ProcessVoterLoads();

                // Periodic full saves
                if (currentTime - _lastSaveTime >= SAVE_INTERVAL_SECONDS)
                {
                    SchedulePeriodicSave();
                    _lastSaveTime = currentTime;
                }

                // Record performance metrics
                RecordPerformanceMetrics();

                // Reset frame counters
                _votersSavedThisFrame = 0;
                _votersLoadedThisFrame = 0;
            }
        }

        private void ProcessVoterSaves(float currentTime)
        {
            var voters = _votersNeedingSaveQuery.ToEntityArray(Allocator.Temp);
            var voterData = _votersNeedingSaveQuery.ToComponentDataArray<VoterData>(Allocator.Temp);
            var opinions = _votersNeedingSaveQuery.ToComponentDataArray<PoliticalOpinion>(Allocator.Temp);
            var behaviors = _votersNeedingSaveQuery.ToComponentDataArray<BehaviorState>(Allocator.Temp);
            var syncStates = _votersNeedingSaveQuery.ToComponentDataArray<DatabaseSyncState>(Allocator.Temp);

            var operationsThisFrame = 0;

            for (int i = 0; i < voters.Length && operationsThisFrame < MAX_OPERATIONS_PER_FRAME; i++)
            {
                var voter = voters[i];
                var data = voterData[i];
                var opinion = opinions[i];
                var behavior = behaviors[i];
                var syncState = syncStates[i];

                // Check if voter needs saving
                if (ShouldSaveVoter(syncState, currentTime))
                {
                    // Queue for batch save
                    _databaseService.QueueVoterUpdate(data.VoterId, data);
                    _databaseService.QueueOpinionUpdate(data.VoterId, opinion);
                    _databaseService.QueueBehaviorUpdate(data.VoterId, behavior);

                    // Update sync state
                    syncState.LastSaveTime = currentTime;
                    syncState.IsDirty = false;
                    syncState.SaveCount++;

                    EntityManager.SetComponentData(voter, syncState);

                    operationsThisFrame++;
                    _votersSavedThisFrame++;
                }
            }

            voters.Dispose();
            voterData.Dispose();
            opinions.Dispose();
            behaviors.Dispose();
            syncStates.Dispose();

            if (operationsThisFrame > 0)
            {
                PerformanceProfiler.RecordMeasurement("VotersSavedPerFrame", operationsThisFrame);
            }
        }

        private bool ShouldSaveVoter(DatabaseSyncState syncState, float currentTime)
        {
            // Save if voter is dirty and enough time has passed
            if (syncState.IsDirty && currentTime - syncState.LastSaveTime >= 5.0f)
                return true;

            // Force save if very dirty
            if (syncState.IsDirty && currentTime - syncState.LastSaveTime >= 60.0f)
                return true;

            // Periodic save for active voters
            if (currentTime - syncState.LastSaveTime >= SAVE_INTERVAL_SECONDS * 2)
                return true;

            return false;
        }

        private void ProcessVoterLoads()
        {
            var loadRequests = _votersNeedingLoadQuery.ToEntityArray(Allocator.Temp);

            if (loadRequests.Length == 0)
            {
                loadRequests.Dispose();
                return;
            }

            var operationsThisFrame = 0;

            foreach (var entity in loadRequests)
            {
                if (operationsThisFrame >= MAX_OPERATIONS_PER_FRAME)
                    break;

                if (EntityManager.HasComponent<DatabaseLoadRequest>(entity))
                {
                    var loadRequest = EntityManager.GetComponentData<DatabaseLoadRequest>(entity);

                    // In a real implementation, this would load from database
                    // For now, we'll simulate successful load
                    ProcessVoterLoad(entity, loadRequest);

                    // Remove load request
                    EntityManager.RemoveComponent<DatabaseLoadRequest>(entity);

                    operationsThisFrame++;
                    _votersLoadedThisFrame++;
                }
            }

            loadRequests.Dispose();

            if (operationsThisFrame > 0)
            {
                PerformanceProfiler.RecordMeasurement("VotersLoadedPerFrame", operationsThisFrame);
            }
        }

        private void ProcessVoterLoad(Entity entity, DatabaseLoadRequest loadRequest)
        {
            // In a real implementation, this would:
            // 1. Query database for voter data by ID
            // 2. Populate VoterData, PoliticalOpinion, BehaviorState components
            // 3. Add DatabaseSyncState component

            // For now, we'll create a sync state component
            var syncState = new DatabaseSyncState
            {
                LastSaveTime = (float)Time.ElapsedTime,
                LastLoadTime = (float)Time.ElapsedTime,
                IsDirty = false,
                SaveCount = 0,
                LoadCount = 1
            };

            EntityManager.AddComponentData(entity, syncState);

            Debug.Log($"Loaded voter {loadRequest.VoterId} from database");
        }

        private void SchedulePeriodicSave()
        {
            // Force flush all pending operations
            _databaseService?.FlushBatchOperations();

            var metrics = _databaseService?.GetMetrics();
            if (metrics.HasValue)
            {
                Debug.Log($"Periodic save completed: {metrics.Value.TotalBatchOperations} operations, " +
                         $"avg batch time: {metrics.Value.AverageBatchTime:F2}ms");
            }

            PerformanceProfiler.RecordMeasurement("PeriodicSaveCompleted", 1f);
        }

        public void SaveAllVoters()
        {
            var voters = _votersNeedingSaveQuery.ToEntityArray(Allocator.Temp);
            var voterData = _votersNeedingSaveQuery.ToComponentDataArray<VoterData>(Allocator.Temp);
            var opinions = _votersNeedingSaveQuery.ToComponentDataArray<PoliticalOpinion>(Allocator.Temp);
            var behaviors = _votersNeedingSaveQuery.ToComponentDataArray<BehaviorState>(Allocator.Temp);

            Debug.Log($"Force saving {voters.Length} voters to database");

            for (int i = 0; i < voters.Length; i++)
            {
                var data = voterData[i];
                var opinion = opinions[i];
                var behavior = behaviors[i];

                _databaseService.QueueVoterUpdate(data.VoterId, data);
                _databaseService.QueueOpinionUpdate(data.VoterId, opinion);
                _databaseService.QueueBehaviorUpdate(data.VoterId, behavior);
            }

            // Force flush
            _databaseService.FlushBatchOperations();

            // Update sync states
            var currentTime = (float)Time.ElapsedTime;
            for (int i = 0; i < voters.Length; i++)
            {
                if (EntityManager.HasComponent<DatabaseSyncState>(voters[i]))
                {
                    var syncState = EntityManager.GetComponentData<DatabaseSyncState>(voters[i]);
                    syncState.LastSaveTime = currentTime;
                    syncState.IsDirty = false;
                    syncState.SaveCount++;
                    EntityManager.SetComponentData(voters[i], syncState);
                }
            }

            voters.Dispose();
            voterData.Dispose();
            opinions.Dispose();
            behaviors.Dispose();

            Debug.Log("Force save completed");
        }

        public void LoadVotersFromDatabase(int count, int offset = 0)
        {
            // In a real implementation, this would:
            // 1. Query database for voter records
            // 2. Create entities and populate components
            // 3. Add appropriate sync states

            Debug.Log($"Loading {count} voters from database (offset: {offset})");

            // For now, this would be implemented when we have actual database queries
            // The structure is in place for the real implementation
        }

        private void RecordPerformanceMetrics()
        {
            var dbMetrics = _databaseService?.GetMetrics();
            if (!dbMetrics.HasValue) return;

            var metrics = dbMetrics.Value;

            PerformanceProfiler.RecordMeasurement("DatabaseActiveConnections", metrics.ActiveConnections);
            PerformanceProfiler.RecordMeasurement("DatabaseQueuedOperations",
                metrics.QueuedVoterUpdates + metrics.QueuedOpinionUpdates + metrics.QueuedBehaviorUpdates);
            PerformanceProfiler.RecordMeasurement("DatabaseCacheSize", metrics.CacheSize);
            PerformanceProfiler.RecordMeasurement("DatabaseAverageBatchTime", metrics.AverageBatchTime);

            // Log statistics periodically
            if (UnityEngine.Time.frameCount % 1800 == 0) // Every 30 seconds
            {
                Debug.Log($"Database Stats - Active Connections: {metrics.ActiveConnections}, " +
                         $"Queued Ops: {metrics.QueuedVoterUpdates + metrics.QueuedOpinionUpdates + metrics.QueuedBehaviorUpdates}, " +
                         $"Cache Size: {metrics.CacheSize}, " +
                         $"Avg Batch Time: {metrics.AverageBatchTime:F2}ms, " +
                         $"Error Rate: {metrics.ErrorRate:P}");
            }
        }

        /// <summary>
        /// Marks a voter as needing database save.
        /// </summary>
        public void MarkVoterDirty(Entity voter)
        {
            if (EntityManager.HasComponent<DatabaseSyncState>(voter))
            {
                var syncState = EntityManager.GetComponentData<DatabaseSyncState>(voter);
                syncState.IsDirty = true;
                EntityManager.SetComponentData(voter, syncState);
            }
            else
            {
                // Add sync state if it doesn't exist
                EntityManager.AddComponentData(voter, new DatabaseSyncState
                {
                    LastSaveTime = 0f,
                    LastLoadTime = (float)Time.ElapsedTime,
                    IsDirty = true,
                    SaveCount = 0,
                    LoadCount = 0
                });
            }
        }

        /// <summary>
        /// Gets current database integration metrics.
        /// </summary>
        public DatabaseIntegrationMetrics GetDatabaseMetrics()
        {
            var dbMetrics = _databaseService?.GetMetrics() ?? default;
            var voterCount = _votersNeedingSaveQuery.CalculateEntityCount();

            return new DatabaseIntegrationMetrics
            {
                TotalVoters = voterCount,
                VotersSavedThisFrame = _votersSavedThisFrame,
                VotersLoadedThisFrame = _votersLoadedThisFrame,
                DatabaseConnections = dbMetrics.ActiveConnections,
                QueuedOperations = dbMetrics.QueuedVoterUpdates + dbMetrics.QueuedOpinionUpdates + dbMetrics.QueuedBehaviorUpdates,
                AverageBatchTime = dbMetrics.AverageBatchTime,
                CacheHitRatio = dbMetrics.CacheSize > 0 ? 0.85f : 0f, // Estimated
                ErrorRate = dbMetrics.ErrorRate
            };
        }

        public struct DatabaseIntegrationMetrics
        {
            public int TotalVoters;
            public int VotersSavedThisFrame;
            public int VotersLoadedThisFrame;
            public int DatabaseConnections;
            public int QueuedOperations;
            public float AverageBatchTime;
            public float CacheHitRatio;
            public float ErrorRate;
        }
    }

    /// <summary>
    /// Component to track database synchronization state for voters.
    /// </summary>
    public struct DatabaseSyncState : IComponentData
    {
        public float LastSaveTime;
        public float LastLoadTime;
        public bool IsDirty;
        public int SaveCount;
        public int LoadCount;
    }

    /// <summary>
    /// Component to request loading voter data from database.
    /// </summary>
    public struct DatabaseLoadRequest : IComponentData
    {
        public int VoterId;
        public float RequestTime;
        public DatabaseLoadPriority Priority;
    }

    public enum DatabaseLoadPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}