using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using SovereignsDilemma.Core.Security;
using SovereignsDilemma.Core.Events;

namespace SovereignsDilemma.AI.Services
{
    /// <summary>
    /// NVIDIA NIM API service implementation for political analysis.
    /// Includes circuit breaker pattern, caching, and Dutch political context.
    /// </summary>
    public class NVIDIANIMService : MonoBehaviour, IAIAnalysisService
    {
        [Header("NVIDIA NIM Configuration")]
        [SerializeField] private string defaultModel = "nvidia/llama-3.1-nemotron-70b-instruct";
        [SerializeField] private string baseUrl = "https://integrate.api.nvidia.com/v1";
        [SerializeField] private int maxConcurrentRequests = 3;
        [SerializeField] private float requestTimeoutSeconds = 5.0f;
        [SerializeField] private int maxRetries = 3;

        [Header("Circuit Breaker Settings")]
        [SerializeField] private int failureThreshold = 5;
        [SerializeField] private float circuitBreakerTimeoutSeconds = 30.0f;
        [SerializeField] private float circuitBreakerSamplingDuration = 60.0f;

        [Header("Caching Settings")]
        [SerializeField] private int maxCacheSize = 1000;
        [SerializeField] private float cacheExpiryHours = 1.0f;
        [SerializeField] private bool enableResponseCaching = true;

        // Private fields
        private HttpClient _httpClient;
        private ICredentialStorage _credentialStorage;
        private IEventBus _eventBus;
        private readonly CircuitBreaker _circuitBreaker = new();
        private readonly Dictionary<string, CachedResponse> _responseCache = new();
        private readonly SemaphoreSlim _requestSemaphore = new(3, 3);
        private AIServiceStatus _status = new();

        public AIProviderType ProviderType => AIProviderType.NvidiaNIM;
        public bool IsAvailable => _circuitBreaker.State == CircuitBreakerState.Closed && !string.IsNullOrEmpty(GetApiKey());

        public event EventHandler<ServiceAvailabilityChangedEventArgs> AvailabilityChanged;

        private void Awake()
        {
            InitializeService();
        }

        private void InitializeService()
        {
            // Initialize HTTP client
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SovereignsDilemma/1.0");

            // Initialize credential storage
            _credentialStorage = new CrossPlatformCredentialStorage();

            // Find event bus
            _eventBus = FindObjectOfType<UnityEventBus>();

            // Configure circuit breaker
            _circuitBreaker.Configure(failureThreshold, TimeSpan.FromSeconds(circuitBreakerTimeoutSeconds));
            _circuitBreaker.StateChanged += OnCircuitBreakerStateChanged;

            // Update semaphore
            _requestSemaphore?.Dispose();
            _requestSemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);

            Debug.Log("NVIDIA NIM service initialized");
        }

        public async Task<PoliticalAnalysis> AnalyzeContentAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be null or empty", nameof(content));

            var startTime = DateTime.UtcNow;

