using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using SovereignsDilemma.Core.EventBus;
using SovereignsDilemma.Political.Components;
using SovereignsDilemma.Testing.Performance;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Advanced political event system for crisis simulation and Dutch political dynamics.
    /// Generates realistic political scenarios that affect voter behavior patterns.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PoliticalEventSystem : SystemBase
    {
        private EventBusSystem _eventBus;

        // Event generation timing
        private float _lastEventTime;
        private float _nextEventInterval = 30f; // Base interval in seconds

        // Dutch political context
        private readonly DutchPoliticalContext _politicalContext;
        private readonly PoliticalEventGenerator _eventGenerator;
        private readonly CrisisSimulator _crisisSimulator;

        // Active crisis tracking
        private List<ActiveCrisis> _activeCrises;
        private List<ActivePoliticalEvent> _activeEvents;

        // Performance tracking
        private int _eventsGeneratedThisSession;
        private float _totalEventImpact;

        protected override void OnCreate()
        {
            _eventBus = World.GetOrCreateSystemManaged<EventBusSystem>();
            _politicalContext = new DutchPoliticalContext();
            _eventGenerator = new PoliticalEventGenerator(_politicalContext);
            _crisisSimulator = new CrisisSimulator(_politicalContext);

            _activeCrises = new List<ActiveCrisis>();
            _activeEvents = new List<ActivePoliticalEvent>();

            Debug.Log("Political Event System initialized with Dutch political context");
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            // Update active events and crises
            UpdateActiveEvents(deltaTime);
            UpdateActiveCrises(deltaTime);

            // Generate new events based on timing and conditions
            if (ShouldGenerateEvent(currentTime))
            {
                GenerateNextEvent(currentTime);
                _lastEventTime = currentTime;
            }

            // Apply political event effects to voters
            ApplyEventEffectsToVoters();

            // Performance monitoring
            PerformanceProfiler.RecordMeasurement("ActivePoliticalEvents", _activeEvents.Count);
            PerformanceProfiler.RecordMeasurement("ActiveCrises", _activeCrises.Count);
        }

        private bool ShouldGenerateEvent(float currentTime)
        {
            if (currentTime - _lastEventTime < _nextEventInterval)
                return false;

            // Dynamic event frequency based on current political climate
            float politicalTension = CalculatePoliticalTension();
            float crisisModifier = _activeCrises.Count > 0 ? 1.5f : 1.0f;

            // Higher tension = more frequent events
            float eventProbability = (politicalTension * crisisModifier * Time.deltaTime) / _nextEventInterval;

            return UnityEngine.Random.value < eventProbability;
        }

        private float CalculatePoliticalTension()
        {
            float baseTension = 0.3f;

            // Increase tension based on active crises
            foreach (var crisis in _activeCrises)
            {
                baseTension += crisis.TensionModifier;
            }

            // Increase tension based on recent events
            foreach (var politicalEvent in _activeEvents.Where(e => e.TimeRemaining > 0))
            {
                baseTension += politicalEvent.EventData.EventStrength * 0.1f;
            }

            return math.clamp(baseTension, 0.1f, 2.0f);
        }

        private void GenerateNextEvent(float currentTime)
        {
            PoliticalEventData eventData;

            // 30% chance to escalate existing crisis
            if (_activeCrises.Count > 0 && UnityEngine.Random.value < 0.3f)
            {
                var crisis = _activeCrises[UnityEngine.Random.Range(0, _activeCrises.Count)];
                eventData = _crisisSimulator.GenerateCrisisEvent(crisis);
            }
            // 20% chance to start new crisis
            else if (UnityEngine.Random.value < 0.2f)
            {
                var newCrisis = _crisisSimulator.GenerateNewCrisis();
                _activeCrises.Add(newCrisis);
                eventData = _crisisSimulator.GenerateCrisisEvent(newCrisis);
            }
            // 50% chance for regular political event
            else
            {
                eventData = _eventGenerator.GenerateRandomEvent();
            }

            // Create and publish the event
            var activeEvent = new ActivePoliticalEvent
            {
                EventData = eventData,
                StartTime = currentTime,
                Duration = eventData.Duration,
                TimeRemaining = eventData.Duration,
                AffectedVoters = new List<int>()
            };

            _activeEvents.Add(activeEvent);

            // Publish event to event bus
            var busEvent = new PoliticalEventOccurredEvent(
                eventData.EventName,
                eventData.EventType,
                eventData.OpinionImpact,
                eventData.EventStrength,
                eventData.Description
            );

            _eventBus.Publish(busEvent, "Political");

            // Update next event timing
            _nextEventInterval = UnityEngine.Random.Range(15f, 60f);
            _eventsGeneratedThisSession++;

            Debug.Log($"Generated political event: {eventData.EventName} (Strength: {eventData.EventStrength:F2})");
        }

        private void UpdateActiveEvents(float deltaTime)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var activeEvent = _activeEvents[i];
                activeEvent.TimeRemaining -= deltaTime;

                if (activeEvent.TimeRemaining <= 0)
                {
                    // Event has expired
                    Debug.Log($"Political event expired: {activeEvent.EventData.EventName}");
                    _activeEvents.RemoveAt(i);
                }
                else
                {
                    _activeEvents[i] = activeEvent;
                }
            }
        }

        private void UpdateActiveCrises(float deltaTime)
        {
            for (int i = _activeCrises.Count - 1; i >= 0; i--)
            {
                var crisis = _activeCrises[i];
                crisis.TimeRemaining -= deltaTime;
                crisis.IntensityDecay -= deltaTime * 0.1f; // Gradual intensity decay

                if (crisis.TimeRemaining <= 0 || crisis.IntensityDecay <= 0.1f)
                {
                    Debug.Log($"Crisis resolved: {crisis.CrisisName}");
                    _activeCrises.RemoveAt(i);
                }
                else
                {
                    _activeCrises[i] = crisis;
                }
            }
        }

        private void ApplyEventEffectsToVoters()
        {
            if (_activeEvents.Count == 0) return;

            // Query all voters to apply event effects
            var voterQuery = SystemAPI.QueryBuilder()
                .WithAll<VoterData, PoliticalOpinion, BehaviorState>()
                .Build();

            var voters = voterQuery.ToEntityArray(Allocator.Temp);
            var voterDataArray = voterQuery.ToComponentDataArray<VoterData>(Allocator.Temp);
            var opinionArray = voterQuery.ToComponentDataArray<PoliticalOpinion>(Allocator.Temp);
            var behaviorArray = voterQuery.ToComponentDataArray<BehaviorState>(Allocator.Temp);

            try
            {
                for (int i = 0; i < voters.Length; i++)
                {
                    var voter = voterDataArray[i];
                    var opinion = opinionArray[i];
                    var behavior = behaviorArray[i];

                    bool opinionChanged = false;

                    foreach (var activeEvent in _activeEvents)
                    {
                        if (ShouldVoterBeAffected(voter, activeEvent.EventData))
                        {
                            var oldOpinion = opinion;
                            ApplyEventToVoter(ref opinion, ref behavior, activeEvent.EventData, voter);

                            if (!oldOpinion.Equals(opinion))
                            {
                                opinionChanged = true;
                                activeEvent.AffectedVoters.Add(voter.VoterId);

                                // Publish voter opinion change event
                                var changeEvent = new VoterOpinionChangedEvent(
                                    voter.VoterId,
                                    oldOpinion,
                                    opinion,
                                    OpinionChangeReason.PoliticalEvent,
                                    activeEvent.EventData.EventStrength
                                );

                                _eventBus.Publish(changeEvent, "Political");
                            }
                        }
                    }

                    if (opinionChanged)
                    {
                        // Update the components
                        SystemAPI.SetComponent(voters[i], opinion);
                        SystemAPI.SetComponent(voters[i], behavior);
                    }
                }
            }
            finally
            {
                voters.Dispose();
                voterDataArray.Dispose();
                opinionArray.Dispose();
                behaviorArray.Dispose();
            }
        }

        private bool ShouldVoterBeAffected(VoterData voter, PoliticalEventData eventData)
        {
            // Base probability of being affected
            float baseProbability = 0.3f;

            // Demographic factors
            float ageFactor = CalculateAgeFactor(voter.Age, eventData.EventType);
            float educationFactor = CalculateEducationFactor(voter.EducationLevel, eventData.EventType);
            float incomeFactor = CalculateIncomeFactor(voter.IncomeLevel, eventData.EventType);

            float totalProbability = baseProbability * ageFactor * educationFactor * incomeFactor * eventData.EventStrength;

            return UnityEngine.Random.value < math.clamp(totalProbability, 0.05f, 0.95f);
        }

        private float CalculateAgeFactor(int age, PoliticalEventType eventType)
        {
            return eventType switch
            {
                PoliticalEventType.SocialMovement => age < 35 ? 1.5f : 0.8f,
                PoliticalEventType.EconomicNews => age > 40 ? 1.3f : 0.9f,
                PoliticalEventType.EnvironmentalCrisis => age < 45 ? 1.4f : 0.7f,
                PoliticalEventType.HealthCrisis => age > 50 ? 1.6f : 0.8f,
                _ => 1.0f
            };
        }

        private float CalculateEducationFactor(EducationLevel education, PoliticalEventType eventType)
        {
            return eventType switch
            {
                PoliticalEventType.PolicyAnnouncement => education >= EducationLevel.Higher ? 1.3f : 0.9f,
                PoliticalEventType.PoliticalScandal => education <= EducationLevel.Secondary ? 1.2f : 1.0f,
                PoliticalEventType.InternationalEvent => education >= EducationLevel.Higher ? 1.4f : 0.8f,
                _ => 1.0f
            };
        }

        private float CalculateIncomeFactor(IncomeLevel income, PoliticalEventType eventType)
        {
            return eventType switch
            {
                PoliticalEventType.EconomicNews => income <= IncomeLevel.Low ? 1.5f : 1.0f,
                PoliticalEventType.PolicyAnnouncement => income >= IncomeLevel.High ? 1.2f : 1.0f,
                _ => 1.0f
            };
        }

        private void ApplyEventToVoter(ref PoliticalOpinion opinion, ref BehaviorState behavior,
            PoliticalEventData eventData, VoterData voter)
        {
            // Apply opinion impact based on voter receptivity
            float receptivity = CalculateVoterReceptivity(voter, eventData);
            float effectStrength = eventData.EventStrength * receptivity;

            // Apply economic impact
            opinion.EconomicPosition = math.clamp(
                opinion.EconomicPosition + eventData.OpinionImpact.x * effectStrength,
                -1f, 1f);

            // Apply social impact
            opinion.SocialPosition = math.clamp(
                opinion.SocialPosition + eventData.OpinionImpact.y * effectStrength,
                -1f, 1f);

            // Apply environmental impact
            opinion.EnvironmentalPosition = math.clamp(
                opinion.EnvironmentalPosition + eventData.OpinionImpact.z * effectStrength,
                -1f, 1f);

            // Update voting intention based on new positions
            opinion.VotingIntention = _politicalContext.CalculateVotingIntention(opinion);

            // Potentially affect behavior based on event intensity
            if (effectStrength > 0.7f)
            {
                behavior.PoliticalEngagement = math.clamp(behavior.PoliticalEngagement + 0.1f, 0f, 1f);

                if (effectStrength > 0.9f)
                {
                    behavior.SocialInfluence = math.clamp(behavior.SocialInfluence + 0.05f, 0f, 1f);
                }
            }

            _totalEventImpact += effectStrength;
        }

        private float CalculateVoterReceptivity(VoterData voter, PoliticalEventData eventData)
        {
            float baseReceptivity = 0.5f;

            // Age-based receptivity
            if (voter.Age < 30) baseReceptivity += 0.2f; // Young voters more receptive
            else if (voter.Age > 60) baseReceptivity -= 0.1f; // Older voters less receptive

            // Education-based receptivity
            if (voter.EducationLevel >= EducationLevel.Higher) baseReceptivity += 0.15f;

            // Political engagement affects receptivity
            // Note: This would need to be retrieved from BehaviorState component
            baseReceptivity += 0.1f; // Placeholder for political engagement factor

            return math.clamp(baseReceptivity, 0.1f, 1.0f);
        }

        public PoliticalEventSystemMetrics GetMetrics()
        {
            return new PoliticalEventSystemMetrics
            {
                ActiveEventsCount = _activeEvents.Count,
                ActiveCrisesCount = _activeCrises.Count,
                EventsGeneratedThisSession = _eventsGeneratedThisSession,
                TotalEventImpact = _totalEventImpact,
                CurrentPoliticalTension = CalculatePoliticalTension()
            };
        }

        protected override void OnDestroy()
        {
            Debug.Log($"Political Event System shutting down. Generated {_eventsGeneratedThisSession} events this session.");
        }
    }

    // Supporting data structures
    [Serializable]
    public struct PoliticalEventData
    {
        public string EventName;
        public PoliticalEventType EventType;
        public float3 OpinionImpact; // Economic, Social, Environmental
        public float EventStrength;
        public float Duration;
        public string Description;
        public List<string> AffectedDemographics;
        public DutchPoliticalParty PrimaryParty;
    }

    [Serializable]
    public class ActivePoliticalEvent
    {
        public PoliticalEventData EventData;
        public float StartTime;
        public float Duration;
        public float TimeRemaining;
        public List<int> AffectedVoters;
    }

    [Serializable]
    public class ActiveCrisis
    {
        public string CrisisName;
        public CrisisType CrisisType;
        public float Intensity;
        public float IntensityDecay;
        public float TimeRemaining;
        public float TensionModifier;
        public List<PoliticalEventType> PossibleEvents;
        public string Description;
    }

    public enum CrisisType
    {
        Economic,
        Health,
        Environmental,
        Political,
        Social,
        International
    }

    public enum DutchPoliticalParty
    {
        VVD,           // People's Party for Freedom and Democracy
        PVV,           // Party for Freedom
        CDA,           // Christian Democratic Appeal
        D66,           // Democrats 66
        GL,            // GreenLeft
        SP,            // Socialist Party
        PvdA,          // Labour Party
        ChristenUnie,  // ChristianUnion
        Volt,          // Volt Netherlands
        JA21,          // JA21
        SGP,           // Reformed Political Party
        DENK,          // DENK
        FvD,           // Forum for Democracy
        PvdD,          // Party for the Animals
        BBB,           // BoerBurgerBeweging (Farmer-Citizen Movement)
        None           // No specific party association
    }

    public struct PoliticalEventSystemMetrics
    {
        public int ActiveEventsCount;
        public int ActiveCrisesCount;
        public int EventsGeneratedThisSession;
        public float TotalEventImpact;
        public float CurrentPoliticalTension;
    }
}