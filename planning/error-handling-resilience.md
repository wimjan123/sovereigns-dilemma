# Error Handling & Resilience Patterns
**Project**: The Sovereign's Dilemma - Dutch Political Simulation
**Version**: 1.0
**Date**: 2025-09-18
**Framework**: Polly v8 .NET Resilience Library + Custom Unity Integration

## Executive Summary

Comprehensive error handling and resilience strategy for The Sovereign's Dilemma, implementing production-ready patterns to ensure system stability, graceful degradation, and excellent user experience under failure conditions.

## Resilience Architecture Overview

### Core Resilience Patterns
Based on Polly v8 (2025) and Microsoft's production resilience recommendations:

1. **Circuit Breaker**: Prevent cascading failures from external services
2. **Retry with Exponential Backoff**: Handle transient failures gracefully
3. **Timeout**: Prevent indefinite waits and resource exhaustion
4. **Bulkhead**: Isolate failure domains and prevent resource starvation
5. **Fallback**: Provide degraded but functional service during outages

## Resilience Implementation

### 1. Circuit Breaker Pattern

```csharp
namespace SovereignsDilemma.Resilience
{
    using Polly;
    using Polly.CircuitBreaker;

    // NVIDIA NIM API Circuit Breaker
    public class NIMServiceCircuitBreaker
    {
        private readonly ResiliencePipeline _pipeline;

        public NIMServiceCircuitBreaker()
        {
            _pipeline = new ResiliencePipelineBuilder()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    // Circuit breaks if >50% of calls fail within 60-second window
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    MinimumThroughput = 8,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    OnOpened = args =>
                    {
                        UnityEngine.Debug.LogWarning($"Circuit breaker OPENED: {args.BreakDuration}");
                        TelemetryService.RecordCircuitBreakerState("nim_api", "open");
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        UnityEngine.Debug.Log("Circuit breaker CLOSED - service recovered");
                        TelemetryService.RecordCircuitBreakerState("nim_api", "closed");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        UnityEngine.Debug.Log("Circuit breaker HALF-OPEN - testing service");
                        TelemetryService.RecordCircuitBreakerState("nim_api", "half_open");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> operation)
        {
            try
            {
                return await _pipeline.ExecuteAsync(operation);
            }
            catch (BrokenCircuitException)
            {
                throw new ServiceUnavailableException("NVIDIA NIM API circuit is open");
            }
        }
    }
}
```

### 2. Retry with Exponential Backoff

```csharp
// Retry Policy for Transient Failures
public class RetryPolicyBuilder
{
    public static ResiliencePipeline BuildRetryPolicy()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true, // Add randomization to prevent thundering herd
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    UnityEngine.Debug.LogWarning(
                        $"Retry attempt {args.AttemptNumber} for operation after {args.Outcome.Exception?.Message}");

                    TelemetryService.RecordRetryAttempt(args.AttemptNumber, args.Outcome.Exception?.GetType().Name);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
```

### 3. Timeout Strategy

```csharp
// Timeout Configuration for Different Operations
public static class TimeoutStrategies
{
    public static ResiliencePipeline APICallTimeout => new ResiliencePipelineBuilder()
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(10), // Max 10 seconds for API calls
            OnTimeout = args =>
            {
                UnityEngine.Debug.LogError($"Operation timed out after {args.Timeout}");
                TelemetryService.RecordTimeout("api_call", args.Timeout);
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    public static ResiliencePipeline DatabaseTimeout => new ResiliencePipelineBuilder()
        .AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(5), // Max 5 seconds for DB operations
            OnTimeout = args =>
            {
                UnityEngine.Debug.LogError($"Database operation timed out after {args.Timeout}");
                TelemetryService.RecordTimeout("database", args.Timeout);
                return ValueTask.CompletedTask;
            }
        })
        .Build();
}
```

### 4. Bulkhead Pattern

