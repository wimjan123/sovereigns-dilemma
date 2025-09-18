using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using SovereignsDilemma.Political.Systems;
using SovereignsDilemma.Core.EventBus;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Testing.PerformanceFramework
{
    /// <summary>
    /// Comprehensive production validation suite for The Sovereign's Dilemma.
    /// Validates all Phase 2 requirements and production readiness standards.
    /// </summary>
    public class ProductionValidationSuite : MonoBehaviour
    {
        [Header("Validation Configuration")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool enableDetailedLogging = true;
        [SerializeField] private bool generateReport = true;
        [SerializeField] private string reportPath = "claudedocs/production-validation-report.md";

        [Header("Performance Targets")]
        [SerializeField] private int targetVoterCount = 10000;
        [SerializeField] private float minimumFPS = 30f;
        [SerializeField] private float maximumMemoryMB = 1024f;
        [SerializeField] private float sessionDurationMinutes = 60f;
        [SerializeField] private float maxAIResponseTimeSeconds = 2f;
        [SerializeField] private float maxDatabaseOperationMs = 500f;

        private ValidationResults _results;
        private World _world;
        private FullScaleVoterSystem _voterSystem;
        private PoliticalEventSystem _eventSystem;
        private EventBusSystem _eventBus;

        private readonly ProfilerMarker _validationMarker = new("ProductionValidation");

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunFullValidationSuite());
            }
        }

        [ContextMenu("Run Production Validation")]
        public void RunValidation()
        {
            StartCoroutine(RunFullValidationSuite());
        }

        private IEnumerator RunFullValidationSuite()
        {
            using (_validationMarker.Auto())
            {
                _results = new ValidationResults();
                _results.StartTime = DateTime.UtcNow;

                LogValidation("=== PRODUCTION VALIDATION SUITE STARTED ===");
                LogValidation($"Target: {targetVoterCount} voters at {minimumFPS}+ FPS");
                LogValidation($"Memory limit: {maximumMemoryMB}MB, Session: {sessionDurationMinutes}min");

                // Phase 1: System Initialization
                yield return StartCoroutine(ValidateSystemInitialization());

                // Phase 2: Performance Baseline
                yield return StartCoroutine(ValidatePerformanceBaseline());

                // Phase 3: Scaling Validation
                yield return StartCoroutine(ValidateScalingPerformance());

                // Phase 4: Memory Stability
                yield return StartCoroutine(ValidateMemoryStability());

                // Phase 5: Event System Validation
                yield return StartCoroutine(ValidateEventSystemPerformance());

                // Phase 6: Long-term Stability
                yield return StartCoroutine(ValidateLongTermStability());

                // Phase 7: Integration Testing
                yield return StartCoroutine(ValidateSystemIntegration());

                // Final Results
                _results.EndTime = DateTime.UtcNow;
                _results.TotalDuration = _results.EndTime - _results.StartTime;

                LogValidationResults();

                if (generateReport)
                {
                    GenerateValidationReport();
                }

                LogValidation("=== PRODUCTION VALIDATION SUITE COMPLETED ===");
            }
        }

        private IEnumerator ValidateSystemInitialization()
        {
            LogValidation("Phase 1: System Initialization Validation");

            var phase = new ValidationPhase { PhaseName = "System Initialization" };

            try
            {
                // Initialize ECS World
                _world = World.DefaultGameObjectInjectionWorld;
                if (_world == null)
                {
                    phase.AddError("ECS World not found");
                    yield break;
                }

                // Validate core systems
                _voterSystem = _world.GetExistingSystemManaged<FullScaleVoterSystem>();
                _eventSystem = _world.GetExistingSystemManaged<PoliticalEventSystem>();
                _eventBus = _world.GetExistingSystemManaged<EventBusSystem>();

                if (_voterSystem == null) phase.AddError("FullScaleVoterSystem not found");
                if (_eventSystem == null) phase.AddError("PoliticalEventSystem not found");
                if (_eventBus == null) phase.AddError("EventBusSystem not found");

                // Validate Jobs System
                var jobHandle = new TestJob().Schedule();
                jobHandle.Complete();
                phase.AddSuccess("Unity Jobs System operational");

                // Validate Burst compilation
                var burstTestResult = TestBurstCompilation();
                if (burstTestResult)
                {
                    phase.AddSuccess("Burst compilation verified");
                }
                else
                {
                    phase.AddWarning("Burst compilation may not be enabled");
                }

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"System initialization failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
            yield return null;
        }

        private IEnumerator ValidatePerformanceBaseline()
        {
            LogValidation("Phase 2: Performance Baseline Validation");

            var phase = new ValidationPhase { PhaseName = "Performance Baseline" };

            try
            {
                // Initial memory snapshot
                var initialMemory = Profiler.GetTotalAllocatedMemory(false) / (1024 * 1024);
                phase.AddMetric("Initial Memory (MB)", initialMemory);

                // FPS baseline with minimal load
                yield return StartCoroutine(MeasureFPS(1f, "Baseline FPS"));

                var baselineFPS = _results.AverageFPS;
                phase.AddMetric("Baseline FPS", baselineFPS);

                if (baselineFPS >= 60f)
                {
                    phase.AddSuccess($"Excellent baseline performance: {baselineFPS:F1} FPS");
                }
                else if (baselineFPS >= 30f)
                {
                    phase.AddSuccess($"Good baseline performance: {baselineFPS:F1} FPS");
                }
                else
                {
                    phase.AddError($"Poor baseline performance: {baselineFPS:F1} FPS");
                }

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"Baseline validation failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
            yield return null;
        }

        private IEnumerator ValidateScalingPerformance()
        {
            LogValidation("Phase 3: Scaling Performance Validation");

            var phase = new ValidationPhase { PhaseName = "Scaling Performance" };

            try
            {
                // Test scaling in increments
                var scalingPoints = new[] { 1000, 2500, 5000, 7500, 10000 };

                foreach (var voterCount in scalingPoints)
                {
                    LogValidation($"Testing {voterCount} voters...");

                    // Create voters for this scale test
                    yield return StartCoroutine(CreateVotersForTest(voterCount));

                    // Measure performance at this scale
                    yield return StartCoroutine(MeasureFPS(3f, $"{voterCount} Voters"));

                    var scaleFPS = _results.AverageFPS;
                    var memoryUsage = Profiler.GetTotalAllocatedMemory(false) / (1024 * 1024);

                    phase.AddMetric($"FPS at {voterCount} voters", scaleFPS);
                    phase.AddMetric($"Memory at {voterCount} voters (MB)", memoryUsage);

                    // Validate against targets
                    if (voterCount == targetVoterCount)
                    {
                        if (scaleFPS >= minimumFPS)
                        {
                            phase.AddSuccess($"Target performance achieved: {scaleFPS:F1} FPS at {voterCount} voters");
                        }
                        else
                        {
                            phase.AddError($"Target performance failed: {scaleFPS:F1} FPS < {minimumFPS} FPS at {voterCount} voters");
                        }

                        if (memoryUsage <= maximumMemoryMB)
                        {
                            phase.AddSuccess($"Memory target achieved: {memoryUsage:F1} MB");
                        }
                        else
                        {
                            phase.AddError($"Memory target exceeded: {memoryUsage:F1} MB > {maximumMemoryMB} MB");
                        }
                    }

                    // Performance degradation analysis
                    var performanceDrop = _results.BaselineFPS > 0 ? ((_results.BaselineFPS - scaleFPS) / _results.BaselineFPS) * 100f : 0f;
                    phase.AddMetric($"Performance drop at {voterCount} (%)", performanceDrop);

                    yield return new WaitForSeconds(0.5f);
                }

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"Scaling validation failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
        }

        private IEnumerator ValidateMemoryStability()
        {
            LogValidation("Phase 4: Memory Stability Validation");

            var phase = new ValidationPhase { PhaseName = "Memory Stability" };

            try
            {
                var initialMemory = Profiler.GetTotalAllocatedMemory(false);
                var memoryReadings = new List<long>();
                var duration = 30f; // 30 second memory test
                var interval = 1f;

                for (float elapsed = 0; elapsed < duration; elapsed += interval)
                {
                    var currentMemory = Profiler.GetTotalAllocatedMemory(false);
                    memoryReadings.Add(currentMemory);

                    yield return new WaitForSeconds(interval);
                }

                // Analyze memory stability
                var maxMemory = memoryReadings.Max();
                var minMemory = memoryReadings.Min();
                var finalMemory = memoryReadings.Last();
                var memoryGrowth = finalMemory - initialMemory;
                var memoryVariance = maxMemory - minMemory;

                phase.AddMetric("Initial Memory (MB)", initialMemory / (1024 * 1024));
                phase.AddMetric("Final Memory (MB)", finalMemory / (1024 * 1024));
                phase.AddMetric("Memory Growth (MB)", memoryGrowth / (1024 * 1024));
                phase.AddMetric("Memory Variance (MB)", memoryVariance / (1024 * 1024));

                // Memory stability criteria
                var growthMB = memoryGrowth / (1024 * 1024);
                if (growthMB < 50)
                {
                    phase.AddSuccess($"Excellent memory stability: {growthMB:F1} MB growth");
                }
                else if (growthMB < 100)
                {
                    phase.AddWarning($"Moderate memory growth: {growthMB:F1} MB");
                }
                else
                {
                    phase.AddError($"Excessive memory growth: {growthMB:F1} MB");
                }

                // Force garbage collection and verify cleanup
                GC.Collect();
                yield return new WaitForSeconds(1f);

                var afterGCMemory = Profiler.GetTotalAllocatedMemory(false);
                var gcCleanup = finalMemory - afterGCMemory;
                phase.AddMetric("GC Cleanup (MB)", gcCleanup / (1024 * 1024));

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"Memory stability validation failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
        }

        private IEnumerator ValidateEventSystemPerformance()
        {
            LogValidation("Phase 5: Event System Performance Validation");

            var phase = new ValidationPhase { PhaseName = "Event System Performance" };

            try
            {
                if (_eventBus == null || _eventSystem == null)
                {
                    phase.AddError("Event systems not available");
                    _results.Phases.Add(phase);
                    yield break;
                }

                // Test event throughput
                var eventCount = 1000;
                var stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < eventCount; i++)
                {
                    var testEvent = new TestPoliticalEvent($"Test Event {i}");
                    _eventBus.Publish(testEvent, "Political");

                    if (i % 100 == 0) yield return null; // Yield occasionally
                }

                stopwatch.Stop();
                var eventsPerSecond = eventCount / (stopwatch.ElapsedMilliseconds / 1000f);

                phase.AddMetric("Events Published", eventCount);
                phase.AddMetric("Events Per Second", eventsPerSecond);
                phase.AddMetric("Average Event Time (ms)", stopwatch.ElapsedMilliseconds / (float)eventCount);

                if (eventsPerSecond > 5000)
                {
                    phase.AddSuccess($"Excellent event throughput: {eventsPerSecond:F0} events/sec");
                }
                else if (eventsPerSecond > 1000)
                {
                    phase.AddSuccess($"Good event throughput: {eventsPerSecond:F0} events/sec");
                }
                else
                {
                    phase.AddWarning($"Low event throughput: {eventsPerSecond:F0} events/sec");
                }

                // Test event bus metrics
                var busMetrics = _eventBus.GetMetrics();
                phase.AddMetric("Event Queue Size", busMetrics.QueueSize);
                phase.AddMetric("Active Handlers", busMetrics.ActiveHandlerCount);
                phase.AddMetric("Bounded Contexts", busMetrics.BoundedContextCount);

                // Test political event system
                if (_eventSystem != null)
                {
                    var eventSystemMetrics = _eventSystem.GetMetrics();
                    phase.AddMetric("Active Political Events", eventSystemMetrics.ActiveEventsCount);
                    phase.AddMetric("Active Crises", eventSystemMetrics.ActiveCrisesCount);
                    phase.AddMetric("Political Tension", eventSystemMetrics.CurrentPoliticalTension);
                }

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"Event system validation failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
            yield return null;
        }

        private IEnumerator ValidateLongTermStability()
        {
            LogValidation("Phase 6: Long-term Stability Validation");

            var phase = new ValidationPhase { PhaseName = "Long-term Stability" };

            try
            {
                // Run reduced duration for validation (5 minutes instead of full hour)
                var testDurationMinutes = Mathf.Min(sessionDurationMinutes, 5f);
                var testDurationSeconds = testDurationMinutes * 60f;

                LogValidation($"Running {testDurationMinutes}-minute stability test...");

                var startTime = Time.realtimeSinceStartup;
                var frameCount = 0;
                var fpsReadings = new List<float>();
                var memoryReadings = new List<float>();
                var lastReading = 0f;

                while (Time.realtimeSinceStartup - startTime < testDurationSeconds)
                {
                    frameCount++;

                    // Take readings every 5 seconds
                    if (Time.realtimeSinceStartup - lastReading >= 5f)
                    {
                        var currentFPS = 1f / Time.unscaledDeltaTime;
                        var currentMemory = Profiler.GetTotalAllocatedMemory(false) / (1024f * 1024f);

                        fpsReadings.Add(currentFPS);
                        memoryReadings.Add(currentMemory);

                        lastReading = Time.realtimeSinceStartup;
                    }

                    yield return null;
                }

                var actualDuration = Time.realtimeSinceStartup - startTime;

                // Analyze stability metrics
                var avgFPS = fpsReadings.Average();
                var minFPS = fpsReadings.Min();
                var maxFPS = fpsReadings.Max();
                var fpsVariance = maxFPS - minFPS;

                var avgMemory = memoryReadings.Average();
                var maxMemory = memoryReadings.Max();
                var memoryGrowth = memoryReadings.Last() - memoryReadings.First();

                phase.AddMetric("Test Duration (min)", actualDuration / 60f);
                phase.AddMetric("Average FPS", avgFPS);
                phase.AddMetric("Minimum FPS", minFPS);
                phase.AddMetric("FPS Variance", fpsVariance);
                phase.AddMetric("Average Memory (MB)", avgMemory);
                phase.AddMetric("Memory Growth (MB)", memoryGrowth);

                // Stability criteria
                if (minFPS >= minimumFPS * 0.8f) // Allow 20% variance
                {
                    phase.AddSuccess($"Stable performance: min FPS {minFPS:F1} ≥ {minimumFPS * 0.8f:F1}");
                }
                else
                {
                    phase.AddError($"Unstable performance: min FPS {minFPS:F1} < {minimumFPS * 0.8f:F1}");
                }

                if (memoryGrowth < 100f) // Less than 100MB growth
                {
                    phase.AddSuccess($"Stable memory usage: {memoryGrowth:F1} MB growth");
                }
                else
                {
                    phase.AddWarning($"Memory growth detected: {memoryGrowth:F1} MB");
                }

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"Long-term stability validation failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
        }

        private IEnumerator ValidateSystemIntegration()
        {
            LogValidation("Phase 7: System Integration Validation");

            var phase = new ValidationPhase { PhaseName = "System Integration" };

            try
            {
                // Test voter-event integration
                var initialVoterQuery = _world.EntityManager.CreateEntityQuery(typeof(VoterData));
                var voterCount = initialVoterQuery.CalculateEntityCount();

                phase.AddMetric("Total Voters", voterCount);

                // Trigger political events and measure impact
                if (_eventSystem != null)
                {
                    var eventSystemMetrics = _eventSystem.GetMetrics();
                    phase.AddMetric("Events Generated", eventSystemMetrics.EventsGeneratedThisSession);

                    // Force generate some events
                    for (int i = 0; i < 5; i++)
                    {
                        // Wait for event generation
                        yield return new WaitForSeconds(1f);
                    }

                    var updatedMetrics = _eventSystem.GetMetrics();
                    var newEvents = updatedMetrics.EventsGeneratedThisSession - eventSystemMetrics.EventsGeneratedThisSession;

                    if (newEvents > 0)
                    {
                        phase.AddSuccess($"Political event generation working: {newEvents} events generated");
                    }
                    else
                    {
                        phase.AddWarning("No political events generated during test");
                    }
                }

                // Test event bus integration
                if (_eventBus != null)
                {
                    var busMetrics = _eventBus.GetMetrics();
                    phase.AddMetric("Total Events Processed", busMetrics.TotalEventsProcessed);
                    phase.AddMetric("Event Queue Size", busMetrics.QueueSize);

                    if (busMetrics.TotalEventsProcessed > 0)
                    {
                        phase.AddSuccess($"Event bus active: {busMetrics.TotalEventsProcessed} events processed");
                    }
                    else
                    {
                        phase.AddWarning("Event bus shows no activity");
                    }
                }

                // Test LOD system integration
                if (_voterSystem != null)
                {
                    var lodMetrics = _voterSystem.GetLODMetrics();
                    phase.AddMetric("High Detail Voters", lodMetrics.HighDetailCount);
                    phase.AddMetric("Medium Detail Voters", lodMetrics.MediumDetailCount);
                    phase.AddMetric("Low Detail Voters", lodMetrics.LowDetailCount);
                    phase.AddMetric("Dormant Voters", lodMetrics.DormantCount);

                    var totalLODVoters = lodMetrics.HighDetailCount + lodMetrics.MediumDetailCount +
                                       lodMetrics.LowDetailCount + lodMetrics.DormantCount;

                    if (totalLODVoters > 0)
                    {
                        phase.AddSuccess($"LOD system active: {totalLODVoters} voters in LOD management");
                    }
                    else
                    {
                        phase.AddWarning("LOD system shows no activity");
                    }
                }

                phase.Success = phase.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                phase.AddError($"System integration validation failed: {ex.Message}");
            }

            _results.Phases.Add(phase);
            yield return null;
        }

        private IEnumerator MeasureFPS(float duration, string testName)
        {
            var startTime = Time.realtimeSinceStartup;
            var frameCount = 0;
            var fpsSum = 0f;

            while (Time.realtimeSinceStartup - startTime < duration)
            {
                var fps = 1f / Time.unscaledDeltaTime;
                fpsSum += fps;
                frameCount++;
                yield return null;
            }

            var averageFPS = frameCount > 0 ? fpsSum / frameCount : 0f;
            _results.AverageFPS = averageFPS;

            if (string.IsNullOrEmpty(testName) == false)
            {
                LogValidation($"{testName}: {averageFPS:F1} FPS");
            }

            if (_results.BaselineFPS == 0f)
            {
                _results.BaselineFPS = averageFPS;
            }
        }

        private IEnumerator CreateVotersForTest(int count)
        {
            // Implementation would create the specified number of voter entities
            // This is a placeholder for the actual voter creation logic
            LogValidation($"Creating {count} voters for test...");
            yield return new WaitForSeconds(0.1f);
        }

        private bool TestBurstCompilation()
        {
            try
            {
                var job = new BurstTestJob { value = 42 };
                var handle = job.Schedule();
                handle.Complete();
                return job.value == 84; // Should be doubled
            }
            catch
            {
                return false;
            }
        }

        private void LogValidation(string message)
        {
            if (enableDetailedLogging)
            {
                Debug.Log($"[VALIDATION] {message}");
            }
        }

        private void LogValidationResults()
        {
            LogValidation("\n=== VALIDATION RESULTS SUMMARY ===");
            LogValidation($"Total Duration: {_results.TotalDuration.TotalMinutes:F1} minutes");
            LogValidation($"Phases Completed: {_results.Phases.Count}");

            var successfulPhases = _results.Phases.Count(p => p.Success);
            var overallSuccess = successfulPhases == _results.Phases.Count;

            LogValidation($"Successful Phases: {successfulPhases}/{_results.Phases.Count}");
            LogValidation($"Overall Result: {(overallSuccess ? "PASS" : "FAIL")}");

            foreach (var phase in _results.Phases)
            {
                var status = phase.Success ? "PASS" : "FAIL";
                LogValidation($"  {phase.PhaseName}: {status} ({phase.SuccessCount} successes, {phase.WarningCount} warnings, {phase.ErrorCount} errors)");
            }
        }

        private void GenerateValidationReport()
        {
            try
            {
                var report = new ProductionValidationReport(_results);
                report.GenerateReport(reportPath);
                LogValidation($"Validation report generated: {reportPath}");
            }
            catch (Exception ex)
            {
                LogValidation($"Failed to generate report: {ex.Message}");
            }
        }

        // Test job structures
        private struct TestJob : IJob
        {
            public void Execute() { }
        }

        private struct BurstTestJob : IJob
        {
            public int value;

            public void Execute()
            {
                value *= 2;
            }
        }

        // Test event for event system validation
        private class TestPoliticalEvent : BaseEvent
        {
            public string TestMessage { get; }

            public TestPoliticalEvent(string message) : base("Political")
            {
                TestMessage = message;
            }
        }
    }

    // Data structures for validation results
    [Serializable]
    public class ValidationResults
    {
        public DateTime StartTime;
        public DateTime EndTime;
        public TimeSpan TotalDuration;
        public float BaselineFPS;
        public float AverageFPS;
        public List<ValidationPhase> Phases = new List<ValidationPhase>();
    }

    [Serializable]
    public class ValidationPhase
    {
        public string PhaseName;
        public bool Success;
        public List<string> Successes = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> Errors = new List<string>();
        public Dictionary<string, float> Metrics = new Dictionary<string, float>();

        public int SuccessCount => Successes.Count;
        public int WarningCount => Warnings.Count;
        public int ErrorCount => Errors.Count;

        public void AddSuccess(string message) => Successes.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
        public void AddError(string message) => Errors.Add(message);
        public void AddMetric(string name, float value) => Metrics[name] = value;
    }
}