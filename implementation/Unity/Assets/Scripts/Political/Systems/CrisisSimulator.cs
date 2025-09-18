using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Advanced crisis simulation system for Dutch political scenarios.
    /// Models complex multi-stage crises with realistic escalation patterns and political responses.
    /// </summary>
    public class CrisisSimulator
    {
        private readonly DutchPoliticalContext _politicalContext;
        private readonly List<CrisisTemplate> _crisisTemplates;
        private readonly System.Random _random;

        public CrisisSimulator(DutchPoliticalContext politicalContext)
        {
            _politicalContext = politicalContext;
            _random = new System.Random();
            _crisisTemplates = InitializeCrisisTemplates();

            Debug.Log($"Crisis Simulator initialized with {_crisisTemplates.Count} crisis templates");
        }

        public ActiveCrisis GenerateNewCrisis()
        {
            var template = SelectCrisisTemplate();
            var scenario = template.CrisisScenarios[_random.Next(template.CrisisScenarios.Count)];

            var crisis = new ActiveCrisis
            {
                CrisisName = scenario.CrisisName,
                CrisisType = template.CrisisType,
                Intensity = GenerateInitialIntensity(template),
                IntensityDecay = template.BaseIntensity,
                TimeRemaining = GenerateCrisisDuration(template),
                TensionModifier = template.TensionModifier,
                PossibleEvents = template.PossibleEvents.ToList(),
                Description = scenario.Description
            };

            Debug.Log($"New crisis generated: {crisis.CrisisName} (Type: {crisis.CrisisType}, Intensity: {crisis.Intensity:F2})");

            return crisis;
        }

        public PoliticalEventData GenerateCrisisEvent(ActiveCrisis crisis)
        {
            var template = _crisisTemplates.FirstOrDefault(t => t.CrisisType == crisis.CrisisType);
            if (template == null)
            {
                Debug.LogWarning($"No template found for crisis type: {crisis.CrisisType}");
                return GenerateFallbackEvent(crisis);
            }

            // Select escalation or resolution event based on crisis state
            var isEscalation = ShouldEscalateCrisis(crisis);
            var eventType = SelectEventType(crisis, isEscalation);

            var crisisEvent = GenerateEventForCrisisStage(template, crisis, eventType, isEscalation);

            // Update crisis intensity based on event
            if (isEscalation)
            {
                crisis.Intensity = math.min(crisis.Intensity + 0.2f, 1.0f);
                crisis.TimeRemaining += 30f; // Extend crisis duration
            }
            else
            {
                crisis.Intensity = math.max(crisis.Intensity - 0.15f, 0.1f);
            }

            return crisisEvent;
        }

        private CrisisTemplate SelectCrisisTemplate()
        {
            // Weight templates based on current conditions and seasonality
            var weightedTemplates = new List<(CrisisTemplate template, float weight)>();

            foreach (var template in _crisisTemplates)
            {
                float weight = CalculateCrisisWeight(template);
                weightedTemplates.Add((template, weight));
            }

            // Select based on weighted probability
            float totalWeight = weightedTemplates.Sum(w => w.weight);
            float randomValue = (float)_random.NextDouble() * totalWeight;
            float currentWeight = 0f;

            foreach (var (template, weight) in weightedTemplates)
            {
                currentWeight += weight;
                if (randomValue <= currentWeight)
                {
                    return template;
                }
            }

            return _crisisTemplates[_random.Next(_crisisTemplates.Count)];
        }

        private float CalculateCrisisWeight(CrisisTemplate template)
        {
            float weight = template.BaseProbability;

            // Seasonal adjustments
            var month = DateTime.Now.Month;
            switch (template.CrisisType)
            {
                case CrisisType.Environmental:
                    weight *= (month >= 6 && month <= 8) ? 2.0f : 1.0f; // Summer floods/heat
                    break;
                case CrisisType.Economic:
                    weight *= (month == 1 || month == 12) ? 1.5f : 1.0f; // Year-end pressures
                    break;
                case CrisisType.Health:
                    weight *= (month >= 10 && month <= 3) ? 1.3f : 1.0f; // Winter health issues
                    break;
                case CrisisType.Political:
                    weight *= (month >= 2 && month <= 4) ? 1.4f : 1.0f; // Election/budget season
                    break;
            }

            // Current issue relevance
            var currentIssues = _politicalContext.GetCurrentIssues();
            foreach (var issue in currentIssues)
            {
                if (template.RelatedIssues.Contains(issue.IssueName))
                {
                    weight *= (1f + issue.Importance * 0.5f);
                }
            }

            return weight;
        }

        private float GenerateInitialIntensity(CrisisTemplate template)
        {
            float baseIntensity = template.BaseIntensity;
            float variation = ((float)_random.NextDouble() - 0.5f) * 0.4f; // ±20% variation
            return math.clamp(baseIntensity + variation, 0.2f, 1.0f);
        }

        private float GenerateCrisisDuration(CrisisTemplate template)
        {
            float baseDuration = template.BaseDuration;
            float variation = ((float)_random.NextDouble() - 0.5f) * 0.6f; // ±30% variation
            return math.max(baseDuration + (baseDuration * variation), 60f);
        }

        private bool ShouldEscalateCrisis(ActiveCrisis crisis)
        {
            // Higher intensity crises more likely to escalate further
            float escalationProbability = 0.3f + (crisis.Intensity * 0.4f);

            // Reduce escalation probability as crisis ages
            float ageFactor = math.max(0.5f, crisis.TimeRemaining / 300f); // Reduce over 5 minutes
            escalationProbability *= ageFactor;

            return _random.NextDouble() < escalationProbability;
        }

        private PoliticalEventType SelectEventType(ActiveCrisis crisis, bool isEscalation)
        {
            var possibleEvents = crisis.PossibleEvents;
            if (possibleEvents.Count == 0)
            {
                return PoliticalEventType.PolicyAnnouncement; // Fallback
            }

            // Weight events based on escalation vs resolution
            if (isEscalation)
            {
                // Prefer more dramatic event types for escalation
                var dramaticEvents = possibleEvents.Where(e =>
                    e == PoliticalEventType.PoliticalScandal ||
                    e == PoliticalEventType.EnvironmentalCrisis ||
                    e == PoliticalEventType.HealthCrisis).ToList();

                if (dramaticEvents.Any())
                {
                    return dramaticEvents[_random.Next(dramaticEvents.Count)];
                }
            }

            return possibleEvents[_random.Next(possibleEvents.Count)];
        }

        private PoliticalEventData GenerateEventForCrisisStage(CrisisTemplate template, ActiveCrisis crisis,
            PoliticalEventType eventType, bool isEscalation)
        {
            var stageEvents = isEscalation ? template.EscalationEvents : template.ResolutionEvents;
            var relevantEvents = stageEvents.Where(e => e.EventType == eventType).ToList();

            if (!relevantEvents.Any())
            {
                relevantEvents = stageEvents; // Fallback to any stage event
            }

            if (!relevantEvents.Any())
            {
                return GenerateFallbackEvent(crisis); // Ultimate fallback
            }

            var selectedEvent = relevantEvents[_random.Next(relevantEvents.Count)];

            // Apply crisis intensity to event strength
            float intensityModifier = isEscalation ? 1.2f : 0.8f;
            float eventStrength = selectedEvent.BaseStrength * crisis.Intensity * intensityModifier;

            // Apply crisis context to opinion impact
            var opinionImpact = selectedEvent.OpinionImpact;
            opinionImpact *= crisis.Intensity * intensityModifier;

            return new PoliticalEventData
            {
                EventName = $"{selectedEvent.EventName} ({crisis.CrisisName})",
                EventType = eventType,
                OpinionImpact = opinionImpact,
                EventStrength = math.clamp(eventStrength, 0.1f, 1.0f),
                Duration = selectedEvent.BaseDuration,
                Description = $"{selectedEvent.Description} Related to ongoing {crisis.CrisisName}.",
                AffectedDemographics = template.AffectedDemographics.ToList(),
                PrimaryParty = DutchPoliticalParty.None
            };
        }

        private PoliticalEventData GenerateFallbackEvent(ActiveCrisis crisis)
        {
            return new PoliticalEventData
            {
                EventName = $"Crisis Update: {crisis.CrisisName}",
                EventType = PoliticalEventType.PolicyAnnouncement,
                OpinionImpact = new float3(-0.1f, -0.1f, 0.0f),
                EventStrength = crisis.Intensity * 0.5f,
                Duration = 30f,
                Description = $"Government announces measures to address ongoing {crisis.CrisisName}.",
                AffectedDemographics = new List<string> { "All Demographics" },
                PrimaryParty = DutchPoliticalParty.None
            };
        }

        private List<CrisisTemplate> InitializeCrisisTemplates()
        {
            return new List<CrisisTemplate>
            {
                // Economic Crisis
                new CrisisTemplate
                {
                    CrisisType = CrisisType.Economic,
                    BaseProbability = 0.2f,
                    BaseIntensity = 0.6f,
                    BaseDuration = 180f, // 3 minutes
                    TensionModifier = 0.3f,
                    RelatedIssues = new[] { "Economic Recovery", "Housing Crisis" },
                    AffectedDemographics = new[] { "Working Age", "Business Owners", "Lower Income" },
                    PossibleEvents = new[] { PoliticalEventType.EconomicNews, PoliticalEventType.PolicyAnnouncement, PoliticalEventType.PoliticalScandal },
                    CrisisScenarios = new List<CrisisScenario>
                    {
                        new CrisisScenario
                        {
                            CrisisName = "Housing Market Collapse",
                            Description = "Rapid decline in housing prices threatens economic stability"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Energy Price Crisis",
                            Description = "Soaring energy costs affect businesses and households"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Supply Chain Disruption",
                            Description = "Critical supply shortages impact multiple economic sectors"
                        }
                    },
                    EscalationEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Bank Lending Restrictions",
                            EventType = PoliticalEventType.EconomicNews,
                            Description = "Major banks tighten lending criteria, reducing business investment",
                            OpinionImpact = new float3(-0.4f, -0.1f, 0.0f),
                            BaseStrength = 0.7f,
                            BaseDuration = 45f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "Business Bankruptcy Wave",
                            EventType = PoliticalEventType.EconomicNews,
                            Description = "Multiple businesses declare bankruptcy due to economic pressures",
                            OpinionImpact = new float3(-0.5f, -0.2f, 0.0f),
                            BaseStrength = 0.8f,
                            BaseDuration = 60f
                        }
                    },
                    ResolutionEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Government Economic Stimulus",
                            EventType = PoliticalEventType.PolicyAnnouncement,
                            Description = "Large-scale government intervention to stabilize economy",
                            OpinionImpact = new float3(0.3f, 0.1f, -0.1f),
                            BaseStrength = 0.6f,
                            BaseDuration = 40f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "EU Financial Support",
                            EventType = PoliticalEventType.InternationalEvent,
                            Description = "European Union provides emergency financial assistance",
                            OpinionImpact = new float3(0.2f, 0.0f, 0.0f),
                            BaseStrength = 0.5f,
                            BaseDuration = 35f
                        }
                    }
                },

                // Environmental Crisis
                new CrisisTemplate
                {
                    CrisisType = CrisisType.Environmental,
                    BaseProbability = 0.25f,
                    BaseIntensity = 0.7f,
                    BaseDuration = 240f, // 4 minutes
                    TensionModifier = 0.4f,
                    RelatedIssues = new[] { "Climate Policy", "Agricultural Regulations" },
                    AffectedDemographics = new[] { "Rural Residents", "Young Adults", "Environmental Activists" },
                    PossibleEvents = new[] { PoliticalEventType.EnvironmentalCrisis, PoliticalEventType.PolicyAnnouncement, PoliticalEventType.SocialMovement },
                    CrisisScenarios = new List<CrisisScenario>
                    {
                        new CrisisScenario
                        {
                            CrisisName = "Delta Works Failure",
                            Description = "Critical failure in flood protection systems threatens major cities"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Agricultural Chemical Contamination",
                            Description = "Widespread soil and water contamination from agricultural chemicals"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Extreme Weather Events",
                            Description = "Series of unprecedented storms and floods across the Netherlands"
                        }
                    },
                    EscalationEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Mass Evacuation Ordered",
                            EventType = PoliticalEventType.EnvironmentalCrisis,
                            Description = "Government orders evacuation of low-lying areas due to flood risk",
                            OpinionImpact = new float3(-0.2f, 0.0f, 0.8f),
                            BaseStrength = 0.9f,
                            BaseDuration = 60f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "International Climate Emergency",
                            EventType = PoliticalEventType.InternationalEvent,
                            Description = "UN declares climate emergency, pressuring Netherlands for action",
                            OpinionImpact = new float3(-0.1f, 0.1f, 0.6f),
                            BaseStrength = 0.7f,
                            BaseDuration = 50f
                        }
                    },
                    ResolutionEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Emergency Climate Action Plan",
                            EventType = PoliticalEventType.PolicyAnnouncement,
                            Description = "Government announces comprehensive emergency climate response",
                            OpinionImpact = new float3(-0.2f, 0.2f, 0.7f),
                            BaseStrength = 0.8f,
                            BaseDuration = 45f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "International Aid Arrives",
                            EventType = PoliticalEventType.InternationalEvent,
                            Description = "EU and international partners provide emergency assistance",
                            OpinionImpact = new float3(0.0f, 0.1f, 0.3f),
                            BaseStrength = 0.5f,
                            BaseDuration = 30f
                        }
                    }
                },

                // Health Crisis
                new CrisisTemplate
                {
                    CrisisType = CrisisType.Health,
                    BaseProbability = 0.15f,
                    BaseIntensity = 0.8f,
                    BaseDuration = 300f, // 5 minutes
                    TensionModifier = 0.5f,
                    RelatedIssues = new[] { "Healthcare System" },
                    AffectedDemographics = new[] { "Older Adults", "Healthcare Workers", "Chronically Ill" },
                    PossibleEvents = new[] { PoliticalEventType.HealthCrisis, PoliticalEventType.PolicyAnnouncement, PoliticalEventType.SocialMovement },
                    CrisisScenarios = new List<CrisisScenario>
                    {
                        new CrisisScenario
                        {
                            CrisisName = "Hospital System Overload",
                            Description = "Healthcare system reaches critical capacity limits"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Infectious Disease Outbreak",
                            Description = "Rapid spread of infectious disease threatens public health"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Medical Supply Shortage",
                            Description = "Critical shortage of essential medical supplies and medications"
                        }
                    },
                    EscalationEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Healthcare Worker Strike",
                            EventType = PoliticalEventType.SocialMovement,
                            Description = "Medical staff strike over dangerous working conditions",
                            OpinionImpact = new float3(-0.3f, 0.2f, 0.0f),
                            BaseStrength = 0.8f,
                            BaseDuration = 50f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "Emergency Lockdown Measures",
                            EventType = PoliticalEventType.PolicyAnnouncement,
                            Description = "Government implements strict lockdown to control crisis",
                            OpinionImpact = new float3(-0.4f, -0.3f, 0.0f),
                            BaseStrength = 0.9f,
                            BaseDuration = 70f
                        }
                    },
                    ResolutionEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Emergency Healthcare Reinforcement",
                            EventType = PoliticalEventType.PolicyAnnouncement,
                            Description = "Military and international aid mobilized to support healthcare",
                            OpinionImpact = new float3(0.0f, 0.4f, 0.0f),
                            BaseStrength = 0.7f,
                            BaseDuration = 40f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "Crisis Successfully Contained",
                            EventType = PoliticalEventType.HealthCrisis,
                            Description = "Health authorities announce successful containment of crisis",
                            OpinionImpact = new float3(0.2f, 0.3f, 0.0f),
                            BaseStrength = 0.6f,
                            BaseDuration = 35f
                        }
                    }
                },

                // Political Crisis
                new CrisisTemplate
                {
                    CrisisType = CrisisType.Political,
                    BaseProbability = 0.18f,
                    BaseIntensity = 0.5f,
                    BaseDuration = 150f, // 2.5 minutes
                    TensionModifier = 0.6f,
                    RelatedIssues = new string[0],
                    AffectedDemographics = new[] { "All Demographics" },
                    PossibleEvents = new[] { PoliticalEventType.PoliticalScandal, PoliticalEventType.PolicyAnnouncement, PoliticalEventType.ElectionCampaign },
                    CrisisScenarios = new List<CrisisScenario>
                    {
                        new CrisisScenario
                        {
                            CrisisName = "Coalition Government Collapse",
                            Description = "Major disagreement causes coalition partners to withdraw support"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Corruption Investigation",
                            Description = "Serious allegations of corruption reach highest levels of government"
                        },
                        new CrisisScenario
                        {
                            CrisisName = "Constitutional Crisis",
                            Description = "Fundamental disagreement about constitutional interpretation"
                        }
                    },
                    EscalationEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "No Confidence Vote Called",
                            EventType = PoliticalEventType.PoliticalScandal,
                            Description = "Opposition parties call for vote of no confidence in government",
                            OpinionImpact = new float3(-0.2f, -0.5f, 0.0f),
                            BaseStrength = 0.8f,
                            BaseDuration = 55f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "Mass Protests Organized",
                            EventType = PoliticalEventType.SocialMovement,
                            Description = "Large-scale public demonstrations against government",
                            OpinionImpact = new float3(-0.1f, -0.4f, 0.0f),
                            BaseStrength = 0.7f,
                            BaseDuration = 45f
                        }
                    },
                    ResolutionEvents = new List<CrisisEventTemplate>
                    {
                        new CrisisEventTemplate
                        {
                            EventName = "Political Compromise Reached",
                            EventType = PoliticalEventType.PolicyAnnouncement,
                            Description = "Political parties reach agreement to resolve crisis",
                            OpinionImpact = new float3(0.1f, 0.3f, 0.0f),
                            BaseStrength = 0.6f,
                            BaseDuration = 40f
                        },
                        new CrisisEventTemplate
                        {
                            EventName = "Reform Package Announced",
                            EventType = PoliticalEventType.PolicyAnnouncement,
                            Description = "Government announces reforms to address underlying issues",
                            OpinionImpact = new float3(0.0f, 0.4f, 0.0f),
                            BaseStrength = 0.5f,
                            BaseDuration = 35f
                        }
                    }
                }
            };
        }
    }

    [Serializable]
    public class CrisisTemplate
    {
        public CrisisType CrisisType;
        public float BaseProbability;      // Likelihood of this crisis type occurring
        public float BaseIntensity;        // Initial intensity level
        public float BaseDuration;         // Base duration in seconds
        public float TensionModifier;      // How much this crisis increases political tension
        public string[] RelatedIssues;
        public string[] AffectedDemographics;
        public PoliticalEventType[] PossibleEvents;
        public List<CrisisScenario> CrisisScenarios;
        public List<CrisisEventTemplate> EscalationEvents;
        public List<CrisisEventTemplate> ResolutionEvents;
    }

    [Serializable]
    public class CrisisScenario
    {
        public string CrisisName;
        public string Description;
    }

    [Serializable]
    public class CrisisEventTemplate
    {
        public string EventName;
        public PoliticalEventType EventType;
        public string Description;
        public float3 OpinionImpact;
        public float BaseStrength;
        public float BaseDuration;
    }
}