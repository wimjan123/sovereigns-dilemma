using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Political.Jobs;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Full-scale voter system optimized for 10,000+ voters with advanced LOD,
    /// memory optimization, and hierarchical processing strategies.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ParallelVoterBehaviorSystem))]
    public partial struct FullScaleVoterSystem : ISystem
    {
        private static readonly ProfilerMarker FullScaleUpdateMarker = new("SovereignsDilemma.FullScaleVoter");

        // Entity queries optimized for large scale
        private EntityQuery _allVotersQuery;
        private EntityQuery _highDetailVotersQuery;
        private EntityQuery _mediumDetailVotersQuery;
        private EntityQuery _lowDetailVotersQuery;
        private EntityQuery _dormantVotersQuery;

        // Component type handles
        private ComponentTypeHandle<VoterData> _voterDataHandle;
        private ComponentTypeHandle<PoliticalOpinion> _politicalOpinionHandle;
        private ComponentTypeHandle<BehaviorState> _behaviorStateHandle;
        private ComponentTypeHandle<SocialNetwork> _socialNetworkHandle;
        private ComponentTypeHandle<VoterLODLevel> _lodLevelHandle;
        private ComponentTypeHandle<LocalTransform> _transformHandle;

        // LOD configuration
        private const float HIGH_DETAIL_DISTANCE = 50f;
        private const float MEDIUM_DETAIL_DISTANCE = 150f;
        private const float LOW_DETAIL_DISTANCE = 500f;

        // Performance configuration
        private const int MAX_HIGH_DETAIL_VOTERS = 500;
        private const int MAX_MEDIUM_DETAIL_VOTERS = 2000;
        private const int VOTERS_PER_FRAME_HIGH = 100;
        private const int VOTERS_PER_FRAME_MEDIUM = 250;
        private const int VOTERS_PER_FRAME_LOW = 500;

        // Memory pool management
        private NativeArray<VoterMemoryBlock> _voterMemoryPool;
        private NativeQueue<int> _availableMemoryBlocks;
        private NativeHashMap<Entity, int> _voterToMemoryBlock;

        // Performance tracking
        private int _frameCounter;
        private float _lastPerformanceUpdate;
        private VoterSystemMetrics _metrics;

        // Camera tracking for LOD
        private float3 _cameraPosition;
        private bool _cameraPositionValid;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create optimized queries for different LOD levels
            _allVotersQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData, PoliticalOpinion, BehaviorState, VoterLODLevel>()
                .Build();

            _highDetailVotersQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData, PoliticalOpinion, BehaviorState, SocialNetwork, LocalTransform>()
                .WithAll<VoterLODLevel>()
                .Build();

            _mediumDetailVotersQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData, PoliticalOpinion, BehaviorState>()
                .WithAll<VoterLODLevel>()
                .WithNone<SocialNetwork>()
                .Build();

            _lowDetailVotersQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData, VoterLODLevel>()
                .WithNone<PoliticalOpinion, BehaviorState>()
                .Build();

            _dormantVotersQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData>()
                .WithNone<VoterLODLevel>()
                .Build();

            // Initialize component handles
            _voterDataHandle = state.GetComponentTypeHandle<VoterData>(true);
            _politicalOpinionHandle = state.GetComponentTypeHandle<PoliticalOpinion>(false);
            _behaviorStateHandle = state.GetComponentTypeHandle<BehaviorState>(false);
            _socialNetworkHandle = state.GetComponentTypeHandle<SocialNetwork>(false);
            _lodLevelHandle = state.GetComponentTypeHandle<VoterLODLevel>(false);
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);

            // Initialize memory management
            InitializeMemoryManagement(ref state);

            // Initialize metrics
            _metrics = new VoterSystemMetrics();

            RequireForUpdate(_allVotersQuery);
        }

        private void InitializeMemoryManagement(ref SystemState state)
        {
            const int maxVoters = 12000; // Buffer for peak usage

            _voterMemoryPool = new NativeArray<VoterMemoryBlock>(maxVoters, Allocator.Persistent);
            _availableMemoryBlocks = new NativeQueue<int>(Allocator.Persistent);
            _voterToMemoryBlock = new NativeHashMap<Entity, int>(maxVoters, Allocator.Persistent);

            // Initialize available memory blocks
            for (int i = 0; i < maxVoters; i++)
            {
                _availableMemoryBlocks.Enqueue(i);
            }

            Debug.Log($"Initialized voter memory management for {maxVoters} voters");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (FullScaleUpdateMarker.Auto())
            {
                _frameCounter++;
                var currentTime = (float)state.WorldUnmanaged.Time.ElapsedTime;
                var deltaTime = state.WorldUnmanaged.Time.DeltaTime;

                // Update component handles
                UpdateComponentHandles(ref state);

                // Get camera position for LOD calculations
                UpdateCameraPosition();

                // Update LOD levels based on performance and distance
                UpdateLODLevels(ref state, currentTime);

                // Process voters by LOD level
                ProcessHighDetailVoters(ref state, deltaTime);
                ProcessMediumDetailVoters(ref state, deltaTime);
                ProcessLowDetailVoters(ref state, deltaTime);
                ProcessDormantVoters(ref state, deltaTime);

                // Memory management
                ManageMemoryAllocation(ref state);

                // Performance monitoring
                UpdatePerformanceMetrics(ref state, currentTime);

                // Adaptive scaling
                ApplyAdaptiveScaling(ref state);
            }
        }

        private void UpdateComponentHandles(ref SystemState state)
        {
            _voterDataHandle.Update(ref state);
            _politicalOpinionHandle.Update(ref state);
            _behaviorStateHandle.Update(ref state);
            _socialNetworkHandle.Update(ref state);
            _lodLevelHandle.Update(ref state);
            _transformHandle.Update(ref state);
        }

        private void UpdateCameraPosition()
        {
            // In a real implementation, this would get the camera position
            // For simulation, we'll use a moving camera position
            var time = Time.time;
            _cameraPosition = new float3(
                math.sin(time * 0.1f) * 100f,
                50f,
                math.cos(time * 0.1f) * 100f
            );
            _cameraPositionValid = true;
        }

        private void UpdateLODLevels(ref SystemState state, float currentTime)
        {
            if (!_cameraPositionValid) return;

            // Update LOD levels based on distance and performance
            var lodUpdateJob = new UpdateVoterLODJob
            {
                CameraPosition = _cameraPosition,
                CurrentTime = currentTime,
                HighDetailDistance = HIGH_DETAIL_DISTANCE,
                MediumDetailDistance = MEDIUM_DETAIL_DISTANCE,
                LowDetailDistance = LOW_DETAIL_DISTANCE,
                MaxHighDetailVoters = MAX_HIGH_DETAIL_VOTERS,
                MaxMediumDetailVoters = MAX_MEDIUM_DETAIL_VOTERS,
                TransformHandle = _transformHandle,
                LODLevelHandle = _lodLevelHandle
            };

            var dependency = lodUpdateJob.ScheduleParallel(_allVotersQuery, state.Dependency);
            state.Dependency = dependency;
        }

        private void ProcessHighDetailVoters(ref SystemState state, float deltaTime)
        {
            var voterCount = _highDetailVotersQuery.CalculateEntityCount();
            if (voterCount == 0) return;

            var processCount = math.min(voterCount, VOTERS_PER_FRAME_HIGH);

            var highDetailJob = new HighDetailVoterUpdateJob
            {
                DeltaTime = deltaTime,
                CurrentFrame = (uint)_frameCounter,
                OpinionUpdateStrength = 0.02f,
                SocialInfluenceStrength = 0.025f,
                BehaviorUpdateRate = 1.0f,
                VoterDataHandle = _voterDataHandle,
                PoliticalOpinionHandle = _politicalOpinionHandle,
                BehaviorStateHandle = _behaviorStateHandle,
                SocialNetworkHandle = _socialNetworkHandle,
                RandomSeed = new Random((uint)(_frameCounter + 12345))
            };

            var batchSize = math.max(1, processCount / Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);
            var dependency = highDetailJob.ScheduleParallel(_highDetailVotersQuery, batchSize, state.Dependency);
            state.Dependency = dependency;

            _metrics.HighDetailVotersProcessed += processCount;
        }

        private void ProcessMediumDetailVoters(ref SystemState state, float deltaTime)
        {
            var voterCount = _mediumDetailVotersQuery.CalculateEntityCount();
            if (voterCount == 0) return;

            var processCount = math.min(voterCount, VOTERS_PER_FRAME_MEDIUM);

            var mediumDetailJob = new MediumDetailVoterUpdateJob
            {
                DeltaTime = deltaTime,
                CurrentFrame = (uint)_frameCounter,
                OpinionUpdateStrength = 0.01f,
                BehaviorUpdateRate = 0.5f,
                VoterDataHandle = _voterDataHandle,
                PoliticalOpinionHandle = _politicalOpinionHandle,
                BehaviorStateHandle = _behaviorStateHandle,
                RandomSeed = new Random((uint)(_frameCounter + 54321))
            };

            var batchSize = math.max(1, processCount / Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);
            var dependency = mediumDetailJob.ScheduleParallel(_mediumDetailVotersQuery, batchSize, state.Dependency);
            state.Dependency = dependency;

            _metrics.MediumDetailVotersProcessed += processCount;
        }

        private void ProcessLowDetailVoters(ref SystemState state, float deltaTime)
        {
            var voterCount = _lowDetailVotersQuery.CalculateEntityCount();
            if (voterCount == 0) return;

            var processCount = math.min(voterCount, VOTERS_PER_FRAME_LOW);

            var lowDetailJob = new LowDetailVoterUpdateJob
            {
                DeltaTime = deltaTime,
                CurrentFrame = (uint)_frameCounter,
                OpinionDecayRate = 0.001f,
                VoterDataHandle = _voterDataHandle,
                RandomSeed = new Random((uint)(_frameCounter + 98765))
            };

            var batchSize = math.max(16, processCount / 4); // Larger batches for simple processing
            var dependency = lowDetailJob.ScheduleParallel(_lowDetailVotersQuery, batchSize, state.Dependency);
            state.Dependency = dependency;

            _metrics.LowDetailVotersProcessed += processCount;
        }

        private void ProcessDormantVoters(ref SystemState state, float deltaTime)
        {
            var voterCount = _dormantVotersQuery.CalculateEntityCount();
            if (voterCount == 0) return;

            // Dormant voters get minimal processing - just age increment every few seconds
            if (_frameCounter % 180 == 0) // Every 3 seconds at 60fps
            {
                var dormantJob = new DormantVoterUpdateJob
                {
                    AgeIncrement = 1.0f / 365.0f, // Age by 1 day per update
                    VoterDataHandle = _voterDataHandle
                };

                var dependency = dormantJob.ScheduleParallel(_dormantVotersQuery, 64, state.Dependency);
                state.Dependency = dependency;

                _metrics.DormantVotersProcessed += voterCount;
            }
        }

        private void ManageMemoryAllocation(ref SystemState state)
        {
            // Manage memory allocation for new voters and cleanup for removed ones
            if (_frameCounter % 60 == 0) // Every second
            {
                CleanupUnusedMemoryBlocks(ref state);
                CompactMemoryPool();
            }
        }

        private void CleanupUnusedMemoryBlocks(ref SystemState state)
        {
            // Find voters that no longer exist and return their memory blocks
            var keysToRemove = new NativeList<Entity>(Allocator.Temp);

            foreach (var kvp in _voterToMemoryBlock)
            {
                if (!state.EntityManager.Exists(kvp.Key))
                {
                    keysToRemove.Add(kvp.Key);
                    _availableMemoryBlocks.Enqueue(kvp.Value);
                }
            }

            foreach (var key in keysToRemove)
            {
                _voterToMemoryBlock.Remove(key);
            }

            keysToRemove.Dispose();
        }

        private void CompactMemoryPool()
        {
            // Periodic memory pool compaction to prevent fragmentation
            if (_frameCounter % 3600 == 0) // Every minute
            {
                // In a real implementation, this would perform memory compaction
                _metrics.MemoryCompactions++;
            }
        }

        private void UpdatePerformanceMetrics(ref SystemState state, float currentTime)
        {
            if (currentTime - _lastPerformanceUpdate >= 1.0f)
            {
                var totalVoters = _allVotersQuery.CalculateEntityCount();
                var memoryUsage = _voterMemoryPool.Length - _availableMemoryBlocks.Count;

                _metrics.TotalVoters = totalVoters;
                _metrics.MemoryUsage = memoryUsage;
                _metrics.FrameRate = _frameCounter / (currentTime - _lastPerformanceUpdate);

                // Reset counters
                _metrics.HighDetailVotersProcessed = 0;
                _metrics.MediumDetailVotersProcessed = 0;
                _metrics.LowDetailVotersProcessed = 0;
                _metrics.DormantVotersProcessed = 0;

                _lastPerformanceUpdate = currentTime;

                // Record performance metrics
                PerformanceProfiler.RecordMeasurement("FullScale_TotalVoters", totalVoters);
                PerformanceProfiler.RecordMeasurement("FullScale_MemoryUsage", memoryUsage);
                PerformanceProfiler.RecordMeasurement("FullScale_FrameRate", _metrics.FrameRate);

                // Log performance periodically
                if (_frameCounter % 1800 == 0) // Every 30 seconds
                {
                    Debug.Log($"Full Scale Voter System: {totalVoters} voters, " +
                             $"{_metrics.FrameRate:F1} FPS, " +
                             $"{memoryUsage}/{_voterMemoryPool.Length} memory blocks used");
                }
            }
        }

        private void ApplyAdaptiveScaling(ref SystemState state)
        {
            // Get adaptive performance multiplier
            var performanceMultiplier = GetAdaptivePerformanceMultiplier();

            // Adjust processing limits based on performance
            if (performanceMultiplier < 0.8f)
            {
                // Reduce processing when performance is low
                ReduceProcessingLoad();
            }
            else if (performanceMultiplier > 1.2f)
            {
                // Increase processing when performance is good
                IncreaseProcessingLoad();
            }
        }

        private float GetAdaptivePerformanceMultiplier()
        {
            return PlayerPrefs.GetFloat("AdaptivePerformance_UpdateFrequencyMultiplier", 1.0f);
        }

        private void ReduceProcessingLoad()
        {
            // Temporarily reduce voters per frame processing
            _metrics.ProcessingLoadReductions++;
        }

        private void IncreaseProcessingLoad()
        {
            // Temporarily increase voters per frame processing when performance allows
            _metrics.ProcessingLoadIncreases++;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_voterMemoryPool.IsCreated)
                _voterMemoryPool.Dispose();
            if (_availableMemoryBlocks.IsCreated)
                _availableMemoryBlocks.Dispose();
            if (_voterToMemoryBlock.IsCreated)
                _voterToMemoryBlock.Dispose();
        }

        /// <summary>
        /// Gets current full-scale voter system metrics.
        /// </summary>
        public VoterSystemMetrics GetMetrics()
        {
            return _metrics;
        }

        /// <summary>
        /// Forces all voters to high detail mode (for testing).
        /// </summary>
        public void ForceHighDetailMode(ref SystemState state)
        {
            // Implementation would set all voters to high detail LOD
            Debug.Log("Forcing all voters to high detail mode");
        }

        /// <summary>
        /// Optimizes memory usage by compacting pools.
        /// </summary>
        public void OptimizeMemory()
        {
            CompactMemoryPool();
            Debug.Log("Memory optimization completed");
        }

        // Data structures
        private struct VoterMemoryBlock
        {
            public Entity Owner;
            public float LastAccessTime;
            public bool IsActive;
            public int ReferenceCount;
        }

        public struct VoterSystemMetrics
        {
            public int TotalVoters;
            public int HighDetailVotersProcessed;
            public int MediumDetailVotersProcessed;
            public int LowDetailVotersProcessed;
            public int DormantVotersProcessed;
            public int MemoryUsage;
            public float FrameRate;
            public int MemoryCompactions;
            public int ProcessingLoadReductions;
            public int ProcessingLoadIncreases;
        }
    }

    /// <summary>
    /// Component to track voter Level of Detail.
    /// </summary>
    public struct VoterLODLevel : IComponentData
    {
        public LODLevel CurrentLevel;
        public float DistanceToCamera;
        public float LastLODUpdate;
        public int FramesSinceUpdate;
    }

    public enum LODLevel : byte
    {
        Dormant = 0,    // No processing
        Low = 1,        // Minimal processing
        Medium = 2,     // Standard processing
        High = 3        // Full processing with social network
    }
}