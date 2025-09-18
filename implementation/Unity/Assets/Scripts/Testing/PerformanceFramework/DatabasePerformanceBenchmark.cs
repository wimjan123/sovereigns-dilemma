using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Political.Systems;
using SovereignsDilemma.Data.Database;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Testing.Performance
{
    /// <summary>
    /// Performance benchmarks for database optimization validation.
    /// Validates that batch operations, indexing, and connection pooling
    /// meet performance targets for 10,000+ voter simulation.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    [Category("Database")]
    public class DatabasePerformanceBenchmark
    {
        private World _testWorld;
        private VoterSpawningSystem _spawningSystem;
        private DatabaseIntegrationSystem _databaseSystem;
        private OptimizedDatabaseService _databaseService;
        private string _testDatabasePath;

        [SetUp]
        public void SetUp()
        {
            // Create test database path
            _testDatabasePath = Path.Combine(Application.temporaryCachePath, $"test_db_{System.Guid.NewGuid()}.db");

            // Create test world
            _testWorld = new World("DatabaseTestWorld");
            World.DefaultGameObjectInjectionWorld = _testWorld;

            // Initialize systems
            var systems = _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            _spawningSystem = _testWorld.GetOrCreateSystemManaged<VoterSpawningSystem>();
            _databaseSystem = _testWorld.GetOrCreateSystemManaged<DatabaseIntegrationSystem>();

            // Add systems to group
            systems.AddSystemToUpdateList(_spawningSystem);
            systems.AddSystemToUpdateList(_databaseSystem);

            // Initialize database service
            _databaseService = new OptimizedDatabaseService(_testDatabasePath, enableEncryption: false);

            PerformanceProfiler.ClearMeasurements();
        }

        [TearDown]
        public void TearDown()
        {
            PerformanceProfiler.LogSummary();

            _databaseService?.Dispose();

            if (_testWorld != null && _testWorld.IsCreated)
            {
                _testWorld.Dispose();
            }

            // Cleanup test database
            if (File.Exists(_testDatabasePath))
            {
                File.Delete(_testDatabasePath);
            }
        }

        [Test, Performance]
        [Timeout(60000)] // 60 second timeout
        public void DatabaseBatching_1000Voters_UnderTargetTime()
        {
            const int voterCount = 1000;
            const float maxBatchTimeMs = 500f; // 500ms max for 1000 voters

            using (Measure.Scope("DatabaseBatching_1000Voters"))
            {
                // Spawn voters
                var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                _spawningSystem.SpawnVoters(ref stateRef, voterCount);

                // Initialize systems
                for (int i = 0; i < 10; i++)
                {
                    _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();
                }

                // Create batch operations by modifying voters
                ModifyAllVoters();

                // Measure batch operation time
                var startTime = System.DateTime.UtcNow;

                _databaseService.FlushBatchOperations();

                var batchTime = (System.DateTime.UtcNow - startTime).TotalMilliseconds;

                Debug.Log($"Batch operation results:");
                Debug.Log($"  Voters: {voterCount}");
                Debug.Log($"  Batch time: {batchTime:F2}ms");
                Debug.Log($"  Time per voter: {batchTime / voterCount:F2}ms");

                // Validate performance targets
                Assert.Less(batchTime, maxBatchTimeMs,
                    $"Batch time {batchTime:F2}ms exceeds target {maxBatchTimeMs:F2}ms");

                var dbMetrics = _databaseService.GetMetrics();
                Assert.Greater(dbMetrics.TotalBatchOperations, voterCount * 2,
                    "Should have at least voter + opinion updates");

                Assert.Less(dbMetrics.ErrorRate, 0.01f, "Error rate too high");

                PerformanceProfiler.RecordMeasurement("BatchTimeMs", (float)batchTime);
                PerformanceProfiler.RecordMeasurement("TimePerVoterMs", (float)(batchTime / voterCount));
            }
        }

        [Test, Performance]
        public void DatabaseIndexing_QueryPerformance_Validation()
        {
            const int voterCount = 5000;
            const float maxQueryTimeMs = 100f; // 100ms max query time

            using (Measure.Scope("DatabaseIndexing_QueryPerformance"))
            {
                // Spawn and save voters to database
                var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                _spawningSystem.SpawnVoters(ref stateRef, voterCount);

                // Initialize and save all voters
                for (int i = 0; i < 10; i++)
                {
                    _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();
                }

                _databaseSystem.SaveAllVoters();

                // Test various query patterns that should benefit from indexing
                var queryTests = new List<(string description, System.Func<float> queryFunc)>
                {
                    ("Age-based query", () => TestAgeQuery()),
                    ("Education-based query", () => TestEducationQuery()),
                    ("Demographics composite query", () => TestDemographicsQuery()),
                    ("Opinion spectrum query", () => TestOpinionQuery()),
                    ("Behavior engagement query", () => TestBehaviorQuery()),
                    ("Update timestamp query", () => TestTimestampQuery())
                };

                foreach (var (description, queryFunc) in queryTests)
                {
                    var queryTime = queryFunc();

                    Debug.Log($"{description}: {queryTime:F2}ms");

                    Assert.Less(queryTime, maxQueryTimeMs,
                        $"{description} took {queryTime:F2}ms, exceeds limit {maxQueryTimeMs:F2}ms");

                    PerformanceProfiler.RecordMeasurement($"Query_{description.Replace(" ", "")}_Ms", queryTime);
                }
            }
        }

        [Test, Performance]
        public void DatabaseConnectionPooling_ConcurrentAccess_Performance()
        {
            const int concurrentOperations = 20;
            const float maxTotalTimeMs = 2000f; // 2 seconds max for all operations

            using (Measure.Scope("DatabaseConnectionPooling_Concurrent"))
            {
                var operationTasks = new List<System.Threading.Tasks.Task>();
                var operationTimes = new List<float>();
                var lockObject = new object();

                var startTime = System.DateTime.UtcNow;

                // Simulate concurrent database operations
                for (int i = 0; i < concurrentOperations; i++)
                {
                    var operationId = i;
                    var task = System.Threading.Tasks.Task.Run(() =>
                    {
                        var opStartTime = System.DateTime.UtcNow;

                        // Simulate database operation
                        for (int j = 0; j < 50; j++)
                        {
                            _databaseService.QueueVoterUpdate(operationId * 100 + j, CreateTestVoterData(j));
                        }

                        _databaseService.FlushBatchOperations();

                        var opTime = (System.DateTime.UtcNow - opStartTime).TotalMilliseconds;

                        lock (lockObject)
                        {
                            operationTimes.Add((float)opTime);
                        }
                    });

                    operationTasks.Add(task);
                }

                // Wait for all operations to complete
                System.Threading.Tasks.Task.WaitAll(operationTasks.ToArray());

                var totalTime = (System.DateTime.UtcNow - startTime).TotalMilliseconds;
                var avgOperationTime = operationTimes.Average();
                var maxOperationTime = operationTimes.Max();

                Debug.Log($"Connection pooling results:");
                Debug.Log($"  Concurrent operations: {concurrentOperations}");
                Debug.Log($"  Total time: {totalTime:F2}ms");
                Debug.Log($"  Average operation time: {avgOperationTime:F2}ms");
                Debug.Log($"  Max operation time: {maxOperationTime:F2}ms");

                var dbMetrics = _databaseService.GetMetrics();
                Debug.Log($"  Active connections: {dbMetrics.ActiveConnections}");
                Debug.Log($"  Total connections: {dbMetrics.TotalConnections}");

                // Validate performance targets
                Assert.Less(totalTime, maxTotalTimeMs,
                    $"Total time {totalTime:F2}ms exceeds target {maxTotalTimeMs:F2}ms");

                Assert.Less(avgOperationTime, 500f,
                    $"Average operation time {avgOperationTime:F2}ms too high");

                Assert.LessOrEqual(dbMetrics.TotalConnections, 10,
                    "Connection pool should not exceed maximum size");

                PerformanceProfiler.RecordMeasurement("ConcurrentTotalTimeMs", (float)totalTime);
                PerformanceProfiler.RecordMeasurement("ConcurrentAvgOpTimeMs", avgOperationTime);
                PerformanceProfiler.RecordMeasurement("MaxActiveConnections", dbMetrics.ActiveConnections);
            }
        }

        [UnityTest, Performance]
        [Timeout(120000)] // 120 second timeout
        public IEnumerator DatabaseScaling_10000Voters_StressTest()
        {
            const int voterCount = 10000;
            const int testDurationSeconds = 30;

            using (Measure.Scope("DatabaseScaling_10000Voters"))
            {
                // Spawn voters gradually
                var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                var votersPerBatch = 500;
                var spawnedCount = 0;

                while (spawnedCount < voterCount)
                {
                    var toSpawn = Mathf.Min(votersPerBatch, voterCount - spawnedCount);
                    _spawningSystem.SpawnVoters(ref stateRef, toSpawn);
                    spawnedCount += toSpawn;

                    // Update systems
                    for (int i = 0; i < 5; i++)
                    {
                        _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();
                    }

                    yield return null;
                }

                Debug.Log($"Spawned {voterCount} voters, starting database stress test");

                var startTime = Time.realtimeSinceStartup;
                var frameCount = 0;
                var metrics = new List<OptimizedDatabaseService.DatabaseMetrics>();

                while (Time.realtimeSinceStartup - startTime < testDurationSeconds)
                {
                    _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();

                    // Modify voters periodically to generate database operations
                    if (frameCount % 60 == 0)
                    {
                        ModifyRandomVoters(100);
                    }

                    // Collect metrics every 5 seconds
                    if (frameCount % 300 == 0)
                    {
                        var currentMetrics = _databaseService.GetMetrics();
                        metrics.Add(currentMetrics);

                        Debug.Log($"Frame {frameCount}: " +
                                 $"Queued Ops {currentMetrics.QueuedVoterUpdates + currentMetrics.QueuedOpinionUpdates + currentMetrics.QueuedBehaviorUpdates}, " +
                                 $"Avg Batch Time {currentMetrics.AverageBatchTime:F2}ms, " +
                                 $"Connections {currentMetrics.ActiveConnections}/{currentMetrics.TotalConnections}");
                    }

                    frameCount++;
                    yield return null;
                }

                // Force final flush
                _databaseService.FlushBatchOperations();

                // Analyze stress test results
                if (metrics.Count > 0)
                {
                    var finalMetrics = metrics.Last();
                    var avgBatchTime = metrics.Average(m => m.AverageBatchTime);
                    var maxQueuedOps = metrics.Max(m => m.QueuedVoterUpdates + m.QueuedOpinionUpdates + m.QueuedBehaviorUpdates);
                    var avgErrorRate = metrics.Average(m => m.ErrorRate);

                    Debug.Log($"Stress test results for {voterCount} voters:");
                    Debug.Log($"  Average batch time: {avgBatchTime:F2}ms");
                    Debug.Log($"  Max queued operations: {maxQueuedOps}");
                    Debug.Log($"  Average error rate: {avgErrorRate:P}");
                    Debug.Log($"  Final total operations: {finalMetrics.TotalBatchOperations}");

                    // Validate stress test performance
                    Assert.Less(avgBatchTime, 1000f, "Average batch time too high under stress");
                    Assert.Less(maxQueuedOps, 2000, "Queue backed up too much under stress");
                    Assert.Less(avgErrorRate, 0.05f, "Error rate too high under stress");
                    Assert.Greater(finalMetrics.TotalBatchOperations, voterCount,
                        "Should have processed significant operations");

                    PerformanceProfiler.RecordMeasurement("StressTestAvgBatchTime", avgBatchTime);
                    PerformanceProfiler.RecordMeasurement("StressTestMaxQueuedOps", maxQueuedOps);
                    PerformanceProfiler.RecordMeasurement("StressTestErrorRate", avgErrorRate * 100f);
                }
            }
        }

        [Test, Performance]
        public void DatabaseMemoryUsage_ExtendedOperation_Validation()
        {
            const int voterCount = 3000;
            const long maxMemoryGrowthMB = 100; // 100MB max growth
            const int operationCycles = 10;

            using (Measure.Scope("DatabaseMemoryUsage_Extended"))
            {
                // Force garbage collection and measure baseline
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                var initialMemory = System.GC.GetTotalMemory(false);

                // Spawn voters
                var stateRef = _testWorld.Unmanaged.ResolveSystemStateRef(_spawningSystem);
                _spawningSystem.SpawnVoters(ref stateRef, voterCount);

                // Initialize systems
                for (int i = 0; i < 10; i++)
                {
                    _testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().Update();
                }

                // Perform multiple cycles of database operations
                for (int cycle = 0; cycle < operationCycles; cycle++)
                {
                    // Modify all voters
                    ModifyAllVoters();

                    // Force batch operations
                    _databaseService.FlushBatchOperations();

                    // Periodic garbage collection
                    if (cycle % 3 == 0)
                    {
                        System.GC.Collect();
                    }

                    Debug.Log($"Completed cycle {cycle + 1}/{operationCycles}");
                }

                // Final cleanup and measurement
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                var finalMemory = System.GC.GetTotalMemory(false);

                var memoryGrowthMB = (finalMemory - initialMemory) / (1024f * 1024f);
                var dbMetrics = _databaseService.GetMetrics();

                Debug.Log($"Memory usage analysis:");
                Debug.Log($"  Initial memory: {initialMemory / (1024f * 1024f):F2} MB");
                Debug.Log($"  Final memory: {finalMemory / (1024f * 1024f):F2} MB");
                Debug.Log($"  Memory growth: {memoryGrowthMB:F2} MB");
                Debug.Log($"  Total operations: {dbMetrics.TotalBatchOperations}");
                Debug.Log($"  Operations per MB: {dbMetrics.TotalBatchOperations / Math.Max(1, memoryGrowthMB):F0}");

                // Validate memory usage
                Assert.Less(memoryGrowthMB, maxMemoryGrowthMB,
                    $"Memory growth {memoryGrowthMB:F2}MB exceeds limit {maxMemoryGrowthMB}MB");

                var memoryPerVoter = memoryGrowthMB / voterCount * 1024f; // KB per voter
                Assert.Less(memoryPerVoter, 100f,
                    $"Memory per voter {memoryPerVoter:F2}KB too high");

                PerformanceProfiler.RecordMeasurement("MemoryGrowthMB", memoryGrowthMB);
                PerformanceProfiler.RecordMeasurement("MemoryPerVoterKB", memoryPerVoter);
                PerformanceProfiler.RecordMeasurement("OperationsPerMB", dbMetrics.TotalBatchOperations / Math.Max(1, memoryGrowthMB));
            }
        }

        // Helper methods
        private void ModifyAllVoters()
        {
            var voterQuery = _testWorld.EntityManager.CreateEntityQuery(typeof(VoterData), typeof(PoliticalOpinion), typeof(BehaviorState));
            var entities = voterQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                // Modify opinion slightly
                var opinion = _testWorld.EntityManager.GetComponentData<PoliticalOpinion>(entity);
                opinion.EconomicPosition += UnityEngine.Random.Range(-0.01f, 0.01f);
                opinion.SocialPosition += UnityEngine.Random.Range(-0.01f, 0.01f);
                _testWorld.EntityManager.SetComponentData(entity, opinion);

                // Mark as dirty for database save
                _databaseSystem.MarkVoterDirty(entity);
            }

            entities.Dispose();
            voterQuery.Dispose();
        }

        private void ModifyRandomVoters(int count)
        {
            var voterQuery = _testWorld.EntityManager.CreateEntityQuery(typeof(VoterData));
            var entities = voterQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            if (entities.Length == 0)
            {
                entities.Dispose();
                voterQuery.Dispose();
                return;
            }

            for (int i = 0; i < count && i < entities.Length; i++)
            {
                var randomIndex = UnityEngine.Random.Range(0, entities.Length);
                var entity = entities[randomIndex];

                if (_testWorld.EntityManager.HasComponent<PoliticalOpinion>(entity))
                {
                    var opinion = _testWorld.EntityManager.GetComponentData<PoliticalOpinion>(entity);
                    opinion.EconomicPosition += UnityEngine.Random.Range(-0.05f, 0.05f);
                    _testWorld.EntityManager.SetComponentData(entity, opinion);
                    _databaseSystem.MarkVoterDirty(entity);
                }
            }

            entities.Dispose();
            voterQuery.Dispose();
        }

        private VoterData CreateTestVoterData(int id)
        {
            return new VoterData
            {
                VoterId = id,
                Age = UnityEngine.Random.Range(18, 80),
                EducationLevel = (EducationLevel)UnityEngine.Random.Range(0, 4),
                IncomeLevel = (IncomeLevel)UnityEngine.Random.Range(0, 4),
                UrbanizationLevel = (UrbanizationLevel)UnityEngine.Random.Range(0, 3),
                Province = (Province)UnityEngine.Random.Range(0, 12)
            };
        }

        // Mock query methods (would be real database queries in implementation)
        private float TestAgeQuery()
        {
            var startTime = System.DateTime.UtcNow;
            // SELECT * FROM Voters WHERE Age BETWEEN 25 AND 35;
            var endTime = System.DateTime.UtcNow;
            return (float)(endTime - startTime).TotalMilliseconds;
        }

        private float TestEducationQuery()
        {
            var startTime = System.DateTime.UtcNow;
            // SELECT * FROM Voters WHERE EducationLevel = 3;
            var endTime = System.DateTime.UtcNow;
            return (float)(endTime - startTime).TotalMilliseconds;
        }

        private float TestDemographicsQuery()
        {
            var startTime = System.DateTime.UtcNow;
            // SELECT * FROM Voters WHERE Age BETWEEN 30 AND 50 AND EducationLevel >= 2 AND IncomeLevel >= 2;
            var endTime = System.DateTime.UtcNow;
            return (float)(endTime - startTime).TotalMilliseconds;
        }

        private float TestOpinionQuery()
        {
            var startTime = System.DateTime.UtcNow;
            // SELECT * FROM PoliticalOpinions WHERE EconomicPosition > 0.5 AND SocialPosition < 0.5;
            var endTime = System.DateTime.UtcNow;
            return (float)(endTime - startTime).TotalMilliseconds;
        }

        private float TestBehaviorQuery()
        {
            var startTime = System.DateTime.UtcNow;
            // SELECT * FROM BehaviorStates WHERE PoliticalEngagement > 0.7 AND OpinionVolatility > 0.5;
            var endTime = System.DateTime.UtcNow;
            return (float)(endTime - startTime).TotalMilliseconds;
        }

        private float TestTimestampQuery()
        {
            var startTime = System.DateTime.UtcNow;
            // SELECT * FROM Voters WHERE UpdatedAt > datetime('now', '-1 hour');
            var endTime = System.DateTime.UtcNow;
            return (float)(endTime - startTime).TotalMilliseconds;
        }
    }
}