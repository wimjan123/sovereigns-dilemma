# Monitoring and Observability Specification
**Project**: The Sovereign's Dilemma - Dutch Political Simulation
**Version**: 1.0
**Date**: 2025-09-18
**Standard**: OpenTelemetry + SRE Best Practices

## Executive Summary

Comprehensive monitoring and observability strategy for The Sovereign's Dilemma, providing real-time insights into system health, performance, user experience, and business metrics through modern observability practices.

## Observability Architecture

### Three Pillars of Observability

#### 1. Metrics (Quantitative Data)
- **System Metrics**: CPU, memory, FPS, response times
- **Business Metrics**: User engagement, political simulation accuracy
- **SLI/SLO Tracking**: Service Level Indicators and Objectives

#### 2. Logs (Event Data)
- **Structured Logging**: JSON format with correlation IDs
- **Centralized Aggregation**: ELK stack or equivalent
- **Contextual Information**: User actions, system events, errors

#### 3. Traces (Request Flow)
- **Distributed Tracing**: End-to-end request tracking
- **Performance Analysis**: Bottleneck identification
- **Dependency Mapping**: Service interaction visualization

## Unity-Specific Monitoring Implementation

### Performance Monitoring
```csharp
namespace SovereignsDilemma.Monitoring
{
    public class UnityPerformanceCollector : MonoBehaviour
    {
        private readonly MetricsCollector _metrics;
        private readonly PerformanceProfiler _profiler;
        private float _lastCollectionTime;

        private void Update()
        {
            if (Time.time - _lastCollectionTime >= 1.0f) // Collect every second
            {
                CollectFrameMetrics();
                CollectMemoryMetrics();
                CollectVoterSimulationMetrics();
                _lastCollectionTime = Time.time;
            }
        }

        private void CollectFrameMetrics()
        {
            var frameTime = Time.deltaTime;
            var fps = 1.0f / frameTime;

            _metrics.RecordGauge("game.fps", fps, new Dictionary<string, string>
            {
                ["platform"] = Application.platform.ToString(),
                ["quality_level"] = QualitySettings.GetQualityLevel().ToString()
            });

            _metrics.RecordGauge("game.frame_time_ms", frameTime * 1000);

            // Alert on performance degradation
            if (fps < 45)
            {
                _metrics.RecordCounter("game.performance_alerts", 1, new Dictionary<string, string>
                {
                    ["severity"] = "warning",
                    ["type"] = "fps_degradation"
                });
            }
        }

        private void CollectMemoryMetrics()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var unityMemory = Profiler.GetTotalReservedMemory(Profiler.Area.All);

            _metrics.RecordGauge("game.memory.total_bytes", totalMemory);
            _metrics.RecordGauge("game.memory.unity_reserved_bytes", unityMemory);
            _metrics.RecordGauge("game.memory.gc_collection_count", GC.CollectionCount(0));

            // Memory pressure detection
            if (totalMemory > 800_000_000) // 800MB threshold
            {
                _metrics.RecordCounter("game.memory_pressure_alerts", 1);
            }
        }

        private void CollectVoterSimulationMetrics()
        {
            var voterCount = GameState.Current?.ActiveVoterCount ?? 0;
            var simulationLoad = VoterSimulationEngine.Current?.GetCurrentLoad() ?? 0;

            _metrics.RecordGauge("game.voters.active_count", voterCount);
            _metrics.RecordGauge("game.simulation.cpu_load", simulationLoad);
            _metrics.RecordGauge("game.simulation.update_frequency",
                VoterSimulationEngine.Current?.UpdatesPerSecond ?? 0);
        }
    }

    // Custom metrics for game-specific KPIs
    public class GameplayMetricsCollector
    {
        private readonly MetricsCollector _metrics;
        private readonly Dictionary<string, Timer> _timers = new();

        public void RecordPoliticalPost(string userId, string content, PoliticalAnalysis analysis)
        {
            _metrics.RecordCounter("gameplay.posts.created", 1, new Dictionary<string, string>
            {
                ["user_id"] = HashUserId(userId),
                ["political_lean"] = analysis.PoliticalLean.ToString(),
                ["sentiment"] = analysis.Sentiment.ToString()
            });

            _metrics.RecordHistogram("gameplay.posts.content_length", content.Length);
        }

        public void RecordVoterResponse(VoterResponse response)
        {
            _metrics.RecordCounter("gameplay.voter_responses.generated", 1, new Dictionary<string, string>
            {
                ["response_type"] = response.Type.ToString(),
                ["engagement_level"] = response.EngagementLevel.ToString()
            });

            _metrics.RecordHistogram("gameplay.voter_responses.generation_time_ms",
                response.GenerationTime.TotalMilliseconds);
        }

        public IDisposable StartAIOperationTimer(string operationType)
        {
            return _metrics.StartTimer("ai.operation_duration", new Dictionary<string, string>
            {
                ["operation_type"] = operationType
            });
        }
    }
}
```