```csharp
// Resource Isolation using Bulkhead Pattern
public class BulkheadResourceManager
{
    private readonly SemaphoreSlim _apiCallSemaphore;
    private readonly SemaphoreSlim _databaseSemaphore;
    private readonly SemaphoreSlim _voterSimulationSemaphore;

    public BulkheadResourceManager()
    {
        // Limit concurrent API calls to prevent overwhelming external service
        _apiCallSemaphore = new SemaphoreSlim(5, 5);

        // Limit concurrent database operations
        _databaseSemaphore = new SemaphoreSlim(10, 10);

        // Limit concurrent voter simulation threads
        _voterSimulationSemaphore = new SemaphoreSlim(4, 4);
    }

    public async Task<T> ExecuteAPICallAsync<T>(Func<Task<T>> operation)
    {
        await _apiCallSemaphore.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _apiCallSemaphore.Release();
        }
    }

    public async Task<T> ExecuteDatabaseOperationAsync<T>(Func<Task<T>> operation)
    {
        await _databaseSemaphore.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _databaseSemaphore.Release();
        }
    }
}
```

### 5. Fallback Strategy

```csharp
// Fallback Implementation for Service Degradation
public class FallbackService : IPoliticalAnalysisService
{
    private readonly IPoliticalAnalysisService _primaryService;
    private readonly ILocalFallbackService _fallbackService;
    private readonly ResiliencePipeline _fallbackPipeline;

    public FallbackService(
        IPoliticalAnalysisService primaryService,
        ILocalFallbackService fallbackService)
    {
        _primaryService = primaryService;
        _fallbackService = fallbackService;

        _fallbackPipeline = new ResiliencePipelineBuilder()
            .AddFallback(new FallbackStrategyOptions<PoliticalAnalysis>
            {
                ShouldHandle = new PredicateBuilder<PoliticalAnalysis>()
                    .Handle<ServiceUnavailableException>()
                    .Handle<BrokenCircuitException>()
                    .Handle<TimeoutException>(),
                FallbackAction = args =>
                {
                    UnityEngine.Debug.LogWarning("Primary service failed, using fallback");
                    TelemetryService.RecordFallbackUsage("political_analysis");

                    return Outcome.FromResultAsValueTask(_fallbackService.GetCachedAnalysis(args.Context));
                }
            })
            .Build();
    }

    public async Task<PoliticalAnalysis> AnalyzeContent(string content)
    {
        return await _fallbackPipeline.ExecuteAsync(async token =>
        {
            return await _primaryService.AnalyzeContent(content);
        });
    }
}

// Local Fallback Service Implementation
public class LocalFallbackService : ILocalFallbackService
{
    private readonly Dictionary<string, PoliticalAnalysis> _cachedAnalyses;
    private readonly IRuleBasedAnalyzer _ruleBasedAnalyzer;

    public PoliticalAnalysis GetCachedAnalysis(ResilienceContext context)
    {
        var content = context.Properties.GetValue("content", string.Empty);

        // Try cached response first
        if (_cachedAnalyses.TryGetValue(content, out var cached))
        {
            return cached;
        }

        // Fall back to rule-based analysis
        return _ruleBasedAnalyzer.Analyze(content);
    }
}
```

## Comprehensive Error Handling Strategy

### 1. Error Classification

```csharp
// Error Classification Hierarchy
public abstract class PoliticalSimulationException : Exception
{
    public ErrorSeverity Severity { get; }
    public string Context { get; }
    public bool IsRecoverable { get; }

    protected PoliticalSimulationException(
        string message,
        ErrorSeverity severity,
        string context,
        bool isRecoverable = true) : base(message)
    {
        Severity = severity;
        Context = context;
        IsRecoverable = isRecoverable;
    }
}

// Specific Error Types
public class ServiceUnavailableException : PoliticalSimulationException
{
    public ServiceUnavailableException(string service)
        : base($"Service {service} is unavailable", ErrorSeverity.High, service, true) { }
}

public class DataCorruptionException : PoliticalSimulationException
{
    public DataCorruptionException(string dataType)
        : base($"Data corruption detected in {dataType}", ErrorSeverity.Critical, dataType, false) { }
}

public class VoterSimulationException : PoliticalSimulationException
{
    public VoterSimulationException(string operation)
        : base($"Voter simulation failed during {operation}", ErrorSeverity.Medium, operation, true) { }
}

public enum ErrorSeverity
{
    Low,      // Warning level, doesn't affect core functionality
    Medium,   // Affects some features, degraded experience
    High,     // Affects core functionality, significant impact
    Critical  // System integrity threatened, immediate action required
}
```

