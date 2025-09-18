using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Profiling;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// System responsible for creating and managing voter entities.
    /// Implements memory pool pattern for efficient voter lifecycle management.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VoterSpawningSystem : ISystem
    {
        private EntityArchetype _voterArchetype;
        private NativeArray<Entity> _voterPool;
        private NativeQueue<Entity> _availableVoters;
        private Random _random;

        // Dutch demographic distribution data
        private static readonly float[] AgeDistribution = { 0.16f, 0.12f, 0.13f, 0.20f, 0.18f, 0.21f }; // Age groups
        private static readonly float[] EducationDistribution = { 0.28f, 0.20f, 0.25f, 0.15f, 0.12f }; // Education levels
        private static readonly float[] RegionDistribution = {
            0.21f, 0.15f, 0.10f, 0.08f, 0.07f, 0.06f, 0.06f, 0.05f, 0.04f, 0.04f, 0.04f, 0.10f // Dutch provinces
        };

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create voter entity archetype
            _voterArchetype = state.EntityManager.CreateArchetype(
                typeof(VoterData),
                typeof(PoliticalOpinion),
                typeof(BehaviorState),
                typeof(SocialNetwork),
                typeof(EventResponse),
                typeof(AIAnalysisCache),
                typeof(SpatialPosition),
                typeof(MemoryPool)
            );

            _random = new Random((uint)System.DateTime.Now.Ticks);

            // Pre-allocate voter pool for performance
            var initialPoolSize = 10000; // Support up to 10k voters
            _voterPool = new NativeArray<Entity>(initialPoolSize, Allocator.Persistent);
            _availableVoters = new NativeQueue<Entity>(Allocator.Persistent);

            // Pre-create entities in pool
            using (var entities = state.EntityManager.CreateEntity(_voterArchetype, initialPoolSize, Allocator.Temp))
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    _voterPool[i] = entities[i];
                    _availableVoters.Enqueue(entities[i]);

                    // Initialize with inactive state
                    state.EntityManager.SetComponentData(entities[i], new MemoryPool
                    {
                        AllocationFrame = 0,
                        PoolIndex = (byte)(i % 256),
                        ReferenceCount = 0,
                        State = LifecycleState.Unallocated
                    });

                    // Disable entities initially
                    state.EntityManager.SetEnabled(entities[i], false);
                }
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system only handles initialization
            // Actual spawning is triggered by external systems calling SpawnVoters
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_voterPool.IsCreated)
                _voterPool.Dispose();
            if (_availableVoters.IsCreated)
                _availableVoters.Dispose();
        }

        /// <summary>
        /// Spawns the specified number of voters from the pool.
        /// </summary>
        public void SpawnVoters(ref SystemState state, int count)
        {
            using (PerformanceProfiler.BeginSample("VoterSpawning"))
            {
                var currentFrame = (uint)UnityEngine.Time.frameCount;
                var availableCount = _availableVoters.Count;
                var actualCount = math.min(count, availableCount);

                for (int i = 0; i < actualCount; i++)
                {
                    if (_availableVoters.TryDequeue(out var entity))
                    {
                        InitializeVoter(ref state, entity, currentFrame);
                        state.EntityManager.SetEnabled(entity, true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a voter entity to the pool for reuse.
        /// </summary>
        public void DespawnVoter(ref SystemState state, Entity entity)
        {
            // Reset to inactive state
            state.EntityManager.SetComponentData(entity, new MemoryPool
            {
                AllocationFrame = 0,
                PoolIndex = 0,
                ReferenceCount = 0,
                State = LifecycleState.Unallocated
            });

            state.EntityManager.SetEnabled(entity, false);
            _availableVoters.Enqueue(entity);
        }

        /// <summary>
        /// Initializes a voter entity with realistic Dutch demographic data.
        /// </summary>
        private void InitializeVoter(ref SystemState state, Entity entity, uint currentFrame)
        {
            var voterData = GenerateVoterData();
            var politicalOpinion = GeneratePoliticalOpinion(voterData);
            var behaviorState = GenerateBehaviorState(voterData);
            var socialNetwork = GenerateSocialNetwork(voterData, behaviorState);
            var eventResponse = new EventResponse
            {
                EmotionalResponse = (byte)_random.NextInt(100, 200),
                RationalResponse = (byte)_random.NextInt(100, 200),
                SocialResponse = (byte)_random.NextInt(50, 150),
                LastResponseType = ResponseType.None,
                ResponseStrength = 0,
                AttentionSpan = (byte)_random.NextInt(60, 255),
                LastEventFrame = 0,
                CumulativeImpact = 0
            };

            var aiCache = new AIAnalysisCache
            {
                PredictedPartySupport = 0,
                PredictedEngagement = 0,
                PredictedVolatility = 0,
                CachedAtFrame = 0,
                CacheConfidence = 0,
                Flags = AnalysisFlags.None,
                ContentHash = 0
            };

            var spatialPosition = new SpatialPosition
            {
                Position = new float3(
                    _random.NextFloat(-1000f, 1000f),
                    0,
                    _random.NextFloat(-1000f, 1000f)
                ),
                Velocity = float3.zero,
                Cluster = (byte)_random.NextInt(0, 16),
                Density = (byte)_random.NextInt(1, 10)
            };

            var memoryPool = new MemoryPool
            {
                AllocationFrame = currentFrame,
                PoolIndex = (byte)_random.NextInt(0, 256),
                ReferenceCount = 1,
                State = LifecycleState.Active
            };

            // Set all components
            state.EntityManager.SetComponentData(entity, voterData);
            state.EntityManager.SetComponentData(entity, politicalOpinion);
            state.EntityManager.SetComponentData(entity, behaviorState);
            state.EntityManager.SetComponentData(entity, socialNetwork);
            state.EntityManager.SetComponentData(entity, eventResponse);
            state.EntityManager.SetComponentData(entity, aiCache);
            state.EntityManager.SetComponentData(entity, spatialPosition);
            state.EntityManager.SetComponentData(entity, memoryPool);
        }

        /// <summary>
        /// Generates realistic Dutch voter demographic data.
        /// </summary>
        private VoterData GenerateVoterData()
        {
            var age = SampleFromDistribution(AgeDistribution) * 15 + 18; // Age groups: 18-32, 33-47, 48-62, 63-77, 78+
            var education = SampleFromDistribution(EducationDistribution) + 1; // 1-5 scale
            var region = SampleFromDistribution(RegionDistribution); // Dutch provinces
            var isUrban = _random.NextFloat() < 0.66f; // 66% urban in Netherlands

            var flags = VoterFlags.None;
            if (isUrban) flags |= VoterFlags.IsUrban;
            if (_random.NextFloat() < 0.42f) flags |= VoterFlags.HasChildren; // Dutch birth rate
            if (_random.NextFloat() < 0.69f) flags |= VoterFlags.IsEmployed; // Dutch employment rate
            if (_random.NextFloat() < 0.59f) flags |= VoterFlags.IsHomeowner; // Dutch homeownership rate
            if (_random.NextFloat() < 0.25f) flags |= VoterFlags.IsImmigrant; // Immigration background
            if (_random.NextFloat() < 0.07f) flags |= VoterFlags.IsDisabled; // Disability rate
            if (age < 25 && _random.NextFloat() < 0.3f) flags |= VoterFlags.IsStudent;
            if (age > 65) flags |= VoterFlags.IsRetired;

            return new VoterData
            {
                VoterId = _random.NextInt(1, int.MaxValue),
                Age = (int)age,
                EducationLevel = (byte)education,
                IncomePercentile = (byte)_random.NextInt(10, 90),
                Region = (byte)region,
                IsUrban = isUrban,
                Gender = (byte)_random.NextInt(0, 3),
                Religion = (byte)_random.NextInt(0, 8), // Various religions + none
                Flags = flags
            };
        }

        /// <summary>
        /// Generates political opinions based on voter demographics.
        /// </summary>
        private PoliticalOpinion GeneratePoliticalOpinion(VoterData voterData)
        {
            // Base positions influenced by demographics
            var economicBase = voterData.IncomePercentile > 60 ? 20 : -20; // Higher income = more right-wing
            var socialBase = voterData.Age < 40 ? 15 : -15; // Younger = more progressive
            var immigrationBase = (voterData.Flags & VoterFlags.IsUrban) != 0 ? 10 : -25; // Urban = more open
            var environmentBase = voterData.EducationLevel > 3 ? 25 : -10; // Higher education = more environmental

            // Add randomization
            var economicPosition = (sbyte)math.clamp(economicBase + _random.NextInt(-30, 30), -100, 100);
            var socialPosition = (sbyte)math.clamp(socialBase + _random.NextInt(-30, 30), -100, 100);
            var immigrationStance = (sbyte)math.clamp(immigrationBase + _random.NextInt(-40, 40), -100, 100);
            var environmentStance = (sbyte)math.clamp(environmentBase + _random.NextInt(-30, 30), -100, 100);

            // Generate party support based on positions
            var opinion = new PoliticalOpinion
            {
                EconomicPosition = economicPosition,
                SocialPosition = socialPosition,
                ImmigrationStance = immigrationStance,
                EnvironmentalStance = environmentStance,
                Confidence = (byte)_random.NextInt(120, 200),
                LastUpdated = (uint)UnityEngine.Time.frameCount
            };

            // Set party support based on political positions
            AssignPartySupport(ref opinion, voterData);

            return opinion;
        }

        /// <summary>
        /// Assigns party support based on political positions and demographics.
        /// </summary>
        private void AssignPartySupport(ref PoliticalOpinion opinion, VoterData voterData)
        {
            // VVD (Liberal Conservative) - right economic, moderate social
            if (opinion.EconomicPosition > 20 && opinion.SocialPosition > -30)
                opinion.VVDSupport = (byte)_random.NextInt(100, 200);

            // PVV (Populist Right) - anti-immigration, nationalist
            if (opinion.ImmigrationStance < -40 && voterData.EducationLevel < 3)
                opinion.PVVSupport = (byte)_random.NextInt(80, 180);

            // D66 (Social Liberal) - progressive social, moderate economic
            if (opinion.SocialPosition > 30 && opinion.EconomicPosition > -20 && voterData.EducationLevel > 3)
                opinion.D66Support = (byte)_random.NextInt(90, 190);

            // SP (Socialist) - left economic, traditional social
            if (opinion.EconomicPosition < -30 && opinion.SocialPosition < 20)
                opinion.SPSupport = (byte)_random.NextInt(70, 170);

            // PvdA (Social Democratic) - center-left economic, progressive social
            if (opinion.EconomicPosition < -10 && opinion.SocialPosition > 10)
                opinion.PvdASupport = (byte)_random.NextInt(80, 180);

            // GL (Green Left) - environmental, progressive
            if (opinion.EnvironmentalStance > 40 && opinion.SocialPosition > 30)
                opinion.GLSupport = (byte)_random.NextInt(90, 190);

            // CDA (Christian Democratic) - moderate positions, traditional values
            if (math.abs(opinion.EconomicPosition) < 30 && opinion.SocialPosition < 10 && voterData.Religion > 0)
                opinion.CDASupport = (byte)_random.NextInt(80, 180);

            // FvD (Right-wing Populist) - anti-establishment, nationalist
            if (opinion.ImmigrationStance < -30 && voterData.Age < 45 && voterData.EducationLevel > 2)
                opinion.FvDSupport = (byte)_random.NextInt(60, 160);

            // Volt (Pro-European) - progressive, pro-EU
            if (opinion.SocialPosition > 40 && voterData.Age < 35 && (voterData.Flags & VoterFlags.IsUrban) != 0)
                opinion.VoltSupport = (byte)_random.NextInt(70, 170);

            // Ensure at least some minimal support for a party
            if (GetTotalPartySupport(opinion) < 50)
            {
                // Assign random moderate support
                var randomParty = _random.NextInt(0, 9);
                SetPartySupport(ref opinion, randomParty, (byte)_random.NextInt(80, 120));
            }
        }

        private int GetTotalPartySupport(PoliticalOpinion opinion)
        {
            return opinion.VVDSupport + opinion.PVVSupport + opinion.CDASupport + opinion.D66Support +
                   opinion.SPSupport + opinion.PvdASupport + opinion.GLSupport + opinion.CUSupport +
                   opinion.SGPSupport + opinion.DENKSupport + opinion.FvDSupport + opinion.VoltSupport;
        }

        private void SetPartySupport(ref PoliticalOpinion opinion, int partyIndex, byte support)
        {
            switch (partyIndex)
            {
                case 0: opinion.VVDSupport = support; break;
                case 1: opinion.PVVSupport = support; break;
                case 2: opinion.CDASupport = support; break;
                case 3: opinion.D66Support = support; break;
                case 4: opinion.SPSupport = support; break;
                case 5: opinion.PvdASupport = support; break;
                case 6: opinion.GLSupport = support; break;
                case 7: opinion.CUSupport = support; break;
                case 8: opinion.FvDSupport = support; break;
            }
        }

        /// <summary>
        /// Generates behavior state based on voter demographics and personality.
        /// </summary>
        private BehaviorState GenerateBehaviorState(VoterData voterData)
        {
            // Generate Big Five personality traits
            var openness = GeneratePersonalityTrait(voterData.EducationLevel, voterData.Age);
            var conscientiousness = GeneratePersonalityTrait(voterData.Age / 10, voterData.EducationLevel);
            var extraversion = GeneratePersonalityTrait(voterData.Age < 40 ? 3 : 1, (voterData.Flags & VoterFlags.IsUrban) != 0 ? 2 : 0);
            var agreeableness = GeneratePersonalityTrait(2, voterData.Religion > 0 ? 2 : 0);
            var neuroticism = GeneratePersonalityTrait(voterData.Age < 30 ? 3 : 1, voterData.IncomePercentile < 40 ? 2 : 0);

            // Information processing traits
            var mediaConsumption = (byte)math.clamp(openness + _random.NextInt(-50, 50), 50, 255);
            var socialInfluence = (byte)math.clamp(extraversion + agreeableness / 2 + _random.NextInt(-40, 40), 30, 255);
            var authorityTrust = (byte)math.clamp(conscientiousness + (voterData.Age / 2) + _random.NextInt(-60, 60), 30, 255);
            var changeResistance = (byte)math.clamp((255 - openness) + voterData.Age + _random.NextInt(-50, 50), 50, 255);

            // Initial emotional state
            var satisfaction = (byte)_random.NextInt(80, 180);
            var anxiety = (byte)_random.NextInt(50, 150);
            var anger = (byte)_random.NextInt(30, 120);
            var hope = (byte)_random.NextInt(80, 180);

            // Generate behavior flags
            var flags = BehaviorFlags.None;
            if (mediaConsumption > 180) flags |= BehaviorFlags.IsEngaged;
            if (extraversion > 200 && socialInfluence > 180) flags |= BehaviorFlags.IsInfluencer;
            if (neuroticism > 180 && openness > 150) flags |= BehaviorFlags.IsVolatile;
            if (conscientiousness > 200 && changeResistance > 180) flags |= BehaviorFlags.IsLoyalist;
            if (anger > 150 && openness > 180) flags |= BehaviorFlags.IsProtester;
            if (mediaConsumption < 100 && socialInfluence < 80) flags |= BehaviorFlags.IsApathetic;
            if (openness > 220) flags |= BehaviorFlags.IsEarlyAdopter;

            return new BehaviorState
            {
                Openness = openness,
                Conscientiousness = conscientiousness,
                Extraversion = extraversion,
                Agreeableness = agreeableness,
                Neuroticism = neuroticism,
                MediaConsumption = mediaConsumption,
                SocialInfluence = socialInfluence,
                AuthorityTrust = authorityTrust,
                ChangeResistance = changeResistance,
                Satisfaction = satisfaction,
                Anxiety = anxiety,
                Anger = anger,
                Hope = hope,
                Flags = flags
            };
        }

        /// <summary>
        /// Generates social network characteristics.
        /// </summary>
        private SocialNetwork GenerateSocialNetwork(VoterData voterData, BehaviorState behavior)
        {
            var baseNetworkSize = (voterData.Flags & VoterFlags.IsUrban) != 0 ? 120 : 80;
            var networkSize = (byte)math.clamp(baseNetworkSize + behavior.Extraversion / 4 + _random.NextInt(-30, 30), 20, 255);

            var influenceScore = (byte)math.clamp((behavior.Extraversion + behavior.Conscientiousness) / 2 + _random.NextInt(-40, 40), 10, 255);
            var susceptibilityScore = (byte)math.clamp(behavior.Agreeableness + (255 - behavior.Conscientiousness) / 2 + _random.NextInt(-50, 50), 30, 255);

            var echoChamberStrength = (byte)_random.NextInt(60, 200);
            var diversityExposure = (voterData.Flags & VoterFlags.IsUrban) != 0 ? (byte)_random.NextInt(100, 200) : (byte)_random.NextInt(50, 150);

            return new SocialNetwork
            {
                NetworkSize = networkSize,
                InfluenceScore = influenceScore,
                SusceptibilityScore = susceptibilityScore,
                FamilyConnections = (byte)_random.NextInt(50, 150),
                WorkConnections = (voterData.Flags & VoterFlags.IsEmployed) != 0 ? (byte)_random.NextInt(80, 180) : (byte)_random.NextInt(20, 80),
                SocialConnections = (byte)math.clamp(behavior.Extraversion + _random.NextInt(-40, 40), 30, 255),
                OnlineConnections = voterData.Age < 50 ? (byte)_random.NextInt(100, 220) : (byte)_random.NextInt(30, 120),
                EchoChamberStrength = echoChamberStrength,
                DiversityExposure = diversityExposure,
                LastInteraction = 0
            };
        }

        private byte GeneratePersonalityTrait(int baseFactor, int modifier)
        {
            var base_value = baseFactor * 30 + modifier * 20;
            return (byte)math.clamp(base_value + _random.NextInt(-60, 60), 30, 255);
        }

        private int SampleFromDistribution(float[] distribution)
        {
            var random = _random.NextFloat();
            var cumulative = 0f;
            for (int i = 0; i < distribution.Length; i++)
            {
                cumulative += distribution[i];
                if (random <= cumulative)
                    return i;
            }
            return distribution.Length - 1;
        }
    }
}