### Distributed Tracing
```csharp
namespace SovereignsDilemma.Tracing
{
    public class GameActivityTracer
    {
        private readonly ActivitySource _activitySource;

        public GameActivityTracer()
        {
            _activitySource = new ActivitySource("SovereignsDilemma.Game");
        }

        public async Task<T> TraceAsync<T>(string operationName, Func<Activity, Task<T>> operation,
            Dictionary<string, object> tags = null)
        {
            using var activity = _activitySource.StartActivity(operationName);

            if (activity != null && tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value?.ToString());
                }
            }

            try
            {
                var result = await operation(activity);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                throw;
            }
        }

        // Trace political post processing end-to-end
        public async Task<VoterResponse[]> TracePoliticalPostProcessing(
            PoliticalPost post, VoterId[] voterIds)
        {
            return await TraceAsync("political_post_processing", async activity =>
            {
                activity?.SetTag("post.id", post.Id.Value);
                activity?.SetTag("voter.count", voterIds.Length);

                // Trace AI analysis
                var analysis = await TraceAsync("ai_analysis", async childActivity =>
                {
                    childActivity?.SetTag("content.length", post.Content.Length);
                    return await _aiService.AnalyzeContent(post.Content);
                });

                activity?.SetTag("analysis.political_lean", analysis.PoliticalLean.ToString());
                activity?.SetTag("analysis.sentiment", analysis.Sentiment.ToString());

                // Trace voter response generation
                var responses = await TraceAsync("voter_response_generation", async childActivity =>
                {
                    childActivity?.SetTag("voter.count", voterIds.Length);
                    return await _voterService.GenerateResponses(post, voterIds);
                });

                activity?.SetTag("responses.generated", responses.Length);
                return responses;
            });
        }
    }
}
```

### Structured Logging
```csharp
namespace SovereignsDilemma.Logging
{
    public class StructuredLogger
    {
        private readonly ILogger _logger;
        private readonly string _correlationId;

        public StructuredLogger(ILogger logger)
        {
            _logger = logger;
            _correlationId = GenerateCorrelationId();
        }

        public void LogGameEvent(string eventName, object eventData, LogLevel level = LogLevel.Information)
        {
            var logEntry = new
            {
                timestamp = DateTime.UtcNow,
                correlation_id = _correlationId,
                event_name = eventName,
                event_data = eventData,
                game_version = Application.version,
                platform = Application.platform.ToString(),
                unity_version = Application.unityVersion
            };

            _logger.Log(level, "{@LogEntry}", logEntry);
        }

        public void LogUserAction(string userId, string action, object actionData = null)
        {
            LogGameEvent("user_action", new
            {
                user_id = HashUserId(userId),
                action = action,
                action_data = actionData,
                session_id = GetCurrentSessionId()
            });
        }

        public void LogPerformanceIssue(string issueType, object issueData)
        {
            LogGameEvent("performance_issue", new
            {
                issue_type = issueType,
                issue_data = issueData,
                system_info = GetSystemInfo()
            }, LogLevel.Warning);
        }

        public void LogAIServiceInteraction(string operation, TimeSpan duration, bool success,
            string errorMessage = null)
        {
            LogGameEvent("ai_service_interaction", new
            {
                operation = operation,
                duration_ms = duration.TotalMilliseconds,
                success = success,
                error_message = errorMessage,
                circuit_breaker_state = GetCircuitBreakerState()
            });
        }

        private object GetSystemInfo()
        {
            return new
            {
                memory_usage = GC.GetTotalMemory(false),
                fps = 1.0f / Time.deltaTime,
                active_voters = GameState.Current?.ActiveVoterCount ?? 0,
                cpu_cores = SystemInfo.processorCount,
                graphics_memory = SystemInfo.graphicsMemorySize
            };
        }
    }
}
```

## Service Level Objectives (SLOs)

### Game Performance SLOs
```yaml
Game Performance SLOs:
  frame_rate:
    slo: "95% of frames rendered at ≥60 FPS"
    sli: "percentage of frames with render time ≤16.67ms"
    measurement_window: "5 minutes"
    error_budget: "5% of frames below 60 FPS per day"

  memory_usage:
    slo: "Memory usage stays below 1GB for 99% of gameplay sessions"
    sli: "peak memory usage during 60-minute gameplay session"
    measurement_window: "1 hour sessions"
    error_budget: "1% of sessions exceeding memory limit"

  voter_simulation:
    slo: "Voter state updates complete within 100ms for 99% of simulation ticks"
    sli: "voter update processing time"
    measurement_window: "10 minutes"
    error_budget: "1% of updates exceeding 100ms threshold"
```

