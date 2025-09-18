using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.AI.Services;
using SovereignsDilemma.Core.Events;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// System that processes AI-driven voter behavior changes.
    /// Batches voters for AI analysis and applies behavioral influences.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VoterBehaviorSystem))]
    public partial class AIBehaviorInfluenceSystem : SystemBase
    {
        private static readonly ProfilerMarker AIAnalysisMarker = PerformanceProfiler.PoliticalAnalysisMarker;

        private IAIAnalysisService _aiService;
        private IEventBus _eventBus;
        private EntityQuery _votersNeedingAnalysis;
        private EntityQuery _representativeVoters;

        // AI request batching and caching
        private NativeList<Entity> _batchedVoters;
        private Dictionary<uint, AIInfluenceData> _analysisCache;
        private float _lastAIRequestTime;
        private const float AI_REQUEST_INTERVAL = 5.0f; // Minimum 5 seconds between AI requests
        private const int MAX_BATCH_SIZE = 50; // Process max 50 voters per AI request
        private const int REPRESENTATIVE_SAMPLE_SIZE = 10; // Use 10 representative voters

        protected override void OnCreate()
        {
            // Find AI service
            _aiService = FindObjectOfType<NVIDIANIMService>();
            _eventBus = FindObjectOfType<UnityEventBus>();

            // Create entity queries
            _votersNeedingAnalysis = GetEntityQuery(
                ComponentType.ReadOnly<VoterData>(),
                ComponentType.ReadWrite<PoliticalOpinion>(),
                ComponentType.ReadWrite<BehaviorState>(),
                ComponentType.ReadWrite<AIAnalysisCache>(),
                ComponentType.Exclude<AIAnalysisInProgress>()
            );

            _representativeVoters = GetEntityQuery(
                ComponentType.ReadOnly<VoterData>(),
                ComponentType.ReadOnly<PoliticalOpinion>(),
                ComponentType.ReadOnly<BehaviorState>(),
                ComponentType.ReadOnly<AIAnalysisCache>()
            );

            _batchedVoters = new NativeList<Entity>(MAX_BATCH_SIZE, Allocator.Persistent);
            _analysisCache = new Dictionary<uint, AIInfluenceData>();

            RequireForUpdate(_votersNeedingAnalysis);
        }

        protected override void OnUpdate()
        {
            using (AIAnalysisMarker.Auto())
            {
                var currentTime = Time.ElapsedTime;

                // Only request AI analysis at intervals to avoid overwhelming the service
                if (currentTime - _lastAIRequestTime < AI_REQUEST_INTERVAL)
                    return;

                // Check if AI service is available
                if (_aiService == null || !_aiService.IsAvailable)
                    return;

                // Process voters needing AI analysis
                ProcessVotersNeedingAnalysis();

                // Apply cached analysis results to similar voters
                ApplyCachedAnalysisToSimilarVoters();

                _lastAIRequestTime = (float)currentTime;
            }
        }

        protected override void OnDestroy()
        {
            if (_batchedVoters.IsCreated)
                _batchedVoters.Dispose();
        }

        /// <summary>
        /// Identifies voters needing AI analysis and processes them in batches.
        /// </summary>
        private void ProcessVotersNeedingAnalysis()
        {
            _batchedVoters.Clear();

            // Collect voters that need analysis
            Entities
                .WithEntityQueryOptions(EntityQueryOptions.FilterWriteGroup)
                .ForEach((Entity entity, ref AIAnalysisCache cache, in VoterData voterData, in BehaviorState behavior) =>
                {
                    var currentFrame = (uint)UnityEngine.Time.frameCount;

                    // Check if voter needs fresh analysis
                    bool needsAnalysis = false;

                    if ((cache.Flags & AnalysisFlags.HasCachedData) == 0)
                    {
                        needsAnalysis = true; // No cached data
                    }
                    else if (currentFrame - cache.CachedAtFrame > 18000) // 5 minutes at 60 FPS
                    {
                        cache.Flags |= AnalysisFlags.NeedsRefresh;
                        needsAnalysis = true;
                    }
                    else if ((behavior.Flags & BehaviorFlags.IsVolatile) != 0 && currentFrame - cache.CachedAtFrame > 3600) // 1 minute for volatile voters
                    {
                        needsAnalysis = true;
                    }

                    if (needsAnalysis && _batchedVoters.Length < MAX_BATCH_SIZE)
                    {
                        _batchedVoters.Add(entity);

                        // Mark as analysis in progress
                        cache.Flags |= AnalysisFlags.AnalysisInProgress;
                    }
                }).WithoutBurst().Run();

            // Process batch if we have voters to analyze
            if (_batchedVoters.Length > 0)
            {
                ProcessAIAnalysisBatch();
            }
        }

        /// <summary>
        /// Sends batch of voters to AI service for behavioral analysis.
        /// </summary>
        private async void ProcessAIAnalysisBatch()
        {
            try
            {
                // Select representative voters from the batch for AI analysis
                var representatives = SelectRepresentativeVoters(_batchedVoters.AsArray());

                if (representatives.Count == 0)
                    return;

                // Create voter profiles for AI analysis
                var voterProfiles = CreateVoterProfiles(representatives);

                // Generate political content for analysis (simulate current political climate)
                var politicalContent = GenerateCurrentPoliticalContent();

                // Request AI analysis
                var responses = await _aiService.GenerateVoterResponsesAsync(politicalContent, voterProfiles);

                // Process AI responses and update voter behaviors
                ProcessAIResponses(representatives, responses, politicalContent);

                // Apply results to similar voters in the batch
                ApplyAnalysisToSimilarVoters(representatives, _batchedVoters.AsArray());
            }
            catch (Exception ex)
            {
                Debug.LogError($"AI analysis batch processing failed: {ex.Message}");

                // Mark analysis as failed for all voters in batch
                foreach (var entity in _batchedVoters)
                {
                    if (EntityManager.Exists(entity))
                    {
                        var cache = EntityManager.GetComponentData<AIAnalysisCache>(entity);
                        cache.Flags &= ~AnalysisFlags.AnalysisInProgress;
                        EntityManager.SetComponentData(entity, cache);
                    }
                }
            }
        }

        /// <summary>
        /// Selects representative voters from a batch for AI analysis.
        /// Uses clustering to reduce API calls while maintaining behavioral diversity.
        /// </summary>
        private List<Entity> SelectRepresentativeVoters(NativeArray<Entity> voterBatch)
        {
            var representatives = new List<Entity>();
            var voterClusters = new Dictionary<VoterClusterKey, List<Entity>>();

            // Cluster voters by key characteristics
            foreach (var entity in voterBatch)
            {
                if (!EntityManager.Exists(entity))
                    continue;

                var voterData = EntityManager.GetComponentData<VoterData>(entity);
                var opinion = EntityManager.GetComponentData<PoliticalOpinion>(entity);
                var behavior = EntityManager.GetComponentData<BehaviorState>(entity);

                var clusterKey = new VoterClusterKey
                {
                    AgeGroup = voterData.Age / 15, // Group by 15-year age brackets
                    EducationLevel = voterData.EducationLevel,
                    IsUrban = voterData.IsUrban,
                    EconomicPosition = opinion.EconomicPosition / 25, // Group by economic quartiles
                    SocialPosition = opinion.SocialPosition / 25,
                    EngagementLevel = (behavior.Flags & BehaviorFlags.IsEngaged) != 0 ? 1 : 0
                };

                if (!voterClusters.ContainsKey(clusterKey))
                    voterClusters[clusterKey] = new List<Entity>();

                voterClusters[clusterKey].Add(entity);
            }

            // Select one representative from each cluster
            foreach (var cluster in voterClusters.Values)
            {
                if (cluster.Count > 0)
                {
                    // Select the voter with highest influence score as representative
                    var representative = cluster
                        .OrderByDescending(e => EntityManager.GetComponentData<SocialNetwork>(e).InfluenceScore)
                        .First();

                    representatives.Add(representative);

                    // Mark as representative
                    var cache = EntityManager.GetComponentData<AIAnalysisCache>(representative);
                    cache.Flags |= AnalysisFlags.Representative;
                    EntityManager.SetComponentData(representative, cache);

                    if (representatives.Count >= REPRESENTATIVE_SAMPLE_SIZE)
                        break;
                }
            }

            return representatives;
        }

        /// <summary>
        /// Creates voter profiles for AI analysis.
        /// </summary>
        private VoterProfile[] CreateVoterProfiles(List<Entity> voterEntities)
        {
            var profiles = new VoterProfile[voterEntities.Count];

            for (int i = 0; i < voterEntities.Count; i++)
            {
                var entity = voterEntities[i];
                var voterData = EntityManager.GetComponentData<VoterData>(entity);
                var opinion = EntityManager.GetComponentData<PoliticalOpinion>(entity);

                profiles[i] = new VoterProfile
                {
                    VoterId = voterData.VoterId.ToString(),
                    Age = voterData.Age,
                    IncomePercentile = voterData.IncomePercentile,
                    EducationLevel = voterData.EducationLevel,
                    Region = GetRegionName(voterData.Region),
                    IsUrban = voterData.IsUrban,
                    CurrentPosition = new PoliticalSpectrum
                    {
                        Economic = opinion.EconomicPosition,
                        Social = opinion.SocialPosition,
                        Immigration = opinion.ImmigrationStance,
                        Environment = opinion.EnvironmentalStance
                    }
                };
            }

            return profiles;
        }

        /// <summary>
        /// Generates current political content for AI analysis.
        /// Simulates realistic Dutch political scenarios.
        /// </summary>
        private string GenerateCurrentPoliticalContent()
        {
            var scenarios = new[]
            {
                "VVD announces new housing policy targeting young professionals in major cities",
                "PVV proposes stricter immigration controls following recent EU migration data",
                "D66 introduces climate legislation requiring 50% emission reduction by 2030",
                "Government coalition discusses healthcare budget increases amid rising costs",
                "New study shows Dutch income inequality reaching 10-year high",
                "EU agricultural policy changes affect Dutch farming communities",
                "Amsterdam housing crisis sparks debate over rent control measures",
                "Energy transition costs spark political debate over consumer burden"
            };

            var random = new Unity.Mathematics.Random((uint)DateTime.Now.Ticks);
            return scenarios[random.NextInt(0, scenarios.Length)];
        }

        /// <summary>
        /// Processes AI responses and updates voter behaviors accordingly.
        /// </summary>
        private void ProcessAIResponses(List<Entity> representatives, VoterResponse[] responses, string content)
        {
            var contentHash = (uint)content.GetHashCode();
            var currentFrame = (uint)UnityEngine.Time.frameCount;

            for (int i = 0; i < responses.Length && i < representatives.Count; i++)
            {
                var entity = representatives[i];
                var response = responses[i];

                if (!EntityManager.Exists(entity))
                    continue;

                // Update voter behavior based on AI response
                var behavior = EntityManager.GetComponentData<BehaviorState>(entity);
                var opinion = EntityManager.GetComponentData<PoliticalOpinion>(entity);
                var eventResponse = EntityManager.GetComponentData<EventResponse>(entity);
                var cache = EntityManager.GetComponentData<AIAnalysisCache>(entity);

                // Apply AI-driven behavior changes
                ApplyAIInfluence(ref behavior, ref opinion, ref eventResponse, response);

                // Update cache with AI analysis results
                cache.PredictedPartySupport = (byte)(response.Sentiment * 127 + 128); // Convert -1..1 to 0..255
                cache.PredictedEngagement = (byte)(response.EngagementLevel * 255);
                cache.CachedAtFrame = currentFrame;
                cache.ContentHash = contentHash;
                cache.CacheConfidence = 200; // High confidence in AI analysis
                cache.Flags |= AnalysisFlags.HasCachedData | AnalysisFlags.HighConfidence | AnalysisFlags.Validated;
                cache.Flags &= ~AnalysisFlags.AnalysisInProgress;

                // Save to analysis cache for reuse
                _analysisCache[contentHash] = new AIInfluenceData
                {
                    SentimentShift = response.Sentiment,
                    EngagementChange = response.EngagementLevel,
                    ResponseType = response.Type,
                    CreatedAt = currentFrame,
                    Confidence = cache.CacheConfidence / 255f
                };

                // Update components
                EntityManager.SetComponentData(entity, behavior);
                EntityManager.SetComponentData(entity, opinion);
                EntityManager.SetComponentData(entity, eventResponse);
                EntityManager.SetComponentData(entity, cache);
            }
        }

        /// <summary>
        /// Applies AI analysis influence to voter behavior and opinions.
        /// </summary>
        private void ApplyAIInfluence(ref BehaviorState behavior, ref PoliticalOpinion opinion, ref EventResponse eventResponse, VoterResponse aiResponse)
        {
            var influenceStrength = 0.1f; // Moderate influence to avoid dramatic swings

            // Update emotional state based on AI response
            if (aiResponse.Type == ResponseType.Support)
            {
                behavior.Satisfaction = (byte)math.clamp(behavior.Satisfaction + (aiResponse.Sentiment * 20), 0, 255);
                behavior.Hope = (byte)math.clamp(behavior.Hope + (aiResponse.EngagementLevel * 15), 0, 255);
            }
            else if (aiResponse.Type == ResponseType.Opposition)
            {
                behavior.Anger = (byte)math.clamp(behavior.Anger + (math.abs(aiResponse.Sentiment) * 25), 0, 255);
                behavior.Satisfaction = (byte)math.clamp(behavior.Satisfaction - (math.abs(aiResponse.Sentiment) * 15), 0, 255);
            }

            // Update engagement based on AI response
            var engagementChange = aiResponse.EngagementLevel * influenceStrength;
            if (engagementChange > 0.5f)
            {
                behavior.Flags |= BehaviorFlags.IsEngaged;
                behavior.Flags &= ~BehaviorFlags.IsApathetic;
            }
            else if (engagementChange < 0.2f)
            {
                behavior.Flags |= BehaviorFlags.IsApathetic;
                behavior.Flags &= ~BehaviorFlags.IsEngaged;
            }

            // Update event response
            eventResponse.LastResponseType = aiResponse.Type;
            eventResponse.ResponseStrength = (byte)(math.abs(aiResponse.Sentiment) * 255);
            eventResponse.EmotionalResponse = (byte)(aiResponse.EngagementLevel * 255);
            eventResponse.LastEventFrame = (uint)UnityEngine.Time.frameCount;

            // Slightly adjust political opinions based on response
            var opinionShift = aiResponse.Sentiment * influenceStrength * 10;
            if (aiResponse.Type == ResponseType.Support)
            {
                // Reinforce existing positions slightly
                opinion.EconomicPosition = (sbyte)math.clamp(opinion.EconomicPosition + opinionShift, -100, 100);
                opinion.SocialPosition = (sbyte)math.clamp(opinion.SocialPosition + opinionShift, -100, 100);
            }

            // Increase confidence in opinions after AI-influenced response
            opinion.Confidence = (byte)math.min(255, opinion.Confidence + 5);
            opinion.LastUpdated = (uint)UnityEngine.Time.frameCount;
        }

        /// <summary>
        /// Applies AI analysis results to similar voters to reduce API calls.
        /// </summary>
        private void ApplyAnalysisToSimilarVoters(List<Entity> representatives, NativeArray<Entity> allVoters)
        {
            // Create mapping of representatives to their analysis
            var representativeAnalysis = new Dictionary<Entity, AIInfluenceData>();

            foreach (var representative in representatives)
            {
                if (EntityManager.Exists(representative))
                {
                    var cache = EntityManager.GetComponentData<AIAnalysisCache>(representative);
                    if (_analysisCache.TryGetValue(cache.ContentHash, out var analysis))
                    {
                        representativeAnalysis[representative] = analysis;
                    }
                }
            }

            // Apply analysis to similar voters
            foreach (var voter in allVoters)
            {
                if (!EntityManager.Exists(voter) || representatives.Contains(voter))
                    continue;

                var closestRepresentative = FindClosestRepresentative(voter, representatives);
                if (closestRepresentative != Entity.Null && representativeAnalysis.TryGetValue(closestRepresentative, out var analysis))
                {
                    ApplyCachedAnalysis(voter, analysis);
                }
            }
        }

        /// <summary>
        /// Applies cached AI analysis to voters with similar characteristics.
        /// </summary>
        private void ApplyCachedAnalysisToSimilarVoters()
        {
            if (_analysisCache.Count == 0)
                return;

            var currentFrame = (uint)UnityEngine.Time.frameCount;

            Entities
                .WithoutBurst()
                .ForEach((Entity entity, ref BehaviorState behavior, ref PoliticalOpinion opinion, ref EventResponse eventResponse, ref AIAnalysisCache cache) =>
                {
                    // Skip if already has recent analysis or is in progress
                    if ((cache.Flags & AnalysisFlags.AnalysisInProgress) != 0)
                        return;

                    if ((cache.Flags & AnalysisFlags.HasCachedData) != 0 && currentFrame - cache.CachedAtFrame < 1800) // 30 seconds
                        return;

                    // Find applicable cached analysis
                    foreach (var cachedAnalysis in _analysisCache.Values)
                    {
                        if (currentFrame - cachedAnalysis.CreatedAt < 3600 && cachedAnalysis.Confidence > 0.6f) // 1 minute, high confidence
                        {
                            // Apply cached analysis with reduced strength
                            var reducedStrength = cachedAnalysis.Confidence * 0.5f; // Reduce influence for non-representative voters

                            if (cachedAnalysis.ResponseType == ResponseType.Support)
                            {
                                behavior.Satisfaction = (byte)math.clamp(behavior.Satisfaction + (cachedAnalysis.SentimentShift * 10 * reducedStrength), 0, 255);
                            }
                            else if (cachedAnalysis.ResponseType == ResponseType.Opposition)
                            {
                                behavior.Anger = (byte)math.clamp(behavior.Anger + (math.abs(cachedAnalysis.SentimentShift) * 12 * reducedStrength), 0, 255);
                            }

                            // Update cache
                            cache.CachedAtFrame = currentFrame;
                            cache.Flags |= AnalysisFlags.HasCachedData;
                            cache.CacheConfidence = (byte)(cachedAnalysis.Confidence * 128); // Lower confidence for derived analysis

                            break; // Apply only first matching analysis
                        }
                    }
                }).Run();
        }

        /// <summary>
        /// Finds the representative voter most similar to the given voter.
        /// </summary>
        private Entity FindClosestRepresentative(Entity voter, List<Entity> representatives)
        {
            if (!EntityManager.Exists(voter) || representatives.Count == 0)
                return Entity.Null;

            var voterData = EntityManager.GetComponentData<VoterData>(voter);
            var voterOpinion = EntityManager.GetComponentData<PoliticalOpinion>(voter);

            Entity closestRepresentative = Entity.Null;
            float closestDistance = float.MaxValue;

            foreach (var representative in representatives)
            {
                if (!EntityManager.Exists(representative))
                    continue;

                var repData = EntityManager.GetComponentData<VoterData>(representative);
                var repOpinion = EntityManager.GetComponentData<PoliticalOpinion>(representative);

                // Calculate similarity score
                var distance = CalculateVoterSimilarity(voterData, voterOpinion, repData, repOpinion);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRepresentative = representative;
                }
            }

            return closestRepresentative;
        }

        /// <summary>
        /// Calculates similarity score between two voters.
        /// </summary>
        private float CalculateVoterSimilarity(VoterData voter1, PoliticalOpinion opinion1, VoterData voter2, PoliticalOpinion opinion2)
        {
            float distance = 0;

            // Age similarity
            distance += math.abs(voter1.Age - voter2.Age) / 70f; // Normalize by max age difference

            // Education similarity
            distance += math.abs(voter1.EducationLevel - voter2.EducationLevel) / 5f;

            // Urban/rural similarity
            distance += voter1.IsUrban == voter2.IsUrban ? 0 : 0.5f;

            // Political opinion similarity
            distance += math.abs(opinion1.EconomicPosition - opinion2.EconomicPosition) / 200f;
            distance += math.abs(opinion1.SocialPosition - opinion2.SocialPosition) / 200f;
            distance += math.abs(opinion1.ImmigrationStance - opinion2.ImmigrationStance) / 200f;

            return distance;
        }

        /// <summary>
        /// Applies cached analysis to a voter.
        /// </summary>
        private void ApplyCachedAnalysis(Entity voter, AIInfluenceData analysis)
        {
            if (!EntityManager.Exists(voter))
                return;

            var behavior = EntityManager.GetComponentData<BehaviorState>(voter);
            var cache = EntityManager.GetComponentData<AIAnalysisCache>(voter);

            // Apply reduced influence
            var influenceStrength = analysis.Confidence * 0.3f; // Reduced for cached analysis

            if (analysis.ResponseType == ResponseType.Support)
            {
                behavior.Satisfaction = (byte)math.clamp(behavior.Satisfaction + (analysis.SentimentShift * 8 * influenceStrength), 0, 255);
            }
            else if (analysis.ResponseType == ResponseType.Opposition)
            {
                behavior.Anger = (byte)math.clamp(behavior.Anger + (math.abs(analysis.SentimentShift) * 10 * influenceStrength), 0, 255);
            }

            // Update cache
            cache.CachedAtFrame = (uint)UnityEngine.Time.frameCount;
            cache.Flags |= AnalysisFlags.HasCachedData;
            cache.CacheConfidence = (byte)(analysis.Confidence * 100); // Lower confidence for derived

            EntityManager.SetComponentData(voter, behavior);
            EntityManager.SetComponentData(voter, cache);
        }

        private string GetRegionName(byte regionIndex)
        {
            var regions = new[] { "Noord-Holland", "Zuid-Holland", "Utrecht", "Gelderland", "Noord-Brabant", "Overijssel", "Limburg", "Friesland", "Groningen", "Drenthe", "Flevoland", "Zeeland" };
            return regionIndex < regions.Length ? regions[regionIndex] : "Nederland";
        }

        // Helper structures
        private struct VoterClusterKey : IEquatable<VoterClusterKey>
        {
            public int AgeGroup;
            public int EducationLevel;
            public bool IsUrban;
            public int EconomicPosition;
            public int SocialPosition;
            public int EngagementLevel;

            public bool Equals(VoterClusterKey other)
            {
                return AgeGroup == other.AgeGroup &&
                       EducationLevel == other.EducationLevel &&
                       IsUrban == other.IsUrban &&
                       EconomicPosition == other.EconomicPosition &&
                       SocialPosition == other.SocialPosition &&
                       EngagementLevel == other.EngagementLevel;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(AgeGroup, EducationLevel, IsUrban, EconomicPosition, SocialPosition, EngagementLevel);
            }
        }

        private struct AIInfluenceData
        {
            public float SentimentShift;
            public float EngagementChange;
            public ResponseType ResponseType;
            public uint CreatedAt;
            public float Confidence;
        }

        /// <summary>
        /// Component to mark voters currently undergoing AI analysis.
        /// </summary>
        private struct AIAnalysisInProgress : IComponentData
        {
            public uint StartFrame;
        }
    }
}