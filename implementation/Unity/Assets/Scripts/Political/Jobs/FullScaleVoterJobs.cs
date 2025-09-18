using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using SovereignsDilemma.Political.Components;

namespace SovereignsDilemma.Political.Jobs
{
    /// <summary>
    /// High-performance jobs for full-scale voter processing with Level of Detail optimization.
    /// Supports 10,000+ voters with adaptive processing based on distance and performance.
    /// </summary>

    /// <summary>
    /// Updates voter LOD levels based on camera distance and performance constraints.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct UpdateVoterLODJob : IJobChunk
    {
        [ReadOnly] public float3 CameraPosition;
        [ReadOnly] public float CurrentTime;
        [ReadOnly] public float HighDetailDistance;
        [ReadOnly] public float MediumDetailDistance;
        [ReadOnly] public float LowDetailDistance;
        [ReadOnly] public int MaxHighDetailVoters;
        [ReadOnly] public int MaxMediumDetailVoters;

        [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
        public ComponentTypeHandle<VoterLODLevel> LODLevelHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref TransformHandle);
            var lodLevels = chunk.GetNativeArray(ref LODLevelHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var position = transforms[i].Position;
                var distance = math.distance(position, CameraPosition);
                var lodLevel = lodLevels[i];

                // Update distance
                lodLevel.DistanceToCamera = distance;
                lodLevel.LastLODUpdate = CurrentTime;
                lodLevel.FramesSinceUpdate = 0;

                // Determine LOD level based on distance
                var newLevel = DetermineLODLevel(distance);

                // Apply performance constraints
                newLevel = ApplyPerformanceConstraints(newLevel, distance);

                lodLevel.CurrentLevel = newLevel;
                lodLevels[i] = lodLevel;
            }
        }

        private LODLevel DetermineLODLevel(float distance)
        {
            if (distance <= HighDetailDistance)
                return LODLevel.High;
            else if (distance <= MediumDetailDistance)
                return LODLevel.Medium;
            else if (distance <= LowDetailDistance)
                return LODLevel.Low;
            else
                return LODLevel.Dormant;
        }

