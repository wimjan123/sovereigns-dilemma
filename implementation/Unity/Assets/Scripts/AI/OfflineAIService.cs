using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

namespace SovereignsDilemma.AI
{
    /// <summary>
    /// Offline AI service providing cached responses and rule-based generation.
    /// Enables seamless offline operation with local fallback mechanisms.
    /// </summary>
    public class OfflineAIService : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool enableOfflineMode = false;
        [SerializeField] private int maxCachedResponses = 1000;
        [SerializeField] private int maxResponseLength = 512;
        [SerializeField] private float responseDelay = 0.5f; // Simulate network delay

        [Header("Rule-Based Generation")]
        [SerializeField] private bool enableRuleBasedGeneration = true;
        [SerializeField] private bool enableTemplateSystem = true;
        [SerializeField] private bool enableMarkovChains = false;

        // Cached responses
        private readonly Dictionary<string, CachedResponse> _responseCache = new Dictionary<string, CachedResponse>();
        private readonly Queue<string> _cacheEvictionQueue = new Queue<string>();

        // Response templates for different content types
        private readonly Dictionary<string, List<ResponseTemplate>> _responseTemplates = new Dictionary<string, List<ResponseTemplate>>();

        // Rule-based generation data
        private readonly Dictionary<string, List<string>> _contextualWords = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> _sentenceStarters = new Dictionary<string, List<string>>();
        private readonly List<string> _politicalTerms = new List<string>();

        // Statistics
        private int _cacheHits = 0;
        private int _cacheMisses = 0;
        private int _generatedResponses = 0;
        private float _averageResponseTime = 0f;

        // Performance tracking
        private readonly ProfilerMarker _generateMarker = new("OfflineAI.Generate");
        private readonly ProfilerMarker _cacheMarker = new("OfflineAI.Cache");

        // Events
        public event Action<bool> OnOfflineModeChanged;
        public event Action<OfflineAIStatistics> OnStatisticsUpdated;

        public bool IsOfflineModeEnabled => enableOfflineMode;
        public float CacheHitRate => _cacheHits + _cacheMisses > 0 ? (float)_cacheHits / (_cacheHits + _cacheMisses) : 0f;

        private void Awake()
        {
            InitializeOfflineService();
            LoadCachedData();
        }

        private void Start()
        {
            SetupResponseTemplates();
            SetupRuleBasedData();
        }

        #region Initialization

        private void InitializeOfflineService()
        {
            enableOfflineMode = PlayerPrefs.GetInt("OfflineMode", 0) == 1;

            Debug.Log($"OfflineAIService initialized - Mode: {(enableOfflineMode ? "Enabled" : "Disabled")}");
        }

        private void LoadCachedData()
        {
            // In production, this would load from persistent storage
            // For now, we'll generate some sample cached responses
            GenerateSampleCache();
        }

        private void SetupResponseTemplates()
        {
            // Political news templates
            _responseTemplates["political_news"] = new List<ResponseTemplate>
            {
                new ResponseTemplate
                {
                    Template = "Breaking: {party} announces {policy_type} focusing on {topic}",
                    Variables = new Dictionary<string, List<string>>
                    {
                        { "party", new List<string> { "Coalition Government", "Progressive Alliance", "Conservative Party", "Green Movement" } },
                        { "policy_type", new List<string> { "new legislation", "policy reform", "initiative", "framework" } },
                        { "topic", new List<string> { "climate action", "economic growth", "healthcare", "education", "housing" } }
                    }
                },
                new ResponseTemplate
                {
                    Template = "{politician} addresses concerns about {issue} in recent statement",
                    Variables = new Dictionary<string, List<string>>
                    {
                        { "politician", new List<string> { "Prime Minister", "Finance Minister", "Climate Secretary", "Health Minister" } },
                        { "issue", new List<string> { "rising energy costs", "healthcare waiting times", "housing shortage", "educational funding" } }
                    }
                }
            };

            // Opinion piece templates
            _responseTemplates["opinion"] = new List<ResponseTemplate>
            {
                new ResponseTemplate
                {
                    Template = "Opinion: Why {topic} should be a priority for the next election",
                    Variables = new Dictionary<string, List<string>>
                    {
                        { "topic", new List<string> { "environmental policy", "economic stability", "social equality", "digital innovation" } }
                    }
                },
                new ResponseTemplate
                {
                    Template = "Analysis: The impact of {policy} on {demographic} communities",
                    Variables = new Dictionary<string, List<string>>
                    {
                        { "policy", new List<string> { "tax reform", "healthcare changes", "education policy", "housing initiative" } },
                        { "demographic", new List<string> { "young", "elderly", "working", "rural", "urban" } }
                    }
                }
            };

            // Crisis response templates
            _responseTemplates["crisis"] = new List<ResponseTemplate>
            {
                new ResponseTemplate
                {
                    Template = "Emergency response: Government implements {measure} following {crisis_type}",
                    Variables = new Dictionary<string, List<string>>
                    {
                        { "measure", new List<string> { "emergency protocols", "support package", "relief program", "coordination efforts" } },
                        { "crisis_type", new List<string> { "natural disaster", "economic disruption", "public health concern", "infrastructure failure" } }
                    }
                }
            };
        }

