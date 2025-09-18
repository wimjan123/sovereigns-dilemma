using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Core voter behavior simulation system using Unity ECS and Jobs.
    /// Processes political opinion updates for thousands of voters efficiently.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct VoterBehaviorSystem : ISystem
    {
        private static readonly ProfilerMarker VoterUpdateMarker = PerformanceProfiler.VoterUpdateMarker;

        private ComponentLookup<PoliticalOpinion> _politicalOpinionLookup;
        private ComponentLookup<BehaviorState> _behaviorStateLookup;
        private ComponentLookup<SocialNetwork> _socialNetworkLookup;
        private ComponentLookup<EventResponse> _eventResponseLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _politicalOpinionLookup = state.GetComponentLookup<PoliticalOpinion>();
            _behaviorStateLookup = state.GetComponentLookup<BehaviorState>();
            _socialNetworkLookup = state.GetComponentLookup<SocialNetwork>();
            _eventResponseLookup = state.GetComponentLookup<EventResponse>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (VoterUpdateMarker.Auto())
            {
                _politicalOpinionLookup.Update(ref state);
                _behaviorStateLookup.Update(ref state);
                _socialNetworkLookup.Update(ref state);
                _eventResponseLookup.Update(ref state);

                var currentFrame = (uint)UnityEngine.Time.frameCount;
                var deltaTime = state.WorldUnmanaged.Time.DeltaTime;

                // Schedule voter opinion decay job
                var opinionDecayJob = new OpinionDecayJob
                {
                    CurrentFrame = currentFrame,
                    DeltaTime = deltaTime,
                    DecayRate = 0.001f // Slow opinion decay over time
                };

                var opinionDecayHandle = opinionDecayJob.ScheduleParallel(state.Dependency);

                // Schedule social influence job
                var socialInfluenceJob = new SocialInfluenceJob
                {
                    PoliticalOpinionLookup = _politicalOpinionLookup,
                    BehaviorStateLookup = _behaviorStateLookup,
                    SocialNetworkLookup = _socialNetworkLookup,
                    CurrentFrame = currentFrame,
                    InfluenceStrength = 0.01f
                };

                var socialInfluenceHandle = socialInfluenceJob.ScheduleParallel(opinionDecayHandle);

                // Schedule behavior state updates
                var behaviorUpdateJob = new BehaviorUpdateJob
                {
                    EventResponseLookup = _eventResponseLookup,
                    CurrentFrame = currentFrame,
                    DeltaTime = deltaTime
                };

                var behaviorUpdateHandle = behaviorUpdateJob.ScheduleParallel(socialInfluenceHandle);

                state.Dependency = behaviorUpdateHandle;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Cleanup if needed
        }
    }

    /// <summary>
    /// Job for processing political opinion decay over time.
    /// Opinions naturally fade without reinforcement.
    /// </summary>
    [BurstCompile]
    public partial struct OpinionDecayJob : IJobEntity
    {
        public uint CurrentFrame;
        public float DeltaTime;
        public float DecayRate;

        public void Execute(ref PoliticalOpinion opinion, in VoterData voterData, in BehaviorState behavior)
        {
            // Skip if recently updated
            if (CurrentFrame - opinion.LastUpdated < 60) // 1 second at 60 FPS
                return;

            // Calculate decay based on confidence and personality
            var personalityStability = (behavior.Conscientiousness + behavior.Neuroticism) / 2f / 255f;
            var effectiveDecayRate = DecayRate * (1f - personalityStability) * DeltaTime;

            // Decay opinions toward neutral (0)
            opinion.EconomicPosition = (sbyte)math.lerp(opinion.EconomicPosition, 0, effectiveDecayRate);
            opinion.SocialPosition = (sbyte)math.lerp(opinion.SocialPosition, 0, effectiveDecayRate);
            opinion.ImmigrationStance = (sbyte)math.lerp(opinion.ImmigrationStance, 0, effectiveDecayRate);
            opinion.EnvironmentalStance = (sbyte)math.lerp(opinion.EnvironmentalStance, 0, effectiveDecayRate);

            // Decay party support more slowly for loyal voters
            var loyaltyFactor = (behavior.Flags & BehaviorFlags.IsLoyalist) != 0 ? 0.1f : 1f;
            var partyDecayRate = effectiveDecayRate * loyaltyFactor;

            opinion.VVDSupport = (byte)math.max(0, opinion.VVDSupport - (partyDecayRate * 255));
            opinion.PVVSupport = (byte)math.max(0, opinion.PVVSupport - (partyDecayRate * 255));
            opinion.CDASupport = (byte)math.max(0, opinion.CDASupport - (partyDecayRate * 255));
            opinion.D66Support = (byte)math.max(0, opinion.D66Support - (partyDecayRate * 255));
            opinion.SPSupport = (byte)math.max(0, opinion.SPSupport - (partyDecayRate * 255));
            opinion.PvdASupport = (byte)math.max(0, opinion.PvdASupport - (partyDecayRate * 255));
            opinion.GLSupport = (byte)math.max(0, opinion.GLSupport - (partyDecayRate * 255));
            opinion.CUSupport = (byte)math.max(0, opinion.CUSupport - (partyDecayRate * 255));
            opinion.SGPSupport = (byte)math.max(0, opinion.SGPSupport - (partyDecayRate * 255));
            opinion.DENKSupport = (byte)math.max(0, opinion.DENKSupport - (partyDecayRate * 255));
            opinion.FvDSupport = (byte)math.max(0, opinion.FvDSupport - (partyDecayRate * 255));
            opinion.VoltSupport = (byte)math.max(0, opinion.VoltSupport - (partyDecayRate * 255));

            // Reduce confidence over time
            opinion.Confidence = (byte)math.max(50, opinion.Confidence - (effectiveDecayRate * 10));

            opinion.LastUpdated = CurrentFrame;
        }
    }

    /// <summary>
    /// Job for processing social influence between voters.
    /// Voters influence nearby voters with similar characteristics.
    /// </summary>
    [BurstCompile]
    public partial struct SocialInfluenceJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<PoliticalOpinion> PoliticalOpinionLookup;
        [ReadOnly] public ComponentLookup<BehaviorState> BehaviorStateLookup;
        [ReadOnly] public ComponentLookup<SocialNetwork> SocialNetworkLookup;

        public uint CurrentFrame;
        public float InfluenceStrength;

        public void Execute(Entity entity, ref PoliticalOpinion opinion, in VoterData voterData, in BehaviorState behavior, in SocialNetwork network)
        {
            // Skip if not socially active or recently influenced
            if ((behavior.Flags & BehaviorFlags.IsApathetic) != 0)
                return;

            if (CurrentFrame - network.LastInteraction < 300) // 5 seconds at 60 FPS
                return;

            // Calculate influence susceptibility
            var susceptibility = network.SusceptibilityScore / 255f;
            var socialInfluence = behavior.SocialInfluence / 255f;
            var openness = behavior.Openness / 255f;

            var totalSusceptibility = (susceptibility + socialInfluence + openness) / 3f;

            // Apply influence based on network characteristics
            if (totalSusceptibility > 0.3f && network.NetworkSize > 2)
            {
                // Simulate influence from social connections
                // In a full implementation, this would iterate through actual connections

                // Echo chamber effect - reinforce existing views
                var echoChamberStrength = network.EchoChamberStrength / 255f;
                var diversityExposure = network.DiversityExposure / 255f;

                if (echoChamberStrength > 0.6f)
                {
                    // Reinforce existing strong opinions
                    if (math.abs(opinion.EconomicPosition) > 30)
                        opinion.EconomicPosition = (sbyte)math.clamp(opinion.EconomicPosition * 1.01f, -100, 100);
                    if (math.abs(opinion.SocialPosition) > 30)
                        opinion.SocialPosition = (sbyte)math.clamp(opinion.SocialPosition * 1.01f, -100, 100);
                }

                if (diversityExposure > 0.4f)
                {
                    // Moderate extreme positions slightly
                    opinion.EconomicPosition = (sbyte)math.lerp(opinion.EconomicPosition, 0, 0.001f);
                    opinion.SocialPosition = (sbyte)math.lerp(opinion.SocialPosition, 0, 0.001f);
                }

                // Increase confidence through social validation
                opinion.Confidence = (byte)math.min(255, opinion.Confidence + 1);
            }
        }
    }

    /// <summary>
    /// Job for updating voter behavior states based on events and experiences.
    /// </summary>
    [BurstCompile]
    public partial struct BehaviorUpdateJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<EventResponse> EventResponseLookup;

        public uint CurrentFrame;
        public float DeltaTime;

        public void Execute(ref BehaviorState behavior, in VoterData voterData, in EventResponse eventResponse)
        {
            // Update emotional states based on recent events
            var timeSinceLastEvent = CurrentFrame - eventResponse.LastEventFrame;

            if (timeSinceLastEvent < 3600) // Within last minute at 60 FPS
            {
                // Recent event affects emotional state
                var eventImpact = eventResponse.ResponseStrength / 255f;
                var emotionalResponse = eventResponse.EmotionalResponse / 255f;

                // Adjust satisfaction based on event response
                if (eventResponse.LastResponseType == ResponseType.Support)
                {
                    behavior.Satisfaction = (byte)math.min(255, behavior.Satisfaction + (eventImpact * 10));
                    behavior.Hope = (byte)math.min(255, behavior.Hope + (eventImpact * 5));
                }
                else if (eventResponse.LastResponseType == ResponseType.Opposition)
                {
                    behavior.Satisfaction = (byte)math.max(0, behavior.Satisfaction - (eventImpact * 10));
                    behavior.Anger = (byte)math.min(255, behavior.Anger + (eventImpact * 8));
                }

                // Emotional volatility affects neuroticism
                if (emotionalResponse > 0.7f)
                {
                    behavior.Neuroticism = (byte)math.min(255, behavior.Neuroticism + 1);
                }
            }
            else
            {
                // Gradual emotional recovery over time
                var recoveryRate = DeltaTime * 0.1f;

                behavior.Anxiety = (byte)math.max(50, behavior.Anxiety - (recoveryRate * 255));
                behavior.Anger = (byte)math.max(50, behavior.Anger - (recoveryRate * 255));
                behavior.Satisfaction = (byte)math.lerp(behavior.Satisfaction, 128, recoveryRate);
                behavior.Hope = (byte)math.lerp(behavior.Hope, 128, recoveryRate);
            }

            // Update engagement based on personality and recent activity
            var baseEngagement = (behavior.Extraversion + behavior.Conscientiousness) / 2f / 255f;
            var eventEngagement = eventResponse.SocialResponse / 255f;

            if (baseEngagement > 0.6f || eventEngagement > 0.5f)
            {
                behavior.Flags |= BehaviorFlags.IsEngaged;
            }
            else if (baseEngagement < 0.3f && eventEngagement < 0.2f)
            {
                behavior.Flags |= BehaviorFlags.IsApathetic;
                behavior.Flags &= ~BehaviorFlags.IsEngaged;
            }

            // Update influencer status based on network activity
            if (eventResponse.SocialResponse > 200 && (behavior.Flags & BehaviorFlags.IsEngaged) != 0)
            {
                behavior.Flags |= BehaviorFlags.IsInfluencer;
            }
        }
    }
}