### 2. Global Error Handler

```csharp
// Unity Global Error Handler
public class GlobalErrorHandler : MonoBehaviour
{
    private readonly IErrorReportingService _errorReporting;
    private readonly IUserNotificationService _userNotification;
    private readonly ITelemetryService _telemetry;

    private void Awake()
    {
        // Handle Unity-specific errors
        Application.logMessageReceived += HandleUnityLogMessage;

        // Handle unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
    }

    private void HandleUnityLogMessage(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            var error = new UnityError
            {
                Message = logString,
                StackTrace = stackTrace,
                Type = type,
                Timestamp = DateTime.UtcNow,
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };

            ProcessError(error);
        }
    }

    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var error = new UnhandledError
        {
            Exception = exception,
            IsTerminating = e.IsTerminating,
            Timestamp = DateTime.UtcNow
        };

        ProcessCriticalError(error);
    }

    private void ProcessError(IError error)
    {
        // Record telemetry
        _telemetry.RecordError(error);

        // Classify error severity
        var severity = ClassifyError(error);

        // Handle based on severity
        switch (severity)
        {
            case ErrorSeverity.Low:
                // Log only, no user notification
                break;

            case ErrorSeverity.Medium:
                // Log and show subtle notification
                _userNotification.ShowWarning(error.GetUserFriendlyMessage());
                break;

            case ErrorSeverity.High:
                // Log, notify user, attempt recovery
                _userNotification.ShowError(error.GetUserFriendlyMessage());
                AttemptRecovery(error);
                break;

            case ErrorSeverity.Critical:
                // Full error handling with graceful shutdown option
                HandleCriticalError(error);
                break;
        }
    }
}
```

### 3. Recovery Strategies

```csharp
// Error Recovery Service
public class ErrorRecoveryService
{
    private readonly IGameStateService _gameState;
    private readonly IDataBackupService _backup;
    private readonly IUserNotificationService _notification;

    public async Task<bool> AttemptRecovery(IError error)
    {
        try
        {
            switch (error.Context)
            {
                case "voter_simulation":
                    return await RecoverVoterSimulation();

                case "database":
                    return await RecoverDatabase();

                case "api_integration":
                    return await RecoverAPIIntegration();

                default:
                    return await GenericRecovery();
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Recovery failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RecoverVoterSimulation()
    {
        // Reset voter simulation to last known good state
        var lastGoodState = await _backup.GetLastVoterSimulationState();
        if (lastGoodState != null)
        {
            _gameState.RestoreVoterSimulation(lastGoodState);
            _notification.ShowInfo("Voter simulation recovered from backup");
            return true;
        }

        // Initialize with reduced voter count
        _gameState.InitializeVoterSimulation(reducedCount: 5000);
        _notification.ShowWarning("Voter simulation restarted with reduced complexity");
        return true;
    }

    private async Task<bool> RecoverDatabase()
    {
        // Attempt database repair
        var repairResult = await _backup.RepairDatabase();
        if (repairResult.Success)
        {
            _notification.ShowInfo("Database recovered successfully");
            return true;
        }

        // Restore from backup
        var restoreResult = await _backup.RestoreFromBackup();
        if (restoreResult.Success)
        {
            _notification.ShowWarning("Database restored from backup - some recent data may be lost");
            return true;
        }

        return false;
    }
}
```

## Operational Monitoring Integration

### 1. Error Telemetry