        private void SetupRuleBasedData()
        {
            // Political terminology
            _politicalTerms.AddRange(new string[]
            {
                "coalition", "parliament", "legislation", "policy", "reform", "initiative",
                "democracy", "governance", "constituents", "budget", "taxation", "regulation",
                "sustainability", "innovation", "equality", "justice", "prosperity", "security"
            });

            // Context-specific word lists
            _contextualWords["economy"] = new List<string>
            {
                "growth", "inflation", "employment", "investment", "trade", "market",
                "fiscal", "monetary", "budget", "deficit", "surplus", "GDP"
            };

            _contextualWords["environment"] = new List<string>
            {
                "climate", "sustainable", "renewable", "emissions", "green", "carbon",
                "conservation", "biodiversity", "pollution", "ecosystem", "energy"
            };

            _contextualWords["society"] = new List<string>
            {
                "community", "equality", "justice", "healthcare", "education", "housing",
                "welfare", "diversity", "inclusion", "rights", "freedom", "democracy"
            };

            // Sentence starters by type
            _sentenceStarters["news"] = new List<string>
            {
                "Breaking news:", "Latest update:", "Reports indicate that", "According to sources",
                "In a recent development", "Officials confirm that", "New information suggests"
            };

            _sentenceStarters["opinion"] = new List<string>
            {
                "In my view,", "It's clear that", "We must consider", "The evidence shows",
                "Critics argue that", "Supporters believe", "Many experts suggest"
            };

            _sentenceStarters["analysis"] = new List<string>
            {
                "Analysis reveals", "Data indicates", "Research shows", "Studies suggest",
                "Trends point to", "Evidence demonstrates", "Statistics confirm"
            };
        }

        private void GenerateSampleCache()
        {
            // Generate sample cached responses for common prompts
            var samplePrompts = new[]
            {
                "Generate a political news headline",
                "Create an opinion piece about climate policy",
                "Write a crisis response statement",
                "Generate voter sentiment analysis",
                "Create social media post about economy"
            };

            foreach (string prompt in samplePrompts)
            {
                string response = GenerateRuleBasedResponse(prompt);
                CacheResponse(prompt, response);
            }
        }

        #endregion

        #region Public Interface

        public void SetOfflineMode(bool enabled)
        {
            enableOfflineMode = enabled;
            PlayerPrefs.SetInt("OfflineMode", enabled ? 1 : 0);
            PlayerPrefs.Save();

            OnOfflineModeChanged?.Invoke(enabled);
            Debug.Log($"Offline mode {(enabled ? "enabled" : "disabled")}");
        }

        public string GenerateResponse(string prompt, string contentType = "general")
        {
            using (_generateMarker.Auto())
            {
                float startTime = Time.realtimeSinceStartup;

                string response = GetCachedResponse(prompt);

                if (response != null)
                {
                    _cacheHits++;
                }
                else
                {
                    _cacheMisses++;
                    response = GenerateNewResponse(prompt, contentType);
                    CacheResponse(prompt, response);
                }

                float responseTime = Time.realtimeSinceStartup - startTime;
                UpdateStatistics(responseTime);

                return response;
            }
        }

