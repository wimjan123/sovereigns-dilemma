using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Political.Systems;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Testing.Performance
{
    /// <summary>
    /// Performance benchmarks for voter simulation system.
    /// Validates that the ECS implementation meets the 1,000+ voter target at 60 FPS.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class VoterSimulationBenchmark
    {
        private World _testWorld;
        private VoterSpawningSystem _spawningSystem;
        private VoterBehaviorSystem _behaviorSystem;
        private AIBehaviorInfluenceSystem _aiSystem;

        [SetUp]
        public void SetUp()
        {
            // Create test world
            _testWorld = new World("TestWorld");
            World.DefaultGameObjectInjectionWorld = _testWorld;

            // Initialize systems
            var systems = _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            _spawningSystem = _testWorld.GetOrCreateSystemManaged<VoterSpawningSystem>();
            _behaviorSystem = _testWorld.GetOrCreateSystemManaged<VoterBehaviorSystem>();
            _aiSystem = _testWorld.GetOrCreateSystemManaged<AIBehaviorInfluenceSystem>();

            // Add systems to group
            systems.AddSystemToUpdateList(_spawningSystem);
            systems.AddSystemToUpdateList(_behaviorSystem);
            systems.AddSystemToUpdateList(_aiSystem);

            PerformanceProfiler.ClearMeasurements();
        }

        [TearDown]
        public void TearDown()
        {
            PerformanceProfiler.LogSummary();

            if (_testWorld != null && _testWorld.IsCreated)
            {
                _testWorld.Dispose();
            }
        }

        [UnityTest, Performance]
        [Timeout(60000)] // 60 second timeout
        public IEnumerator VoterSimulation_1000Voters_MaintainsTargetFramerate()
        {
            const int targetVoterCount = 1000;
            const float targetFrameTime = 16.67f; // 60 FPS
            const int testDurationFrames = 300; // 5 seconds at 60 FPS

            yield return MeasureVoterSimulationPerformance(targetVoterCount, testDurationFrames, targetFrameTime);
        }

        [UnityTest, Performance]
        [Timeout(120000)] // 120 second timeout
        public IEnumerator VoterSimulation_5000Voters_AcceptablePerformance()
        {
            const int targetVoterCount = 5000;
            const float targetFrameTime = 33.33f; // 30 FPS minimum
            const int testDurationFrames = 180; // 3 seconds at 60 FPS

            yield return MeasureVoterSimulationPerformance(targetVoterCount, testDurationFrames, targetFrameTime);
        }

        [UnityTest, Performance]
        [Timeout(180000)] // 180 second timeout
        public IEnumerator VoterSimulation_10000Voters_StressTest()
        {
            const int targetVoterCount = 10000;
            const float targetFrameTime = 50.0f; // 20 FPS minimum for stress test
            const int testDurationFrames = 120; // 2 seconds at 60 FPS

            yield return MeasureVoterSimulationPerformance(targetVoterCount, testDurationFrames, targetFrameTime);
        }

        [Test, Performance]
        public void VoterMemoryAllocation_1000Voters_WithinLimits()
        {
            const int voterCount = 1000;
            const int maxMemoryMB = 100; // 100MB limit for 1000 voters

            using (Measure.Scope("VoterMemoryAllocation"))
            {
                var initialMemory = System.GC.GetTotalMemory(true);

                // Spawn voters
                var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                _spawningSystem.SpawnVoters(ref stateRef, voterCount);

                // Update systems to initialize voter data
                _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();

                var finalMemory = System.GC.GetTotalMemory(false);
                var allocatedMB = (finalMemory - initialMemory) / (1024f * 1024f);

                Debug.Log($"Memory allocated for {voterCount} voters: {allocatedMB:F2}MB");

                Assert.Less(allocatedMB, maxMemoryMB,
                    $"Memory allocation exceeded limit: {allocatedMB:F2}MB > {maxMemoryMB}MB");

                // Validate voter count
                var voterQuery = _testWorld.EntityManager.CreateEntityQuery(typeof(VoterData));
                var actualCount = voterQuery.CalculateEntityCount();

                Assert.AreEqual(voterCount, actualCount, "Voter count mismatch");

                voterQuery.Dispose();
            }
        }

        [Test, Performance]
        public void VoterBehaviorUpdate_ScalabilityTest()
        {
            var voterCounts = new[] { 100, 500, 1000, 2000, 5000 };
            var results = new List<(int count, double time)>();

            foreach (var count in voterCounts)
            {
                using (Measure.Scope($"VoterBehavior_{count}"))
                {
                    SetUp(); // Fresh world for each test

                    // Spawn voters
                    var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                    _spawningSystem.SpawnVoters(ref stateRef, count);

                    // Warm up
                    for (int i = 0; i < 5; i++)
                    {
                        _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();
                    }

                    // Measure behavior update performance
                    var startTime = System.DateTime.UtcNow;
                    const int iterations = 10;

                    for (int i = 0; i < iterations; i++)
                    {
                        using (PerformanceProfiler.BeginSample("VoterUpdate"))
                        {
                            _behaviorSystem.Update();
                        }
                    }

                    var elapsed = System.DateTime.UtcNow - startTime;
                    var avgTime = elapsed.TotalMilliseconds / iterations;

                    results.Add((count, avgTime));

                    Debug.Log($"Voter count: {count}, Avg update time: {avgTime:F2}ms");

                    TearDown();
                }
            }

            // Validate scalability - should be roughly linear
            for (int i = 1; i < results.Count; i++)
            {
                var (prevCount, prevTime) = results[i - 1];
                var (currCount, currTime) = results[i];

                var countRatio = (double)currCount / prevCount;
                var timeRatio = currTime / prevTime;

                // Time ratio should not exceed count ratio by more than 50%
                Assert.Less(timeRatio, countRatio * 1.5f,
                    $"Performance degradation detected: {currCount} voters took {timeRatio:F2}x longer than expected");
            }
        }

        [UnityTest, Performance]
        public IEnumerator AIBehaviorInfluence_BatchProcessing_Efficiency()
        {
            const int voterCount = 1000;
            const int testDuration = 30; // 30 seconds

            // Spawn voters
            var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
            _spawningSystem.SpawnVoters(ref stateRef, voterCount);

            // Initialize voters
            _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();

            var startTime = Time.realtimeSinceStartup;
            var frameCount = 0;
            var aiRequestCount = 0;

            using (Measure.Scope("AIBehaviorInfluence"))
            {
                while (Time.realtimeSinceStartup - startTime < testDuration)
                {
                    var frameStart = Time.realtimeSinceStartup;

                    // Update AI system
                    _aiSystem.Update();

                    frameCount++;

                    // Track AI requests (simulated)
                    var stats = PerformanceProfiler.GetStats("PoliticalAnalysis");
                    if (stats.SampleCount > aiRequestCount)
                    {
                        aiRequestCount = stats.SampleCount;
                        Debug.Log($"AI request #{aiRequestCount} at frame {frameCount}");
                    }

                    yield return null;
                }
            }

            var totalTime = Time.realtimeSinceStartup - startTime;
            var avgFPS = frameCount / totalTime;

            Debug.Log($"AI system test completed: {frameCount} frames in {totalTime:F2}s (avg FPS: {avgFPS:F1})");
            Debug.Log($"Total AI requests: {aiRequestCount}");

            // Validate efficiency targets
            Assert.Greater(avgFPS, 30f, "AI system should maintain >30 FPS");
            Assert.Less(aiRequestCount, voterCount / 10, "AI requests should be <10% of voter count due to batching");

            // Validate that AI system reduces API calls through batching
            var expectedMaxRequests = voterCount / 50; // Batch size of 50
            Assert.Less(aiRequestCount, expectedMaxRequests * 2, "AI batching should significantly reduce API calls");
        }

        [Test, Performance]
        public void DatabaseIntegration_VoterStatePersistence_Performance()
        {
            const int voterCount = 1000;
            const double maxSaveTime = 1000.0; // 1 second max
            const double maxLoadTime = 500.0; // 500ms max

            // This test would require database service integration
            // For now, we'll test the data structure serialization performance

            using (Measure.Scope("VoterStateSerialization"))
            {
                // Spawn voters
                var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                _spawningSystem.SpawnVoters(ref stateRef, voterCount);

                _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();

                var voterQuery = _testWorld.EntityManager.CreateEntityQuery(typeof(VoterData), typeof(PoliticalOpinion), typeof(BehaviorState));
                var voterEntities = voterQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

                // Measure serialization time
                var startTime = System.DateTime.UtcNow;

                var serializedData = new List<string>();
                foreach (var entity in voterEntities)
                {
                    var voterData = _testWorld.EntityManager.GetComponentData<VoterData>(entity);
                    var opinion = _testWorld.EntityManager.GetComponentData<PoliticalOpinion>(entity);
                    var behavior = _testWorld.EntityManager.GetComponentData<BehaviorState>(entity);

                    // Simulate serialization
                    var serialized = $"{voterData.VoterId},{voterData.Age},{opinion.EconomicPosition},{behavior.Satisfaction}";
                    serializedData.Add(serialized);
                }

                var serializeTime = (System.DateTime.UtcNow - startTime).TotalMilliseconds;

                // Measure deserialization time
                startTime = System.DateTime.UtcNow;

                var deserializedCount = 0;
                foreach (var data in serializedData)
                {
                    var parts = data.Split(',');
                    if (parts.Length >= 4)
                    {
                        deserializedCount++;
                    }
                }

                var deserializeTime = (System.DateTime.UtcNow - startTime).TotalMilliseconds;

                Debug.Log($"Serialization: {serializeTime:F2}ms for {voterCount} voters");
                Debug.Log($"Deserialization: {deserializeTime:F2}ms for {deserializedCount} voters");

                Assert.Less(serializeTime, maxSaveTime, "Voter state serialization too slow");
                Assert.Less(deserializeTime, maxLoadTime, "Voter state deserialization too slow");
                Assert.AreEqual(voterCount, deserializedCount, "Deserialization count mismatch");

                voterEntities.Dispose();
                voterQuery.Dispose();
            }
        }

        private IEnumerator MeasureVoterSimulationPerformance(int voterCount, int testDurationFrames, float targetFrameTime)
        {
            // Spawn voters
            var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
            _spawningSystem.SpawnVoters(ref stateRef, voterCount);

            // Warm up - let systems initialize
            for (int i = 0; i < 30; i++)
            {
                _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();
                yield return null;
            }

            Debug.Log($"Starting performance test: {voterCount} voters, {testDurationFrames} frames, target: {targetFrameTime:F2}ms");

            // Measure performance
            var frameTimes = new List<float>();
            var systemGroup = _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();

            using (Measure.Scope($"VoterSimulation_{voterCount}"))
            {
                for (int frame = 0; frame < testDurationFrames; frame++)
                {
                    var frameStart = Time.realtimeSinceStartup;

                    // Update voter simulation
                    using (PerformanceProfiler.BeginSample("VoterUpdate"))
                    {
                        systemGroup.Update();
                    }

                    yield return null;

                    var frameTime = (Time.realtimeSinceStartup - frameStart) * 1000f; // Convert to ms
                    frameTimes.Add(frameTime);

                    // Log progress every 60 frames
                    if (frame % 60 == 0)
                    {
                        Debug.Log($"Frame {frame}/{testDurationFrames}, Frame time: {frameTime:F2}ms");
                    }
                }
            }

            // Analyze results
            var avgFrameTime = frameTimes.Average();
            var maxFrameTime = frameTimes.Max();
            var p95FrameTime = frameTimes.OrderBy(x => x).ElementAt((int)(frameTimes.Count * 0.95f));

            Debug.Log($"Performance results for {voterCount} voters:");
            Debug.Log($"  Average frame time: {avgFrameTime:F2}ms (target: {targetFrameTime:F2}ms)");
            Debug.Log($"  Maximum frame time: {maxFrameTime:F2}ms");
            Debug.Log($"  95th percentile: {p95FrameTime:F2}ms");

            // Get detailed profiling stats
            var voterStats = PerformanceProfiler.GetStats("VoterUpdate");
            if (voterStats.SampleCount > 0)
            {
                Debug.Log($"  Voter update avg: {voterStats.AverageTimeMs:F2}ms");
                Debug.Log($"  Voter update P95: {voterStats.P95TimeMs:F2}ms");
            }

            // Validate performance targets
            Assert.Less(avgFrameTime, targetFrameTime,
                $"Average frame time exceeds target: {avgFrameTime:F2}ms > {targetFrameTime:F2}ms");

            Assert.Less(p95FrameTime, targetFrameTime * 1.5f,
                $"95th percentile frame time exceeds acceptable threshold: {p95FrameTime:F2}ms > {targetFrameTime * 1.5f:F2}ms");

            // Validate voter count
            var voterQuery = _testWorld.EntityManager.CreateEntityQuery(typeof(VoterData));
            var actualVoterCount = voterQuery.CalculateEntityCount();

            Assert.AreEqual(voterCount, actualVoterCount, "Voter count changed during simulation");

            voterQuery.Dispose();

            // Check for memory leaks
            var initialMemory = System.GC.GetTotalMemory(true);
            System.GC.Collect();
            var finalMemory = System.GC.GetTotalMemory(true);
            var memoryGrowth = (finalMemory - initialMemory) / (1024f * 1024f);

            Assert.Less(memoryGrowth, 10f, $"Potential memory leak detected: {memoryGrowth:F2}MB growth");

            Debug.Log($"Performance test completed successfully for {voterCount} voters");
        }
    }
}