```csharp
// Error Tracking and Telemetry
public class ErrorTelemetryService
{
    private readonly Dictionary<string, int> _errorCounts = new();
    private readonly Queue<ErrorEvent> _recentErrors = new();
    private readonly Timer _reportingTimer;

    public void RecordError(IError error)
    {
        var key = $"{error.Context}_{error.Severity}";
        _errorCounts.TryGetValue(key, out var count);
        _errorCounts[key] = count + 1;

        _recentErrors.Enqueue(new ErrorEvent
        {
            Error = error,
            Timestamp = DateTime.UtcNow
        });

        // Keep only last 100 errors
        while (_recentErrors.Count > 100)
        {
            _recentErrors.Dequeue();
        }

        // Check for error rate spikes
        CheckErrorRateSpike(error);
    }

    private void CheckErrorRateSpike(IError error)
    {
        var recentSimilarErrors = _recentErrors
            .Where(e => e.Error.Context == error.Context)
            .Where(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5))
            .Count();

        if (recentSimilarErrors > 10)
        {
            TriggerAlert(new ErrorRateSpike
            {
                Context = error.Context,
                Count = recentSimilarErrors,
                TimeWindow = TimeSpan.FromMinutes(5)
            });
        }
    }
}
```

### 2. Health Monitoring

```csharp
// System Health Monitor
public class HealthMonitorService : MonoBehaviour
{
    private readonly Dictionary<string, IHealthCheck> _healthChecks = new();

    private void Start()
    {
        // Register health checks
        RegisterHealthCheck("nim_api", new NIMAPIHealthCheck());
        RegisterHealthCheck("database", new DatabaseHealthCheck());
        RegisterHealthCheck("voter_simulation", new VoterSimulationHealthCheck());

        // Start periodic health monitoring
        InvokeRepeating(nameof(PerformHealthChecks), 30f, 30f);
    }

    private async void PerformHealthChecks()
    {
        var healthReport = new HealthReport();

        foreach (var (name, healthCheck) in _healthChecks)
        {
            try
            {
                var result = await healthCheck.CheckHealthAsync();
                healthReport.AddResult(name, result);

                if (result.Status == HealthStatus.Unhealthy)
                {
                    TriggerHealthAlert(name, result);
                }
            }
            catch (Exception ex)
            {
                healthReport.AddResult(name, HealthCheckResult.Unhealthy(ex.Message));
            }
        }

        // Report overall system health
        ReportSystemHealth(healthReport);
    }
}
```

## Testing Strategy

### 1. Resilience Testing

```csharp
// Chaos Engineering Tests
[Test]
public async Task CircuitBreaker_OpensAfterConsecutiveFailures()
{
    // Arrange
    var mockService = new Mock<IPoliticalAnalysisService>();
    mockService.Setup(s => s.AnalyzeContent(It.IsAny<string>()))
           .ThrowsAsync(new HttpRequestException("Service unavailable"));

    var circuitBreaker = new NIMServiceCircuitBreaker();

    // Act & Assert
    for (int i = 0; i < 5; i++)
    {
        await Assert.ThrowsAsync<HttpRequestException>(
            () => circuitBreaker.ExecuteAsync(_ => mockService.Object.AnalyzeContent("test")));
    }

    // Circuit should now be open
    await Assert.ThrowsAsync<ServiceUnavailableException>(
        () => circuitBreaker.ExecuteAsync(_ => mockService.Object.AnalyzeContent("test")));
}

[Test]
public async Task Fallback_ActivatesWhenPrimaryServiceFails()
{
    // Arrange
    var primaryService = new Mock<IPoliticalAnalysisService>();
    primaryService.Setup(s => s.AnalyzeContent(It.IsAny<string>()))
              .ThrowsAsync(new ServiceUnavailableException("Primary service down"));

    var fallbackService = new Mock<ILocalFallbackService>();
    fallbackService.Setup(s => s.GetCachedAnalysis(It.IsAny<ResilienceContext>()))
               .Returns(new PoliticalAnalysis { Sentiment = "neutral" });

    var service = new FallbackService(primaryService.Object, fallbackService.Object);

    // Act
    var result = await service.AnalyzeContent("test content");

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual("neutral", result.Sentiment);
    fallbackService.Verify(s => s.GetCachedAnalysis(It.IsAny<ResilienceContext>()), Times.Once);
}
```

This comprehensive error handling and resilience framework ensures The Sovereign's Dilemma can handle production failures gracefully while maintaining excellent user experience and system stability.