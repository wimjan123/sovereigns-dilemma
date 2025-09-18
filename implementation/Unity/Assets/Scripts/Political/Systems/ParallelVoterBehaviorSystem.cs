using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Political.Jobs;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// High-performance parallel voter behavior system using Unity Jobs System.
    /// Processes 5,000+ voters efficiently with Burst compilation and parallel execution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoterBehaviorSystem))]
    public partial struct ParallelVoterBehaviorSystem : ISystem
    {
        private static readonly ProfilerMarker VoterParallelUpdateMarker = new("SovereignsDilemma.VoterParallelUpdate");

        private EntityQuery _activeVotersQuery;
        private ComponentTypeHandle<VoterData> _voterDataTypeHandle;
        private ComponentTypeHandle<PoliticalOpinion> _politicalOpinionTypeHandle;
        private ComponentTypeHandle<BehaviorState> _behaviorStateTypeHandle;
        private ComponentTypeHandle<SocialNetwork> _socialNetworkTypeHandle;
        private ComponentTypeHandle<EventResponse> _eventResponseTypeHandle;
        private ComponentTypeHandle<AIAnalysisCache> _aiAnalysisCacheTypeHandle;

        // Performance tracking
        private int _lastProcessedVoterCount;
        private double _lastUpdateTime;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create query for active voters only
            _activeVotersQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData, PoliticalOpinion, BehaviorState, SocialNetwork, EventResponse, AIAnalysisCache>()
                .WithNone<AIAnalysisInProgress>() // Exclude voters currently being analyzed by AI
                .Build();

            // Get component type handles
            _voterDataTypeHandle = state.GetComponentTypeHandle<VoterData>(true);
            _politicalOpinionTypeHandle = state.GetComponentTypeHandle<PoliticalOpinion>(false);
            _behaviorStateTypeHandle = state.GetComponentTypeHandle<BehaviorState>(false);
            _socialNetworkTypeHandle = state.GetComponentTypeHandle<SocialNetwork>(false);
            _eventResponseTypeHandle = state.GetComponentTypeHandle<EventResponse>(true);
            _aiAnalysisCacheTypeHandle = state.GetComponentTypeHandle<AIAnalysisCache>(false);

            RequireForUpdate(_activeVotersQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (VoterParallelUpdateMarker.Auto())
            {
                var voterCount = _activeVotersQuery.CalculateEntityCount();

                // Skip update if no voters or system is overloaded
                if (voterCount == 0)
                    return;

                // Performance-based update frequency scaling
                var targetFrameTime = GetTargetFrameTime(voterCount);
                if (_lastUpdateTime > 0 && Time.ElapsedTime - _lastUpdateTime < targetFrameTime)
                    return;

                // Update component type handles
                _voterDataTypeHandle.Update(ref state);
                _politicalOpinionTypeHandle.Update(ref state);
                _behaviorStateTypeHandle.Update(ref state);
                _socialNetworkTypeHandle.Update(ref state);
                _eventResponseTypeHandle.Update(ref state);
                _aiAnalysisCacheTypeHandle.Update(ref state);

                var currentFrame = (uint)UnityEngine.Time.frameCount;
                var deltaTime = state.WorldUnmanaged.Time.DeltaTime;

                // Create random seed for this frame
                var randomSeed = new Random((uint)(currentFrame + 1));

                // Schedule parallel jobs for different aspects of voter behavior
                var dependency = state.Dependency;

                // Job 1: Update core voter behavior (opinions, emotions, traits)
                var behaviorJob = new VoterBehaviorUpdateJob
                {
                    VoterDataArray = _activeVotersQuery.ToComponentDataArray<VoterData>(Allocator.TempJob),
                    PoliticalOpinionArray = _activeVotersQuery.ToComponentDataArray<PoliticalOpinion>(Allocator.TempJob),
                    BehaviorStateArray = _activeVotersQuery.ToComponentDataArray<BehaviorState>(Allocator.TempJob),
                    SocialNetworkArray = _activeVotersQuery.ToComponentDataArray<SocialNetwork>(Allocator.TempJob),
                    EventResponseArray = _activeVotersQuery.ToComponentDataArray<EventResponse>(Allocator.TempJob),
                    AIAnalysisCacheArray = _activeVotersQuery.ToComponentDataArray<AIAnalysisCache>(Allocator.TempJob),
                    CurrentFrame = currentFrame,
                    DeltaTime = deltaTime,
                    OpinionDecayRate = 0.002f, // Slightly faster decay for large populations
                    SocialInfluenceStrength = 0.015f, // Increased social influence
                    RandomSeed = randomSeed
                };

                // Calculate optimal batch size based on voter count and system performance
                var batchSize = CalculateOptimalBatchSize(voterCount);
                var behaviorJobHandle = behaviorJob.ScheduleParallel(voterCount, batchSize, dependency);

                // Job 2: Update social networks (can run in parallel with behavior updates)
                var networkJob = new SocialNetworkUpdateJob
                {
                    VoterDataArray = behaviorJob.VoterDataArray,
                    PoliticalOpinionArray = behaviorJob.PoliticalOpinionArray,
                    BehaviorStateArray = behaviorJob.BehaviorStateArray,
                    SocialNetworkArray = behaviorJob.SocialNetworkArray,
                    CurrentFrame = currentFrame,
                    DeltaTime = deltaTime,
                    RandomSeed = new Random(randomSeed.state + 54321)
                };

                var networkJobHandle = networkJob.ScheduleParallel(voterCount, batchSize, dependency);

                // Wait for both jobs to complete
                var combinedHandle = JobHandle.CombineDependencies(behaviorJobHandle, networkJobHandle);

                // Copy results back to component arrays
                var copyBackJob = new CopyBackToComponentsJob
                {
                    PoliticalOpinionArray = behaviorJob.PoliticalOpinionArray,
                    BehaviorStateArray = behaviorJob.BehaviorStateArray,
                    SocialNetworkArray = behaviorJob.SocialNetworkArray,
                    AIAnalysisCacheArray = behaviorJob.AIAnalysisCacheArray,
                    PoliticalOpinionTypeHandle = _politicalOpinionTypeHandle,
                    BehaviorStateTypeHandle = _behaviorStateTypeHandle,
                    SocialNetworkTypeHandle = _socialNetworkTypeHandle,
                    AIAnalysisCacheTypeHandle = _aiAnalysisCacheTypeHandle
                };

                var copyBackHandle = copyBackJob.ScheduleParallel(_activeVotersQuery, combinedHandle);

                // Schedule cleanup job to dispose temporary arrays
                var cleanupJob = new CleanupArraysJob
                {
                    VoterDataArray = behaviorJob.VoterDataArray,
                    PoliticalOpinionArray = behaviorJob.PoliticalOpinionArray,
                    BehaviorStateArray = behaviorJob.BehaviorStateArray,
                    SocialNetworkArray = behaviorJob.SocialNetworkArray,
                    EventResponseArray = behaviorJob.EventResponseArray,
                    AIAnalysisCacheArray = behaviorJob.AIAnalysisCacheArray
                };

                var cleanupHandle = cleanupJob.Schedule(copyBackHandle);

                state.Dependency = cleanupHandle;

                // Track performance metrics
                _lastProcessedVoterCount = voterCount;
                _lastUpdateTime = Time.ElapsedTime;

                // Record performance metrics
                PerformanceProfiler.RecordMeasurement("ParallelVoterUpdate", (float)(Time.ElapsedTime - _lastUpdateTime) * 1000f);
                PerformanceProfiler.RecordMeasurement("ProcessedVoterCount", voterCount);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Cleanup is handled by the scheduled cleanup job
        }

        /// <summary>
        /// Calculates optimal batch size based on voter count and system performance.
        /// </summary>
        private int CalculateOptimalBatchSize(int voterCount)
        {
            // Base batch size on CPU core count and voter density
            var coreCount = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
            var baseBatchSize = math.max(32, voterCount / (coreCount * 4));

            // Adjust based on system performance
            if (_lastUpdateTime > 0.020f) // If last update took >20ms
            {
                baseBatchSize *= 2; // Larger batches to reduce overhead
            }
            else if (_lastUpdateTime < 0.008f) // If last update was very fast
            {
                baseBatchSize = math.max(16, baseBatchSize / 2); // Smaller batches for better parallelization
            }

            return math.clamp(baseBatchSize, 16, 256);
        }

        /// <summary>
        /// Gets target frame time based on voter count for performance scaling.
        /// </summary>
        private float GetTargetFrameTime(int voterCount)
        {
            if (voterCount < 1000)
                return 0.016f; // 60 FPS for small populations
            else if (voterCount < 5000)
                return 0.020f; // 50 FPS for medium populations
            else if (voterCount < 10000)
                return 0.033f; // 30 FPS for large populations
            else
                return 0.050f; // 20 FPS for very large populations
        }
    }

    /// <summary>
    /// Job to copy processed data back to ECS components.
    /// </summary>
    [BurstCompile]
    public struct CopyBackToComponentsJob : IJobChunk
    {
        [ReadOnly] public NativeArray<PoliticalOpinion> PoliticalOpinionArray;
        [ReadOnly] public NativeArray<BehaviorState> BehaviorStateArray;
        [ReadOnly] public NativeArray<SocialNetwork> SocialNetworkArray;
        [ReadOnly] public NativeArray<AIAnalysisCache> AIAnalysisCacheArray;

        public ComponentTypeHandle<PoliticalOpinion> PoliticalOpinionTypeHandle;
        public ComponentTypeHandle<BehaviorState> BehaviorStateTypeHandle;
        public ComponentTypeHandle<SocialNetwork> SocialNetworkTypeHandle;
        public ComponentTypeHandle<AIAnalysisCache> AIAnalysisCacheTypeHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var politicalOpinions = chunk.GetNativeArray(ref PoliticalOpinionTypeHandle);
            var behaviorStates = chunk.GetNativeArray(ref BehaviorStateTypeHandle);
            var socialNetworks = chunk.GetNativeArray(ref SocialNetworkTypeHandle);
            var aiCaches = chunk.GetNativeArray(ref AIAnalysisCacheTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var globalIndex = unfilteredChunkIndex * chunk.Capacity + i;

                if (globalIndex < PoliticalOpinionArray.Length)
                {
                    politicalOpinions[i] = PoliticalOpinionArray[globalIndex];
                    behaviorStates[i] = BehaviorStateArray[globalIndex];
                    socialNetworks[i] = SocialNetworkArray[globalIndex];
                    aiCaches[i] = AIAnalysisCacheArray[globalIndex];
                }
            }
        }
    }

    /// <summary>
    /// Job to dispose temporary arrays after processing.
    /// </summary>
    [BurstCompile]
    public struct CleanupArraysJob : IJob
    {
        [DeallocateOnJobCompletion] public NativeArray<VoterData> VoterDataArray;
        [DeallocateOnJobCompletion] public NativeArray<PoliticalOpinion> PoliticalOpinionArray;
        [DeallocateOnJobCompletion] public NativeArray<BehaviorState> BehaviorStateArray;
        [DeallocateOnJobCompletion] public NativeArray<SocialNetwork> SocialNetworkArray;
        [DeallocateOnJobCompletion] public NativeArray<EventResponse> EventResponseArray;
        [DeallocateOnJobCompletion] public NativeArray<AIAnalysisCache> AIAnalysisCacheArray;

        public void Execute()
        {
            // Arrays are automatically disposed due to [DeallocateOnJobCompletion] attribute
            // This job just ensures proper scheduling and dependency handling
        }
    }

    /// <summary>
    /// System for monitoring and adapting performance of parallel voter processing.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ParallelVoterBehaviorSystem))]
    public partial class VoterPerformanceAdaptationSystem : SystemBase
    {
        private EntityQuery _voterQuery;
        private float _lastFrameTime;
        private int _lastVoterCount;
        private bool _performanceWarningIssued;

        protected override void OnCreate()
        {
            _voterQuery = GetEntityQuery(typeof(VoterData));
        }

        protected override void OnUpdate()
        {
            var currentVoterCount = _voterQuery.CalculateEntityCount();
            var currentFrameTime = Time.DeltaTime;

            // Monitor performance and adjust if needed
            if (currentFrameTime > 0.033f && !_performanceWarningIssued) // >30 FPS
            {
                Debug.LogWarning($"Performance warning: Frame time {currentFrameTime * 1000:F1}ms with {currentVoterCount} voters");
                _performanceWarningIssued = true;

                // Could trigger adaptive measures here:
                // - Reduce voter count
                // - Decrease update frequency
                // - Simplify calculations
            }
            else if (currentFrameTime < 0.020f && _performanceWarningIssued) // <50 FPS
            {
                _performanceWarningIssued = false;
            }

            // Log performance statistics periodically
            if (UnityEngine.Time.frameCount % 300 == 0) // Every 5 seconds at 60 FPS
            {
                var stats = PerformanceProfiler.GetStats("ParallelVoterUpdate");
                if (stats.SampleCount > 0)
                {
                    Debug.Log($"Parallel voter system: {currentVoterCount} voters, avg {stats.AverageTimeMs:F1}ms, max {stats.MaxTimeMs:F1}ms");
                }
            }

            _lastFrameTime = currentFrameTime;
            _lastVoterCount = currentVoterCount;
        }
    }
}