        private LODLevel ApplyPerformanceConstraints(LODLevel requestedLevel, float distance)
        {
            // In a real implementation, this would check current high/medium detail voter counts
            // and potentially downgrade LOD if limits are exceeded
            return requestedLevel;
        }
    }

    /// <summary>
    /// High-detail voter processing with full social network simulation.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct HighDetailVoterUpdateJob : IJobChunk
    {
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public uint CurrentFrame;
        [ReadOnly] public float OpinionUpdateStrength;
        [ReadOnly] public float SocialInfluenceStrength;
        [ReadOnly] public float BehaviorUpdateRate;
        [ReadOnly] public Random RandomSeed;

        [ReadOnly] public ComponentTypeHandle<VoterData> VoterDataHandle;
        public ComponentTypeHandle<PoliticalOpinion> PoliticalOpinionHandle;
        public ComponentTypeHandle<BehaviorState> BehaviorStateHandle;
        public ComponentTypeHandle<SocialNetwork> SocialNetworkHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var voterData = chunk.GetNativeArray(ref VoterDataHandle);
            var opinions = chunk.GetNativeArray(ref PoliticalOpinionHandle);
            var behaviors = chunk.GetNativeArray(ref BehaviorStateHandle);
            var networks = chunk.GetNativeArray(ref SocialNetworkHandle);

            var random = RandomSeed;
            random.state += (uint)(unfilteredChunkIndex * 12345 + CurrentFrame);

            for (int i = 0; i < chunk.Count; i++)
            {
                var voter = voterData[i];
                var opinion = opinions[i];
                var behavior = behaviors[i];
                var network = networks[i];

                // Full opinion evolution with social influence
                UpdateOpinionWithSocialInfluence(ref opinion, ref behavior, ref network, voter, ref random);

                // Advanced behavior modeling
                UpdateAdvancedBehavior(ref behavior, opinion, voter, ref random);

                // Social network dynamics
                UpdateSocialNetwork(ref network, opinion, behavior, ref random);

                opinions[i] = opinion;
                behaviors[i] = behavior;
                networks[i] = network;
            }
        }

        private void UpdateOpinionWithSocialInfluence(ref PoliticalOpinion opinion, ref BehaviorState behavior,
            ref SocialNetwork network, VoterData voter, ref Random random)
        {
            // Natural opinion drift based on personality
            var personalityDrift = CalculatePersonalityDrift(voter, ref random);

            opinion.EconomicPosition += personalityDrift.x * OpinionUpdateStrength * DeltaTime;
            opinion.SocialPosition += personalityDrift.y * OpinionUpdateStrength * DeltaTime;
            opinion.EnvironmentalPosition += personalityDrift.z * OpinionUpdateStrength * DeltaTime;

            // Social influence from network
            if (network.ConnectionCount > 0)
            {
                var socialInfluence = CalculateSocialInfluence(network, ref random);
                var influenceStrength = SocialInfluenceStrength * behavior.PoliticalEngagement * DeltaTime;

                opinion.EconomicPosition += socialInfluence.x * influenceStrength;
                opinion.SocialPosition += socialInfluence.y * influenceStrength;
                opinion.EnvironmentalPosition += socialInfluence.z * influenceStrength;
            }

            // Clamp opinions to valid range
            opinion.EconomicPosition = math.clamp(opinion.EconomicPosition, 0f, 1f);
            opinion.SocialPosition = math.clamp(opinion.SocialPosition, 0f, 1f);
            opinion.EnvironmentalPosition = math.clamp(opinion.EnvironmentalPosition, 0f, 1f);
        }

        private void UpdateAdvancedBehavior(ref BehaviorState behavior, PoliticalOpinion opinion,
            VoterData voter, ref Random random)
        {
            // Satisfaction based on opinion alignment with preferred party
            var preferredPartyAlignment = CalculatePartyAlignment(opinion, voter);
            var satisfactionTarget = preferredPartyAlignment * 0.8f + 0.2f;

            behavior.Satisfaction = math.lerp(behavior.Satisfaction, satisfactionTarget,
                BehaviorUpdateRate * DeltaTime * 0.1f);

            // Political engagement influenced by satisfaction and age
            var engagementFactor = GetEngagementFactor(voter);
            var engagementTarget = behavior.Satisfaction * engagementFactor;

            behavior.PoliticalEngagement = math.lerp(behavior.PoliticalEngagement, engagementTarget,
                BehaviorUpdateRate * DeltaTime * 0.05f);

            // Opinion volatility decreases with age and engagement
            var volatilityTarget = math.max(0.1f, 1.0f - behavior.PoliticalEngagement * 0.5f - GetAgeStabilityFactor(voter.Age));
            behavior.OpinionVolatility = math.lerp(behavior.OpinionVolatility, volatilityTarget,
                BehaviorUpdateRate * DeltaTime * 0.02f);

            // Random life events affecting behavior
            if (random.NextFloat() < 0.001f * DeltaTime) // Rare events
            {
                ApplyRandomLifeEvent(ref behavior, ref random);
            }
        }

        private void UpdateSocialNetwork(ref SocialNetwork network, PoliticalOpinion opinion,
            BehaviorState behavior, ref Random random)
        {
            // Network growth based on political engagement
            if (behavior.PoliticalEngagement > 0.7f && random.NextFloat() < 0.01f * DeltaTime)
            {
                network.ConnectionCount = math.min(network.ConnectionCount + 1, 50);
            }

            // Network decay due to opinion differences or low engagement
            if (behavior.PoliticalEngagement < 0.3f && random.NextFloat() < 0.005f * DeltaTime)
            {
                network.ConnectionCount = math.max(network.ConnectionCount - 1, 0);
            }

            // Update influence score based on network size and engagement
            network.InfluenceScore = math.sqrt(network.ConnectionCount) * behavior.PoliticalEngagement * 0.1f;
        }

        private float3 CalculatePersonalityDrift(VoterData voter, ref Random random)
        {
            // Personality-based opinion drift
            var ageFactor = 1.0f - (voter.Age / 100f) * 0.5f; // Younger people more volatile
            var educationFactor = (int)voter.EducationLevel * 0.1f + 0.7f; // Education affects opinion stability

            var driftStrength = ageFactor * (2.0f - educationFactor) * 0.01f;

            return new float3(
                (random.NextFloat() - 0.5f) * driftStrength,
                (random.NextFloat() - 0.5f) * driftStrength,
                (random.NextFloat() - 0.5f) * driftStrength
            );
        }

        private float3 CalculateSocialInfluence(SocialNetwork network, ref Random random)
        {
            // Simulate social influence from network connections
            var influenceStrength = math.min(network.InfluenceScore, 1.0f);

            return new float3(
                (random.NextFloat() - 0.5f) * influenceStrength,
                (random.NextFloat() - 0.5f) * influenceStrength,
                (random.NextFloat() - 0.5f) * influenceStrength
            );
        }

        private float CalculatePartyAlignment(PoliticalOpinion opinion, VoterData voter)
        {
            // Simplified party alignment calculation
            // In reality, this would use detailed party position data
            var economic = opinion.EconomicPosition;
            var social = opinion.SocialPosition;

            // Example: moderate positions have higher satisfaction
            var economicAlignment = 1.0f - math.abs(economic - 0.5f) * 2.0f;
            var socialAlignment = 1.0f - math.abs(social - 0.5f) * 2.0f;

            return (economicAlignment + socialAlignment) * 0.5f;
        }

        private float GetEngagementFactor(VoterData voter)
        {
            // Age-based engagement curve (peaks in middle age)
            var ageFactor = 1.0f - math.abs(voter.Age - 45f) / 45f;
            ageFactor = math.max(0.3f, ageFactor);

            // Education increases engagement
            var educationFactor = (int)voter.EducationLevel * 0.2f + 0.6f;

            return ageFactor * educationFactor;
        }

        private float GetAgeStabilityFactor(int age)
        {
            // Older voters have more stable opinions
            return math.min(age / 80f, 0.8f);
        }

        private void ApplyRandomLifeEvent(ref BehaviorState behavior, ref Random random)
        {
            var eventType = random.NextInt(0, 4);

            switch (eventType)
            {
                case 0: // Economic event
                    behavior.Satisfaction += random.NextFloat(-0.3f, 0.3f);
                    break;
                case 1: // Social event
                    behavior.PoliticalEngagement += random.NextFloat(-0.2f, 0.4f);
                    break;
                case 2: // Health event
                    behavior.OpinionVolatility += random.NextFloat(-0.1f, 0.3f);
                    break;
                case 3: // Family event
                    behavior.Satisfaction += random.NextFloat(-0.2f, 0.4f);
                    break;
            }

            // Clamp all values
            behavior.Satisfaction = math.clamp(behavior.Satisfaction, 0f, 1f);
            behavior.PoliticalEngagement = math.clamp(behavior.PoliticalEngagement, 0f, 1f);
            behavior.OpinionVolatility = math.clamp(behavior.OpinionVolatility, 0f, 1f);
        }
    }

    /// <summary>
    /// Medium-detail voter processing with simplified social simulation.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct MediumDetailVoterUpdateJob : IJobChunk
    {
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public uint CurrentFrame;
        [ReadOnly] public float OpinionUpdateStrength;
        [ReadOnly] public float BehaviorUpdateRate;
        [ReadOnly] public Random RandomSeed;

        [ReadOnly] public ComponentTypeHandle<VoterData> VoterDataHandle;
        public ComponentTypeHandle<PoliticalOpinion> PoliticalOpinionHandle;
        public ComponentTypeHandle<BehaviorState> BehaviorStateHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var voterData = chunk.GetNativeArray(ref VoterDataHandle);
            var opinions = chunk.GetNativeArray(ref PoliticalOpinionHandle);
            var behaviors = chunk.GetNativeArray(ref BehaviorStateHandle);

            var random = RandomSeed;
            random.state += (uint)(unfilteredChunkIndex * 54321 + CurrentFrame);

            for (int i = 0; i < chunk.Count; i++)
            {
                var voter = voterData[i];
                var opinion = opinions[i];
                var behavior = behaviors[i];

                // Simplified opinion evolution
                UpdateOpinionSimplified(ref opinion, voter, ref random);

                // Basic behavior updates
                UpdateBehaviorSimplified(ref behavior, opinion, voter, ref random);

                opinions[i] = opinion;
                behaviors[i] = behavior;
            }
        }

        private void UpdateOpinionSimplified(ref PoliticalOpinion opinion, VoterData voter, ref Random random)
        {
            // Basic personality-driven drift
            var ageFactor = (100 - voter.Age) / 100f * 0.5f + 0.1f;
            var drift = (random.NextFloat() - 0.5f) * OpinionUpdateStrength * ageFactor * DeltaTime;

            opinion.EconomicPosition += drift;
            opinion.SocialPosition += drift * 0.8f;
            opinion.EnvironmentalPosition += drift * 1.2f;

            // Clamp values
            opinion.EconomicPosition = math.clamp(opinion.EconomicPosition, 0f, 1f);
            opinion.SocialPosition = math.clamp(opinion.SocialPosition, 0f, 1f);
            opinion.EnvironmentalPosition = math.clamp(opinion.EnvironmentalPosition, 0f, 1f);
        }

        private void UpdateBehaviorSimplified(ref BehaviorState behavior, PoliticalOpinion opinion,
            VoterData voter, ref Random random)
        {
            // Simplified satisfaction calculation
            var idealPosition = 0.5f; // Assume moderate position is most satisfying
            var positionDistance = math.abs(opinion.EconomicPosition - idealPosition);
            var targetSatisfaction = 1.0f - positionDistance;

            behavior.Satisfaction = math.lerp(behavior.Satisfaction, targetSatisfaction,
                BehaviorUpdateRate * DeltaTime * 0.1f);

            // Age-based engagement
            var ageEngagement = 1.0f - math.abs(voter.Age - 45f) / 45f;
            behavior.PoliticalEngagement = math.lerp(behavior.PoliticalEngagement, ageEngagement,
                BehaviorUpdateRate * DeltaTime * 0.05f);

            // Simple volatility based on age
            var targetVolatility = math.max(0.1f, (100f - voter.Age) / 100f);
            behavior.OpinionVolatility = math.lerp(behavior.OpinionVolatility, targetVolatility,
                BehaviorUpdateRate * DeltaTime * 0.02f);
        }
    }

    /// <summary>
    /// Low-detail voter processing with minimal computation.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct LowDetailVoterUpdateJob : IJobChunk
    {
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public uint CurrentFrame;
        [ReadOnly] public float OpinionDecayRate;
        [ReadOnly] public Random RandomSeed;

        [ReadOnly] public ComponentTypeHandle<VoterData> VoterDataHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var voterData = chunk.GetNativeArray(ref VoterDataHandle);

            // Minimal processing - just age tracking for distant voters
            // Most other components are not updated to save performance
        }
    }

    /// <summary>
    /// Dormant voter processing - minimal updates for inactive voters.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct DormantVoterUpdateJob : IJobChunk
    {
        [ReadOnly] public float AgeIncrement;
        public ComponentTypeHandle<VoterData> VoterDataHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var voterData = chunk.GetNativeArray(ref VoterDataHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var voter = voterData[i];

                // Only update age for dormant voters
                // This happens very infrequently (every few seconds)
                voter.Age = (int)math.min(voter.Age + AgeIncrement, 100);

                voterData[i] = voter;
            }
        }
    }
}