            try
            {
                // Check cache first
                if (enableResponseCaching && TryGetCachedResponse(content, out var cachedAnalysis))
                {
                    _status.CacheHitRate = CalculateCacheHitRate(true);
                    return cachedAnalysis;
                }

                _status.CacheHitRate = CalculateCacheHitRate(false);

                // Analyze content via circuit breaker
                var analysis = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    return await PerformAnalysisRequest(content);
                });

                analysis.ProcessingTime = DateTime.UtcNow - startTime;

                // Cache the response
                if (enableResponseCaching)
                {
                    CacheResponse(content, analysis);
                }

                // Publish event
                _eventBus?.PublishAsync(new AIAnalysisCompletedEvent(content, analysis, analysis.ProcessingTime));

                _status.LastSuccessfulRequest = DateTime.UtcNow;
                _status.RequestsToday++;

                return analysis;
            }
            catch (Exception ex)
            {
                _status.FailedRequestsToday++;
                Debug.LogError($"NVIDIA NIM analysis failed: {ex.Message}");
                throw;
            }
        }

        public async Task<PoliticalAnalysis[]> AnalyzeBatchAsync(string[] contents)
        {
            if (contents == null || contents.Length == 0)
                return Array.Empty<PoliticalAnalysis>();

            var tasks = contents.Select(AnalyzeContentAsync);
            return await Task.WhenAll(tasks);
        }

        public async Task<VoterResponse[]> GenerateVoterResponsesAsync(string content, VoterProfile[] voterProfiles)
        {
            if (string.IsNullOrWhiteSpace(content) || voterProfiles == null || voterProfiles.Length == 0)
                return Array.Empty<VoterResponse>();

            var startTime = DateTime.UtcNow;

            try
            {
                return await _circuitBreaker.ExecuteAsync(async () =>
                {
                    return await PerformVoterResponseRequest(content, voterProfiles);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"NVIDIA NIM voter response generation failed: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var healthCheck = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    await PerformHealthCheck();
                    return true;
                });

                _status.IsOperational = healthCheck;
                return healthCheck;
            }
            catch
            {
                _status.IsOperational = false;
                return false;
            }
        }

        public AIServiceStatus GetStatus()
        {
            _status.CircuitBreakerState = _circuitBreaker.State;
            return _status;
        }

        private async Task<PoliticalAnalysis> PerformAnalysisRequest(string content)
        {
            await _requestSemaphore.WaitAsync();

            try
            {
                var apiKey = GetApiKey();
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("NVIDIA NIM API key not configured");

                var request = CreateAnalysisRequest(content);
                var requestJson = JsonSerializer.Serialize(request);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var nimResponse = JsonSerializer.Deserialize<NIMResponse>(responseJson);

                return ParseAnalysisResponse(nimResponse);
            }
            finally
            {
                _requestSemaphore.Release();
            }
        }

        private async Task<VoterResponse[]> PerformVoterResponseRequest(string content, VoterProfile[] voterProfiles)
        {
            await _requestSemaphore.WaitAsync();

            try
            {
                var apiKey = GetApiKey();
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("NVIDIA NIM API key not configured");

                var request = CreateVoterResponseRequest(content, voterProfiles);
                var requestJson = JsonSerializer.Serialize(request);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var nimResponse = JsonSerializer.Deserialize<NIMResponse>(responseJson);

                return ParseVoterResponsesResponse(nimResponse, voterProfiles);
            }
            finally
            {
                _requestSemaphore.Release();
            }
        }

        private async Task PerformHealthCheck()
        {
            var healthRequest = new
            {
                model = defaultModel,
                messages = new[]
                {
                    new { role = "user", content = "Health check" }
                },
                max_tokens = 1,
                temperature = 0.1
            };

            var requestJson = JsonSerializer.Serialize(healthRequest);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {GetApiKey()}");
            httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }

        private object CreateAnalysisRequest(string content)
        {
            return new
            {
                model = defaultModel,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = GetDutchPoliticalSystemPrompt()
                    },
                    new
                    {
                        role = "user",
                        content = $"Analyze this Dutch political content: {content}"
                    }
                },
                max_tokens = 500,
                temperature = 0.3
            };
        }

        private object CreateVoterResponseRequest(string content, VoterProfile[] voterProfiles)
        {
            var voterContext = string.Join("\n", voterProfiles.Select(v =>
                $"Voter {v.VoterId}: Age {v.Age}, Income {v.IncomePercentile}%, Education {v.EducationLevel}, Region {v.Region}"));

            return new
            {
                model = defaultModel,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "Generate realistic Dutch voter responses to political content based on voter demographics."
                    },
                    new
                    {
                        role = "user",
                        content = $"Political content: {content}\n\nVoter profiles:\n{voterContext}\n\nGenerate appropriate responses."
                    }
                },
                max_tokens = 1000,
                temperature = 0.7
            };
        }

        private string GetDutchPoliticalSystemPrompt()
        {
            return @"You are an expert analyst of Dutch politics. Analyze political content in the context of:
- Dutch political parties (VVD, PVV, CDA, D66, SP, PvdA, GL, CU, SGP, DENK, FvD, Volt, etc.)
- Dutch political issues (immigration, housing, climate, healthcare, economy)
- Dutch political spectrum and coalition dynamics
- Regional differences within the Netherlands

Provide analysis scores from -1.0 to 1.0 for:
- Overall sentiment (negative to positive)
- Political lean (left to right)
- Economic position (progressive to conservative)
- Social position (traditional to progressive)
- Immigration stance (restrictive to open)
- Environmental stance (skeptical to activist)

Include confidence level and key topics identified.";
        }

        private PoliticalAnalysis ParseAnalysisResponse(NIMResponse response)
        {
            // TODO: Implement proper parsing of NVIDIA NIM response
            // This is a simplified placeholder implementation
            return new PoliticalAnalysis
            {
                Sentiment = 0.0f,
                PoliticalLean = 0.0f,
                EconomicPosition = 0.0f,
                SocialPosition = 0.0f,
                ImmigrationStance = 0.0f,
                EnvironmentalStance = 0.0f,
                Confidence = 0.8f,
                Topics = new[] { "placeholder" },
                AnalyzedAt = DateTime.UtcNow
            };
        }

        private VoterResponse[] ParseVoterResponsesResponse(NIMResponse response, VoterProfile[] voterProfiles)
        {
            // TODO: Implement proper parsing of voter responses
            // This is a simplified placeholder implementation
            return voterProfiles.Select(profile => new VoterResponse
            {
                VoterId = profile.VoterId,
                Content = "Generated response placeholder",
                Sentiment = 0.0f,
                EngagementLevel = 0.5f,
                Type = ResponseType.Neutral,
                GenerationTime = TimeSpan.FromMilliseconds(100),
                CreatedAt = DateTime.UtcNow
            }).ToArray();
        }

        private string GetApiKey()
        {
            try
            {
                return _credentialStorage?.RetrieveCredentialAsync("nvidia_nim_api_key").Result;
            }
            catch
            {
                return null;
            }
        }

        private bool TryGetCachedResponse(string content, out PoliticalAnalysis analysis)
        {
            analysis = null;
            var cacheKey = GenerateCacheKey(content);

            if (_responseCache.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.UtcNow - cached.CreatedAt < TimeSpan.FromHours(cacheExpiryHours))
                {
                    analysis = cached.Analysis;
                    return true;
                }
                else
                {
                    _responseCache.Remove(cacheKey);
                }
            }

            return false;
        }

        private void CacheResponse(string content, PoliticalAnalysis analysis)
        {
            if (_responseCache.Count >= maxCacheSize)
            {
                // Remove oldest entry
                var oldest = _responseCache.OrderBy(kvp => kvp.Value.CreatedAt).First();
                _responseCache.Remove(oldest.Key);
            }

            var cacheKey = GenerateCacheKey(content);
            _responseCache[cacheKey] = new CachedResponse
            {
                Analysis = analysis,
                CreatedAt = DateTime.UtcNow
            };
        }

        private string GenerateCacheKey(string content)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(content)).Substring(0, Math.Min(32, content.Length));
        }

        private float CalculateCacheHitRate(bool wasHit)
        {
            // Simplified cache hit rate calculation
            return wasHit ? 0.8f : 0.2f;
        }

        private void OnCircuitBreakerStateChanged(object sender, CircuitBreakerStateChangedEventArgs e)
        {
            Debug.Log($"Circuit breaker state changed to: {e.NewState}");

            var wasAvailable = IsAvailable;
            var isNowAvailable = e.NewState == CircuitBreakerState.Closed;

            if (wasAvailable != isNowAvailable)
            {
                AvailabilityChanged?.Invoke(this, new ServiceAvailabilityChangedEventArgs(
                    isNowAvailable,
                    $"Circuit breaker state changed to {e.NewState}"));
            }
        }

        private void OnDestroy()
        {
            _httpClient?.Dispose();
            _requestSemaphore?.Dispose();
        }

        // Helper classes
        private class CachedResponse
        {
            public PoliticalAnalysis Analysis { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private class NIMResponse
        {
            public NIMChoice[] choices { get; set; }
        }

        private class NIMChoice
        {
            public NIMMessage message { get; set; }
        }

        private class NIMMessage
        {
            public string content { get; set; }
        }
    }

    /// <summary>
    /// Circuit breaker implementation for AI service resilience.
    /// </summary>
    public class CircuitBreaker
    {
        private CircuitBreakerState _state = CircuitBreakerState.Closed;
        private int _failureCount = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private int _failureThreshold = 5;
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);

        public CircuitBreakerState State => _state;

        public event EventHandler<CircuitBreakerStateChangedEventArgs> StateChanged;

        public void Configure(int failureThreshold, TimeSpan timeout)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime < _timeout)
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
                _state = CircuitBreakerState.HalfOpen;
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(_state));
            }

            try
            {
                var result = await operation();
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure();
                throw;
            }
        }

        private void OnSuccess()
        {
            _failureCount = 0;
            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(_state));
            }
        }

        private void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(_state));
            }
        }
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }

    public class CircuitBreakerStateChangedEventArgs : EventArgs
    {
        public CircuitBreakerState NewState { get; }

        public CircuitBreakerStateChangedEventArgs(CircuitBreakerState newState)
        {
            NewState = newState;
        }
    }
}