### External Service SLOs
```yaml
NVIDIA NIM API SLOs:
  response_time:
    slo: "95% of API calls complete within 2 seconds"
    sli: "API response time from request to response"
    measurement_window: "1 hour"
    error_budget: "5% of requests exceeding 2 seconds per hour"

  availability:
    slo: "API available for 99.5% of requests"
    sli: "percentage of successful API responses (non-5xx)"
    measurement_window: "24 hours"
    error_budget: "0.5% error rate per day"

  circuit_breaker:
    slo: "Circuit breaker activates within 30 seconds of detecting failures"
    sli: "time from failure detection to circuit open"
    measurement_window: "failure incidents"
    error_budget: "0 incidents with delayed circuit activation"
```

## Alerting Strategy

### Alert Severity Levels
```yaml
Critical Alerts (P0):
  triggers:
    - Game crashes or becomes unresponsive
    - Data corruption detected
    - Security breach indicators
    - Complete external service failure
  response_time: "5 minutes"
  escalation: "Immediate to on-call engineer"
  channels: ["pagerduty", "slack", "email", "sms"]

High Priority Alerts (P1):
  triggers:
    - FPS drops below 30 for >2 minutes
    - Memory usage exceeds 1.2GB
    - API error rate >10% for >5 minutes
    - Circuit breaker open for >10 minutes
  response_time: "15 minutes"
  escalation: "1 hour to team lead"
  channels: ["slack", "email"]

Medium Priority Alerts (P2):
  triggers:
    - FPS drops below 45 for >5 minutes
    - API response time >3 seconds for >10 minutes
    - Unusual voter behavior patterns detected
    - High error log volume
  response_time: "1 hour"
  escalation: "4 hours to product owner"
  channels: ["slack"]

Informational Alerts (P3):
  triggers:
    - New users registering
    - Feature usage statistics
    - Performance optimization opportunities
    - Scheduled maintenance reminders
  response_time: "Best effort"
  escalation: "None"
  channels: ["dashboard"]
```

### Alert Rules Configuration
```yaml
# Prometheus alert rules example
game_performance_alerts:
  - alert: FrameRateDegradation
    expr: avg_over_time(game_fps[5m]) < 45
    for: 2m
    labels:
      severity: critical
      team: game_engine
    annotations:
      summary: "Game frame rate severely degraded"
      description: "Average FPS is {{ $value }} over the last 5 minutes"

  - alert: MemoryPressure
    expr: game_memory_total_bytes > 1073741824  # 1GB
    for: 1m
    labels:
      severity: warning
      team: game_engine
    annotations:
      summary: "High memory usage detected"
      description: "Memory usage is {{ $value | humanizeBytes }}"

ai_service_alerts:
  - alert: AIServiceDown
    expr: up{job="nvidia_nim"} == 0
    for: 30s
    labels:
      severity: critical
      team: ai_integration
    annotations:
      summary: "NVIDIA NIM service is down"
      description: "AI service has been unavailable for 30 seconds"

  - alert: CircuitBreakerOpen
    expr: ai_circuit_breaker_state == 1
    for: 5m
    labels:
      severity: warning
      team: ai_integration
    annotations:
      summary: "AI service circuit breaker is open"
      description: "Circuit breaker has been open for 5 minutes"
```

## Dashboard Specifications

### Real-time Operations Dashboard
```yaml
Game Health Dashboard:
  refresh_rate: "30 seconds"

  sections:
    system_health:
      widgets:
        - type: "gauge"
          metric: "game_fps"
          title: "Current FPS"
          thresholds: [30, 45, 60]

        - type: "gauge"
          metric: "game_memory_usage_percent"
          title: "Memory Usage"
          thresholds: [70, 85, 95]

        - type: "timeseries"
          metric: "game_active_voters"
          title: "Active Voters"
          timerange: "1h"

    user_engagement:
      widgets:
        - type: "counter"
          metric: "gameplay_posts_created_total"
          title: "Posts Created Today"
          timerange: "24h"

        - type: "heatmap"
          metric: "user_activity_by_hour"
          title: "User Activity Patterns"

    ai_service_health:
      widgets:
        - type: "timeseries"
          metric: "ai_request_duration_seconds"
          title: "AI Response Time"
          percentiles: [50, 95, 99]

        - type: "status"
          metric: "ai_circuit_breaker_state"
          title: "Circuit Breaker Status"

    error_tracking:
      widgets:
        - type: "table"
          metric: "error_rate_by_component"
          title: "Error Rates by Component"

        - type: "log_stream"
          query: "level:ERROR"
          title: "Recent Errors"
          limit: 10
```