        public void PreloadResponses(List<string> prompts, string contentType = "general")
        {
            foreach (string prompt in prompts)
            {
                if (!HasCachedResponse(prompt))
                {
                    string response = GenerateNewResponse(prompt, contentType);
                    CacheResponse(prompt, response);
                }
            }

            Debug.Log($"Preloaded {prompts.Count} responses");
        }

        public void ClearCache()
        {
            _responseCache.Clear();
            _cacheEvictionQueue.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;

            Debug.Log("Response cache cleared");
        }

        public OfflineAIStatistics GetStatistics()
        {
            return new OfflineAIStatistics
            {
                CacheHitRate = CacheHitRate,
                CachedResponseCount = _responseCache.Count,
                GeneratedResponseCount = _generatedResponses,
                AverageResponseTime = _averageResponseTime,
                IsOfflineModeEnabled = enableOfflineMode
            };
        }

        #endregion

        #region Response Generation

        private string GenerateNewResponse(string prompt, string contentType)
        {
            if (enableTemplateSystem && _responseTemplates.ContainsKey(contentType))
            {
                return GenerateTemplateResponse(contentType);
            }
            else if (enableRuleBasedGeneration)
            {
                return GenerateRuleBasedResponse(prompt);
            }
            else
            {
                return GenerateFallbackResponse(prompt);
            }
        }

        private string GenerateTemplateResponse(string contentType)
        {
            var templates = _responseTemplates[contentType];
            var template = templates[UnityEngine.Random.Range(0, templates.Count)];

            string response = template.Template;

            foreach (var variable in template.Variables)
            {
                string placeholder = "{" + variable.Key + "}";
                if (response.Contains(placeholder))
                {
                    string value = variable.Value[UnityEngine.Random.Range(0, variable.Value.Count)];
                    response = response.Replace(placeholder, value);
                }
            }

            return response;
        }

        private string GenerateRuleBasedResponse(string prompt)
        {
            _generatedResponses++;

            // Analyze prompt to determine context
            string context = DetermineContext(prompt);
            string responseType = DetermineResponseType(prompt);

            // Generate response based on context and type
            string starter = GetRandomStarter(responseType);
            string content = GenerateContextualContent(context);
            string conclusion = GenerateConclusion(context);

            return $"{starter} {content} {conclusion}".Trim();
        }

        private string DetermineContext(string prompt)
        {
            string lowerPrompt = prompt.ToLower();

            if (lowerPrompt.Contains("economy") || lowerPrompt.Contains("economic") || lowerPrompt.Contains("financial"))
                return "economy";
            else if (lowerPrompt.Contains("environment") || lowerPrompt.Contains("climate") || lowerPrompt.Contains("green"))
                return "environment";
            else if (lowerPrompt.Contains("social") || lowerPrompt.Contains("healthcare") || lowerPrompt.Contains("education"))
                return "society";
            else
                return "general";
        }

        private string DetermineResponseType(string prompt)
        {
            string lowerPrompt = prompt.ToLower();

            if (lowerPrompt.Contains("news") || lowerPrompt.Contains("headline") || lowerPrompt.Contains("breaking"))
                return "news";
            else if (lowerPrompt.Contains("opinion") || lowerPrompt.Contains("view") || lowerPrompt.Contains("think"))
                return "opinion";
            else if (lowerPrompt.Contains("analysis") || lowerPrompt.Contains("study") || lowerPrompt.Contains("research"))
                return "analysis";
            else
                return "general";
        }

        private string GetRandomStarter(string responseType)
        {
            if (_sentenceStarters.ContainsKey(responseType))
            {
                var starters = _sentenceStarters[responseType];
                return starters[UnityEngine.Random.Range(0, starters.Count)];
            }

            return "Recent developments show that";
        }

