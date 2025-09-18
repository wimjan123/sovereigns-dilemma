using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Jobs
{
    /// <summary>
    /// High-performance job for updating voter behavior using Unity Jobs System.
    /// Processes thousands of voters in parallel with Burst compilation.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct VoterBehaviorUpdateJob : IJobParallelFor
    {
        // Read-only data
        [ReadOnly] public NativeArray<VoterData> VoterDataArray;
        [ReadOnly] public NativeArray<SocialNetwork> SocialNetworkArray;
        [ReadOnly] public NativeArray<EventResponse> EventResponseArray;

        // Read-write data
        public NativeArray<PoliticalOpinion> PoliticalOpinionArray;
        public NativeArray<BehaviorState> BehaviorStateArray;
        public NativeArray<AIAnalysisCache> AIAnalysisCacheArray;

        // Job parameters
        [ReadOnly] public uint CurrentFrame;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public float OpinionDecayRate;
        [ReadOnly] public float SocialInfluenceStrength;
        [ReadOnly] public Random RandomSeed;

        public void Execute(int index)
        {
            var voterData = VoterDataArray[index];
            var socialNetwork = SocialNetworkArray[index];
            var eventResponse = EventResponseArray[index];

            var opinion = PoliticalOpinionArray[index];
            var behavior = BehaviorStateArray[index];
            var aiCache = AIAnalysisCacheArray[index];

            // Create job-local random generator
            var random = new Random(RandomSeed.state + (uint)index);

            // Apply opinion decay
            ApplyOpinionDecay(ref opinion, voterData, behavior, random);

            // Apply social influence
            ApplySocialInfluence(ref opinion, ref behavior, voterData, socialNetwork, random);

            // Update behavior state
            UpdateBehaviorState(ref behavior, voterData, eventResponse, random);

            // Update AI cache status
            UpdateAIAnalysisCache(ref aiCache, behavior, random);

            // Write back results
            PoliticalOpinionArray[index] = opinion;
            BehaviorStateArray[index] = behavior;
            AIAnalysisCacheArray[index] = aiCache;
        }

        [BurstCompile]
        private void ApplyOpinionDecay(ref PoliticalOpinion opinion, VoterData voterData, BehaviorState behavior, Random random)
        {
            // Skip if recently updated
            if (CurrentFrame - opinion.LastUpdated < 60) // 1 second at 60 FPS
                return;

            // Calculate decay rate based on personality and confidence
            var personalityStability = (behavior.Conscientiousness + (255 - behavior.Neuroticism)) / 2f / 255f;
            var confidenceStability = opinion.Confidence / 255f;

            var effectiveDecayRate = OpinionDecayRate * (1f - personalityStability * 0.7f) * (1f - confidenceStability * 0.3f) * DeltaTime;

            // Add small random variations to avoid synchronized decay
            effectiveDecayRate *= (0.8f + random.NextFloat() * 0.4f); // ±20% variation

            // Decay political positions toward neutral
            opinion.EconomicPosition = (sbyte)math.lerp(opinion.EconomicPosition, 0, effectiveDecayRate);
            opinion.SocialPosition = (sbyte)math.lerp(opinion.SocialPosition, 0, effectiveDecayRate);
            opinion.ImmigrationStance = (sbyte)math.lerp(opinion.ImmigrationStance, 0, effectiveDecayRate);
            opinion.EnvironmentalStance = (sbyte)math.lerp(opinion.EnvironmentalStance, 0, effectiveDecayRate);

            // Decay party support based on loyalty
            var loyaltyFactor = (behavior.Flags & BehaviorFlags.IsLoyalist) != 0 ? 0.2f : 1f;
            var partyDecayRate = effectiveDecayRate * loyaltyFactor;

            opinion.VVDSupport = (byte)math.max(0, opinion.VVDSupport - (partyDecayRate * 50));
            opinion.PVVSupport = (byte)math.max(0, opinion.PVVSupport - (partyDecayRate * 50));
            opinion.CDASupport = (byte)math.max(0, opinion.CDASupport - (partyDecayRate * 50));
            opinion.D66Support = (byte)math.max(0, opinion.D66Support - (partyDecayRate * 50));
            opinion.SPSupport = (byte)math.max(0, opinion.SPSupport - (partyDecayRate * 50));
            opinion.PvdASupport = (byte)math.max(0, opinion.PvdASupport - (partyDecayRate * 50));
            opinion.GLSupport = (byte)math.max(0, opinion.GLSupport - (partyDecayRate * 50));
            opinion.CUSupport = (byte)math.max(0, opinion.CUSupport - (partyDecayRate * 50));
            opinion.SGPSupport = (byte)math.max(0, opinion.SGPSupport - (partyDecayRate * 50));
            opinion.DENKSupport = (byte)math.max(0, opinion.DENKSupport - (partyDecayRate * 50));
            opinion.FvDSupport = (byte)math.max(0, opinion.FvDSupport - (partyDecayRate * 50));
            opinion.VoltSupport = (byte)math.max(0, opinion.VoltSupport - (partyDecayRate * 50));

            // Gradually reduce confidence over time
            opinion.Confidence = (byte)math.max(80, opinion.Confidence - (effectiveDecayRate * 5));

            opinion.LastUpdated = CurrentFrame;
        }

        [BurstCompile]
        private void ApplySocialInfluence(ref PoliticalOpinion opinion, ref BehaviorState behavior, VoterData voterData, SocialNetwork network, Random random)
        {
            // Skip apathetic voters or those recently influenced
            if ((behavior.Flags & BehaviorFlags.IsApathetic) != 0)
                return;

            if (CurrentFrame - network.LastInteraction < 300) // 5 seconds at 60 FPS
                return;

            // Calculate influence susceptibility
            var susceptibility = network.SusceptibilityScore / 255f;
            var socialInfluence = behavior.SocialInfluence / 255f;
            var openness = behavior.Openness / 255f;

            var totalSusceptibility = (susceptibility + socialInfluence + openness) / 3f;

            // Apply influence if susceptible enough
            if (totalSusceptibility > 0.25f && network.NetworkSize > 3)
            {
                var influenceStrength = SocialInfluenceStrength * totalSusceptibility;

                // Echo chamber effect
                var echoChamberStrength = network.EchoChamberStrength / 255f;
                var diversityExposure = network.DiversityExposure / 255f;

                if (echoChamberStrength > 0.6f)
                {
                    // Reinforce existing strong opinions
                    if (math.abs(opinion.EconomicPosition) > 40)
                    {
                        var reinforcement = influenceStrength * 0.5f;
                        opinion.EconomicPosition = (sbyte)math.clamp(
                            opinion.EconomicPosition + (opinion.EconomicPosition > 0 ? reinforcement * 20 : -reinforcement * 20),
                            -100, 100);
                    }

                    if (math.abs(opinion.SocialPosition) > 40)
                    {
                        var reinforcement = influenceStrength * 0.5f;
                        opinion.SocialPosition = (sbyte)math.clamp(
                            opinion.SocialPosition + (opinion.SocialPosition > 0 ? reinforcement * 20 : -reinforcement * 20),
                            -100, 100);
                    }

                    // Increase confidence through echo chamber validation
                    opinion.Confidence = (byte)math.min(255, opinion.Confidence + influenceStrength * 15);
                }

                if (diversityExposure > 0.5f)
                {
                    // Moderate extreme positions through diverse exposure
                    var moderationStrength = influenceStrength * diversityExposure * 0.3f;

                    if (math.abs(opinion.EconomicPosition) > 60)
                    {
                        opinion.EconomicPosition = (sbyte)math.lerp(opinion.EconomicPosition, 0, moderationStrength);
                    }

                    if (math.abs(opinion.SocialPosition) > 60)
                    {
                        opinion.SocialPosition = (sbyte)math.lerp(opinion.SocialPosition, 0, moderationStrength);
                    }

                    // Increase openness through diverse exposure
                    behavior.Openness = (byte)math.min(255, behavior.Openness + moderationStrength * 10);
                }

                // Random small influences from social interactions
                if (random.NextFloat() < 0.1f) // 10% chance per update
                {
                    var randomInfluence = (random.NextFloat() - 0.5f) * influenceStrength * 10;

                    opinion.EconomicPosition = (sbyte)math.clamp(opinion.EconomicPosition + randomInfluence, -100, 100);
                    opinion.SocialPosition = (sbyte)math.clamp(opinion.SocialPosition + randomInfluence * 0.7f, -100, 100);
                }
            }
        }

        [BurstCompile]
        private void UpdateBehaviorState(ref BehaviorState behavior, VoterData voterData, EventResponse eventResponse, Random random)
        {
            // Update emotional states based on recent events
            var timeSinceLastEvent = CurrentFrame - eventResponse.LastEventFrame;

            if (timeSinceLastEvent < 3600) // Within last minute at 60 FPS
            {
                // Recent event affects emotional state
                var eventImpact = eventResponse.ResponseStrength / 255f;
                var emotionalResponse = eventResponse.EmotionalResponse / 255f;

                // Adjust emotional state based on event type
                switch (eventResponse.LastResponseType)
                {
                    case ResponseType.Support:
                        behavior.Satisfaction = (byte)math.min(255, behavior.Satisfaction + (eventImpact * 12));
                        behavior.Hope = (byte)math.min(255, behavior.Hope + (eventImpact * 8));
                        behavior.Anger = (byte)math.max(0, behavior.Anger - (eventImpact * 5));
                        break;

                    case ResponseType.Opposition:
                        behavior.Satisfaction = (byte)math.max(0, behavior.Satisfaction - (eventImpact * 15));
                        behavior.Anger = (byte)math.min(255, behavior.Anger + (eventImpact * 18));
                        behavior.Anxiety = (byte)math.min(255, behavior.Anxiety + (eventImpact * 10));
                        break;

                    case ResponseType.Questioning:
                        behavior.Anxiety = (byte)math.min(255, behavior.Anxiety + (eventImpact * 8));
                        behavior.Hope = (byte)math.max(0, behavior.Hope - (eventImpact * 3));
                        break;

                    case ResponseType.Emotional:
                        // Increase neuroticism for highly emotional responses
                        if (emotionalResponse > 0.8f)
                        {
                            behavior.Neuroticism = (byte)math.min(255, behavior.Neuroticism + 2);
                        }
                        break;
                }

                // Update personality traits based on repeated patterns
                if (eventResponse.CumulativeImpact > 50)
                {
                    behavior.ChangeResistance = (byte)math.min(255, behavior.ChangeResistance + 1);
                }
                else if (eventResponse.CumulativeImpact < -50)
                {
                    behavior.Openness = (byte)math.min(255, behavior.Openness + 1);
                }
            }
            else
            {
                // Gradual emotional recovery over time
                var recoveryRate = DeltaTime * 0.05f; // Slower recovery than original

                behavior.Anxiety = (byte)math.lerp(behavior.Anxiety, 80, recoveryRate); // Return to baseline
                behavior.Anger = (byte)math.lerp(behavior.Anger, 60, recoveryRate);
                behavior.Satisfaction = (byte)math.lerp(behavior.Satisfaction, 120, recoveryRate);
                behavior.Hope = (byte)math.lerp(behavior.Hope, 130, recoveryRate);
            }

            // Update behavioral flags based on current state
            UpdateBehaviorFlags(ref behavior, voterData, random);

            // Apply age-related changes very slowly
            if (random.NextFloat() < 0.001f) // Very rare updates
            {
                ApplyAgeRelatedChanges(ref behavior, voterData);
            }
        }

        [BurstCompile]
        private void UpdateBehaviorFlags(ref BehaviorState behavior, VoterData voterData, Random random)
        {
            // Update engagement based on current emotional state and personality
            var baseEngagement = (behavior.Extraversion + behavior.Conscientiousness + behavior.MediaConsumption) / 3f / 255f;
            var emotionalActivation = (behavior.Anger + behavior.Anxiety + behavior.Hope) / 3f / 255f;

            var totalEngagement = (baseEngagement + emotionalActivation * 0.5f);

            if (totalEngagement > 0.7f)
            {
                behavior.Flags |= BehaviorFlags.IsEngaged;
                behavior.Flags &= ~BehaviorFlags.IsApathetic;
            }
            else if (totalEngagement < 0.3f)
            {
                behavior.Flags |= BehaviorFlags.IsApathetic;
                behavior.Flags &= ~BehaviorFlags.IsEngaged;
            }

            // Update influencer status
            var influencePotential = (behavior.Extraversion + behavior.Conscientiousness + behavior.MediaConsumption) / 3f / 255f;
            if (influencePotential > 0.8f && (behavior.Flags & BehaviorFlags.IsEngaged) != 0)
            {
                behavior.Flags |= BehaviorFlags.IsInfluencer;
            }
            else if (influencePotential < 0.4f)
            {
                behavior.Flags &= ~BehaviorFlags.IsInfluencer;
            }

            // Update volatility based on neuroticism and recent changes
            var volatilityScore = (behavior.Neuroticism + (255 - behavior.ChangeResistance)) / 2f / 255f;
            if (volatilityScore > 0.7f)
            {
                behavior.Flags |= BehaviorFlags.IsVolatile;
            }
            else if (volatilityScore < 0.3f)
            {
                behavior.Flags &= ~BehaviorFlags.IsVolatile;
            }

            // Update protest tendency
            var protestPotential = (behavior.Anger + behavior.Openness + behavior.Extraversion) / 3f / 255f;
            if (protestPotential > 0.75f && behavior.Anger > 180)
            {
                behavior.Flags |= BehaviorFlags.IsProtester;
            }
            else if (protestPotential < 0.4f || behavior.Anger < 100)
            {
                behavior.Flags &= ~BehaviorFlags.IsProtester;
            }

            // Update activist status
            var activismScore = (behavior.Openness + behavior.Conscientiousness + behavior.MediaConsumption) / 3f / 255f;
            if (activismScore > 0.8f && (behavior.Flags & BehaviorFlags.IsEngaged) != 0)
            {
                behavior.Flags |= BehaviorFlags.IsActivist;
            }

            // Update early adopter status
            if (behavior.Openness > 200 && behavior.Extraversion > 180)
            {
                behavior.Flags |= BehaviorFlags.IsEarlyAdopter;
            }
        }

        [BurstCompile]
        private void ApplyAgeRelatedChanges(ref BehaviorState behavior, VoterData voterData)
        {
            // Simulate gradual personality changes with age
            if (voterData.Age > 50)
            {
                // Older voters become slightly more conservative and less open to change
                behavior.ChangeResistance = (byte)math.min(255, behavior.ChangeResistance + 1);
                behavior.Openness = (byte)math.max(50, behavior.Openness - 1);
                behavior.AuthorityTrust = (byte)math.min(255, behavior.AuthorityTrust + 1);
            }
            else if (voterData.Age < 30)
            {
                // Younger voters become slightly more open and engaged over time
                behavior.Openness = (byte)math.min(255, behavior.Openness + 1);
                behavior.MediaConsumption = (byte)math.min(255, behavior.MediaConsumption + 1);
            }
        }

        [BurstCompile]
        private void UpdateAIAnalysisCache(ref AIAnalysisCache cache, BehaviorState behavior, Random random)
        {
            // Mark cache as needing refresh if voter behavior has changed significantly
            if ((behavior.Flags & BehaviorFlags.IsVolatile) != 0)
            {
                if (CurrentFrame - cache.CachedAtFrame > 1800) // 30 seconds for volatile voters
                {
                    cache.Flags |= AnalysisFlags.NeedsRefresh;
                }
            }

            // Reduce cache confidence over time
            if (cache.CacheConfidence > 0)
            {
                cache.CacheConfidence = (byte)math.max(50, cache.CacheConfidence - 1);
            }

            // Mark representative voters for priority AI analysis
            if ((behavior.Flags & BehaviorFlags.IsInfluencer) != 0 && random.NextFloat() < 0.01f)
            {
                cache.Flags |= AnalysisFlags.Representative;
            }
        }
    }

    /// <summary>
    /// Parallel job for processing social network interactions between voters.
    /// Handles influence propagation and network dynamics efficiently.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct SocialNetworkUpdateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VoterData> VoterDataArray;
        [ReadOnly] public NativeArray<PoliticalOpinion> PoliticalOpinionArray;
        [ReadOnly] public NativeArray<BehaviorState> BehaviorStateArray;

        public NativeArray<SocialNetwork> SocialNetworkArray;

        [ReadOnly] public uint CurrentFrame;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public Random RandomSeed;

        public void Execute(int index)
        {
            var voterData = VoterDataArray[index];
            var opinion = PoliticalOpinionArray[index];
            var behavior = BehaviorStateArray[index];
            var network = SocialNetworkArray[index];

            var random = new Random(RandomSeed.state + (uint)index + 12345); // Different seed offset

            // Update network metrics based on current behavior
            UpdateNetworkMetrics(ref network, voterData, behavior, random);

            // Simulate network interactions
            SimulateNetworkInteraction(ref network, voterData, opinion, behavior, random);

            SocialNetworkArray[index] = network;
        }

        [BurstCompile]
        private void UpdateNetworkMetrics(ref SocialNetwork network, VoterData voterData, BehaviorState behavior, Random random)
        {
            // Update influence score based on engagement and personality
            var newInfluenceScore = (behavior.Extraversion + behavior.MediaConsumption + behavior.Conscientiousness) / 3f;

            if ((behavior.Flags & BehaviorFlags.IsInfluencer) != 0)
                newInfluenceScore *= 1.2f;
            if ((behavior.Flags & BehaviorFlags.IsEngaged) != 0)
                newInfluenceScore *= 1.1f;

            network.InfluenceScore = (byte)math.clamp(newInfluenceScore, 10, 255);

            // Update susceptibility based on personality traits
            var newSusceptibility = (behavior.Agreeableness + behavior.SocialInfluence + (255 - behavior.Conscientiousness)) / 3f;
            network.SusceptibilityScore = (byte)math.clamp(newSusceptibility, 30, 255);

            // Gradually update network size based on behavior changes
            if ((behavior.Flags & BehaviorFlags.IsEngaged) != 0)
            {
                network.NetworkSize = (byte)math.min(255, network.NetworkSize + random.NextInt(0, 2));
            }
            else if ((behavior.Flags & BehaviorFlags.IsApathetic) != 0)
            {
                network.NetworkSize = (byte)math.max(20, network.NetworkSize - random.NextInt(0, 2));
            }

            // Update online connections based on age and engagement
            if (voterData.Age < 40 && (behavior.Flags & BehaviorFlags.IsEngaged) != 0)
            {
                network.OnlineConnections = (byte)math.min(255, network.OnlineConnections + random.NextInt(0, 3));
            }
            else if (voterData.Age > 60)
            {
                network.OnlineConnections = (byte)math.max(20, network.OnlineConnections - random.NextInt(0, 2));
            }
        }

        [BurstCompile]
        private void SimulateNetworkInteraction(ref SocialNetwork network, VoterData voterData, PoliticalOpinion opinion, BehaviorState behavior, Random random)
        {
            // Skip interaction if too recent
            if (CurrentFrame - network.LastInteraction < 180) // 3 seconds minimum
                return;

            // Probability of interaction based on engagement and network size
            var interactionProbability = (behavior.Extraversion / 255f) * (network.NetworkSize / 255f) * 0.1f;

            if (random.NextFloat() > interactionProbability)
                return;

            // Simulate interaction effects
            if ((behavior.Flags & BehaviorFlags.IsEngaged) != 0)
            {
                // Engaged voters strengthen their networks
                network.FamilyConnections = (byte)math.min(255, network.FamilyConnections + 1);
                network.SocialConnections = (byte)math.min(255, network.SocialConnections + 2);

                // Update echo chamber or diversity based on openness
                if (behavior.Openness > 180)
                {
                    network.DiversityExposure = (byte)math.min(255, network.DiversityExposure + 2);
                    network.EchoChamberStrength = (byte)math.max(20, network.EchoChamberStrength - 1);
                }
                else if (behavior.ChangeResistance > 180)
                {
                    network.EchoChamberStrength = (byte)math.min(255, network.EchoChamberStrength + 2);
                    network.DiversityExposure = (byte)math.max(30, network.DiversityExposure - 1);
                }
            }

            // Update work connections based on employment status
            if ((voterData.Flags & VoterFlags.IsEmployed) != 0 && random.NextFloat() < 0.3f)
            {
                network.WorkConnections = (byte)math.min(255, network.WorkConnections + 1);
            }

            network.LastInteraction = CurrentFrame;
        }
    }
}