### Business Metrics Dashboard
```yaml
Political Simulation Analytics:
  sections:
    simulation_effectiveness:
      widgets:
        - type: "gauge"
          metric: "voter_opinion_change_rate"
          title: "Opinion Change Rate"

        - type: "pie_chart"
          metric: "political_spectrum_distribution"
          title: "Political Spectrum Distribution"

    player_engagement:
      widgets:
        - type: "timeseries"
          metric: "average_session_duration"
          title: "Average Session Duration"

        - type: "funnel"
          metrics: ["posts_created", "responses_received", "political_impact"]
          title: "Engagement Funnel"

    content_analysis:
      widgets:
        - type: "wordcloud"
          metric: "popular_political_topics"
          title: "Trending Political Topics"

        - type: "bar_chart"
          metric: "sentiment_distribution"
          title: "Post Sentiment Distribution"
```

## Log Management Strategy

### Log Levels and Categories
```yaml
Log Configuration:
  levels:
    DEBUG: "Development debugging information"
    INFO: "General application flow and user actions"
    WARN: "Potentially harmful situations or degraded performance"
    ERROR: "Error events that might still allow application to continue"
    FATAL: "Critical errors that will abort the application"

  categories:
    game_engine: "Unity engine events, rendering, physics"
    voter_simulation: "Political simulation logic and voter behavior"
    ai_integration: "NVIDIA NIM API interactions and analysis"
    user_interface: "UI interactions and user experience events"
    data_persistence: "Database operations and data integrity"
    security: "Authentication, authorization, and security events"
    performance: "Performance metrics and optimization events"
```

### Log Retention and Archival
```yaml
Log Retention Policy:
  real_time_logs:
    retention: "7 days"
    location: "Hot storage (SSD)"
    query_performance: "Sub-second"

  historical_logs:
    retention: "90 days"
    location: "Warm storage"
    query_performance: "Few seconds"

  archived_logs:
    retention: "1 year"
    location: "Cold storage"
    query_performance: "Minutes"
    compliance: "GDPR compliant with anonymization"

  log_sampling:
    debug_logs: "1% in production"
    info_logs: "100% always"
    error_logs: "100% always"
    performance_logs: "10% sampled for performance"
```

## Incident Response Integration

### Automated Incident Detection
```csharp
public class IncidentDetectionEngine
{
    private readonly AlertManager _alertManager;
    private readonly TelemetryService _telemetry;

    public async Task ProcessHealthCheckResults(HealthCheckResults results)
    {
        if (results.HasCriticalFailures)
        {
            await CreateIncident(new Incident
            {
                Severity = IncidentSeverity.Critical,
                Title = "Critical system health failure detected",
                Description = results.GetFailureDescription(),
                AffectedSystems = results.FailedSystems,
                DetectedAt = DateTime.UtcNow,
                AutoAssignee = GetOnCallEngineer()
            });
        }
    }

    public async Task AnalyzeTrendingMetrics()
    {
        // Detect anomalies in key metrics
        var anomalies = await _telemetry.DetectAnomalies(new[]
        {
            "game_fps",
            "ai_response_time",
            "memory_usage",
            "error_rate"
        }, TimeSpan.FromHours(24));

        foreach (var anomaly in anomalies.Where(a => a.Severity >= AnomalySeverity.High))
        {
            await CreateIncident(new Incident
            {
                Severity = IncidentSeverity.High,
                Title = $"Performance anomaly detected: {anomaly.MetricName}",
                Description = anomaly.Description,
                DetectedAt = anomaly.DetectedAt,
                AutoAssignee = GetPerformanceEngineer()
            });
        }
    }
}
```

### Runbook Integration
```yaml
Automated Runbook Execution:
  triggers:
    memory_pressure:
      condition: "memory_usage > 85%"
      actions:
        - "trigger_garbage_collection"
        - "reduce_voter_count_temporarily"
        - "notify_performance_team"

    api_circuit_breaker_open:
      condition: "ai_circuit_breaker_state == open"
      actions:
        - "activate_fallback_responses"
        - "check_api_status_page"
        - "escalate_if_duration > 10min"

    frame_rate_degradation:
      condition: "avg_fps < 30 for 5min"
      actions:
        - "collect_performance_profile"
        - "enable_aggressive_optimization"
        - "create_critical_incident"
```

This comprehensive monitoring and observability specification ensures The Sovereign's Dilemma has full visibility into system health, performance, and user experience with automated incident detection and response capabilities.