        private string GenerateContextualContent(string context)
        {
            List<string> contentWords = new List<string>();

            if (_contextualWords.ContainsKey(context))
            {
                contentWords.AddRange(_contextualWords[context]);
            }

            contentWords.AddRange(_politicalTerms);

            // Generate content using selected words
            string[] templates = {
                "the government's focus on {0} and {1} reflects changing priorities",
                "stakeholders emphasize the importance of {0} in addressing {1} concerns",
                "new initiatives target {0} while maintaining {1} standards",
                "policy makers balance {0} considerations with {1} requirements"
            };

            string template = templates[UnityEngine.Random.Range(0, templates.Length)];
            string word1 = contentWords[UnityEngine.Random.Range(0, contentWords.Count)];
            string word2 = contentWords[UnityEngine.Random.Range(0, contentWords.Count)];

            return string.Format(template, word1, word2);
        }

        private string GenerateConclusion(string context)
        {
            string[] conclusions = {
                "This development is expected to influence upcoming policy decisions.",
                "Further updates will be provided as the situation develops.",
                "Stakeholders continue to monitor the implementation progress.",
                "Public response has been generally positive with some concerns raised."
            };

            return conclusions[UnityEngine.Random.Range(0, conclusions.Length)];
        }

        private string GenerateFallbackResponse(string prompt)
        {
            return $"Based on current political dynamics, this matter requires careful consideration of multiple stakeholder perspectives. " +
                   $"The government continues to evaluate options while maintaining focus on public interests and long-term sustainability.";
        }

        #endregion

        #region Cache Management

        private string GetCachedResponse(string prompt)
        {
            using (_cacheMarker.Auto())
            {
                if (_responseCache.ContainsKey(prompt))
                {
                    var cached = _responseCache[prompt];

                    // Check if cache entry is still valid
                    if (DateTime.UtcNow - cached.Timestamp < TimeSpan.FromDays(7)) // 7-day cache expiry
                    {
                        cached.AccessCount++;
                        cached.LastAccessed = DateTime.UtcNow;
                        return cached.Response;
                    }
                    else
                    {
                        _responseCache.Remove(prompt);
                    }
                }

                return null;
            }
        }

        private void CacheResponse(string prompt, string response)
        {
            if (_responseCache.Count >= maxCachedResponses)
            {
                EvictOldestCacheEntry();
            }

            var cacheEntry = new CachedResponse
            {
                Prompt = prompt,
                Response = response,
                Timestamp = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                AccessCount = 1
            };

            _responseCache[prompt] = cacheEntry;
            _cacheEvictionQueue.Enqueue(prompt);
        }

        private void EvictOldestCacheEntry()
        {
            if (_cacheEvictionQueue.Count > 0)
            {
                string oldestPrompt = _cacheEvictionQueue.Dequeue();
                _responseCache.Remove(oldestPrompt);
            }
        }

        private bool HasCachedResponse(string prompt)
        {
            return _responseCache.ContainsKey(prompt);
        }

        #endregion

        #region Statistics

        private void UpdateStatistics(float responseTime)
        {
            // Update average response time
            int totalResponses = _cacheHits + _cacheMisses;
            _averageResponseTime = (_averageResponseTime * (totalResponses - 1) + responseTime) / totalResponses;

            // Emit statistics update event
            OnStatisticsUpdated?.Invoke(GetStatistics());
        }

        #endregion

        #region Utility Methods

        public void SimulateNetworkDelay()
        {
            // Add artificial delay to simulate network response time
            if (responseDelay > 0f)
            {
                System.Threading.Thread.Sleep((int)(responseDelay * 1000));
            }
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        public class CachedResponse
        {
            public string Prompt;
            public string Response;
            public DateTime Timestamp;
            public DateTime LastAccessed;
            public int AccessCount;
        }

        [System.Serializable]
        public class ResponseTemplate
        {
            public string Template;
            public Dictionary<string, List<string>> Variables;
        }

        [System.Serializable]
        public struct OfflineAIStatistics
        {
            public float CacheHitRate;
            public int CachedResponseCount;
            public int GeneratedResponseCount;
            public float AverageResponseTime;
            public bool IsOfflineModeEnabled;
        }

        #endregion
    }
}