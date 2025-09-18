using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Advanced political event generator for realistic Dutch political scenarios.
    /// Creates events based on current political climate, demographic factors, and historical patterns.
    /// </summary>
    public class PoliticalEventGenerator
    {
        private readonly DutchPoliticalContext _politicalContext;
        private readonly List<EventTemplate> _eventTemplates;
        private readonly System.Random _random;

        public PoliticalEventGenerator(DutchPoliticalContext politicalContext)
        {
            _politicalContext = politicalContext;
            _random = new System.Random();
            _eventTemplates = InitializeEventTemplates();

            Debug.Log($"Political Event Generator initialized with {_eventTemplates.Count} event templates");
        }

        public PoliticalEventData GenerateRandomEvent()
        {
            // Select event template based on current political climate
            var template = SelectEventTemplate();

            // Generate specific event from template
            return GenerateEventFromTemplate(template);
        }

        public PoliticalEventData GenerateEventForParty(DutchPoliticalParty party)
        {
            var partyProfile = _politicalContext.GetPartyProfile(party);
            var relevantTemplates = _eventTemplates
                .Where(t => t.RelevantParties.Contains(party) || t.RelevantParties.Contains(DutchPoliticalParty.None))
                .ToList();

            var template = relevantTemplates[_random.Next(relevantTemplates.Count)];
            var eventData = GenerateEventFromTemplate(template);
            eventData.PrimaryParty = party;

            return eventData;
        }

        public PoliticalEventData GenerateEventForIssue(string issueName)
        {
            var relevantTemplates = _eventTemplates
                .Where(t => t.RelatedIssues.Contains(issueName))
                .ToList();

            if (!relevantTemplates.Any())
            {
                return GenerateRandomEvent(); // Fallback to random event
            }

            var template = relevantTemplates[_random.Next(relevantTemplates.Count)];
            return GenerateEventFromTemplate(template);
        }

        private EventTemplate SelectEventTemplate()
        {
            // Weight templates based on current political importance
            var weightedTemplates = new List<(EventTemplate template, float weight)>();

            foreach (var template in _eventTemplates)
            {
                float weight = CalculateTemplateWeight(template);
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

            // Fallback to random template
            return _eventTemplates[_random.Next(_eventTemplates.Count)];
        }

        private float CalculateTemplateWeight(EventTemplate template)
        {
            float weight = template.BaseFrequency;

            // Increase weight based on related current issues
            var currentIssues = _politicalContext.GetCurrentIssues();
            foreach (var issue in currentIssues)
            {
                if (template.RelatedIssues.Contains(issue.IssueName))
                {
                    weight *= (1f + issue.Importance);
                }
            }

            // Seasonal adjustments
            weight *= GetSeasonalModifier(template);

            return weight;
        }

        private float GetSeasonalModifier(EventTemplate template)
        {
            var month = DateTime.Now.Month;

            return template.EventType switch
            {
                PoliticalEventType.EnvironmentalCrisis => month >= 6 && month <= 8 ? 1.5f : 1.0f, // Summer
                PoliticalEventType.EconomicNews => month == 1 || month == 12 ? 1.3f : 1.0f, // Year-end
                PoliticalEventType.PolicyAnnouncement => month >= 9 && month <= 11 ? 1.4f : 1.0f, // Budget season
                PoliticalEventType.ElectionCampaign => month >= 2 && month <= 4 ? 1.6f : 0.8f, // Campaign season
                _ => 1.0f
            };
        }

        private PoliticalEventData GenerateEventFromTemplate(EventTemplate template)
        {
            var variants = template.EventVariants;
            var selectedVariant = variants[_random.Next(variants.Count)];

            // Apply randomization to impact values
            var economicImpact = ApplyRandomVariation(selectedVariant.OpinionImpact.x, template.ImpactVariation);
            var socialImpact = ApplyRandomVariation(selectedVariant.OpinionImpact.y, template.ImpactVariation);
            var environmentalImpact = ApplyRandomVariation(selectedVariant.OpinionImpact.z, template.ImpactVariation);

            var eventStrength = ApplyRandomVariation(template.BaseStrength, 0.3f);
            var duration = ApplyRandomVariation(template.BaseDuration, 0.4f);

            return new PoliticalEventData
            {
                EventName = selectedVariant.EventName,
                EventType = template.EventType,
                OpinionImpact = new float3(economicImpact, socialImpact, environmentalImpact),
                EventStrength = math.clamp(eventStrength, 0.1f, 1.0f),
                Duration = math.max(duration, 5f),
                Description = selectedVariant.Description,
                AffectedDemographics = template.AffectedDemographics.ToList(),
                PrimaryParty = template.RelevantParties.FirstOrDefault()
            };
        }

        private float ApplyRandomVariation(float baseValue, float variationPercentage)
        {
            float variation = (float)_random.NextDouble() * 2f - 1f; // -1 to 1
            return baseValue + (baseValue * variation * variationPercentage);
        }

        private List<EventTemplate> InitializeEventTemplates()
        {
            return new List<EventTemplate>
            {
                // Economic Events
                new EventTemplate
                {
                    EventType = PoliticalEventType.EconomicNews,
                    BaseFrequency = 0.3f,
                    BaseStrength = 0.6f,
                    BaseDuration = 45f,
                    ImpactVariation = 0.3f,
                    RelevantParties = new[] { DutchPoliticalParty.VVD, DutchPoliticalParty.D66, DutchPoliticalParty.PvdA },
                    RelatedIssues = new[] { "Economic Recovery", "Housing Crisis" },
                    AffectedDemographics = new[] { "Working Age", "Business Owners", "Middle Income" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "Inflation Rate Increases",
                            Description = "Central Bank reports rising inflation, affecting household purchasing power",
                            OpinionImpact = new float3(-0.3f, 0.0f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Tech Sector Growth",
                            Description = "Major tech companies announce expansion in Netherlands, creating jobs",
                            OpinionImpact = new float3(0.4f, 0.1f, -0.1f)
                        },
                        new EventVariant
                        {
                            EventName = "Trade Deal Signed",
                            Description = "Netherlands signs beneficial trade agreement with major partner",
                            OpinionImpact = new float3(0.3f, 0.0f, 0.0f)
                        }
                    }
                },

                // Environmental Events
                new EventTemplate
                {
                    EventType = PoliticalEventType.EnvironmentalCrisis,
                    BaseFrequency = 0.25f,
                    BaseStrength = 0.7f,
                    BaseDuration = 60f,
                    ImpactVariation = 0.4f,
                    RelevantParties = new[] { DutchPoliticalParty.GL, DutchPoliticalParty.PvdD, DutchPoliticalParty.D66 },
                    RelatedIssues = new[] { "Climate Policy", "Agricultural Regulations" },
                    AffectedDemographics = new[] { "Young Adults", "Urban Residents", "Highly Educated" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "Severe Flooding in Delta Region",
                            Description = "Climate change-related flooding affects thousands of homes",
                            OpinionImpact = new float3(-0.2f, 0.1f, 0.8f)
                        },
                        new EventVariant
                        {
                            EventName = "EU Climate Targets Announced",
                            Description = "Stricter EU emissions targets require significant policy changes",
                            OpinionImpact = new float3(-0.3f, 0.0f, 0.6f)
                        },
                        new EventVariant
                        {
                            EventName = "Record Heat Wave",
                            Description = "Unprecedented temperatures highlight urgency of climate action",
                            OpinionImpact = new float3(-0.1f, 0.0f, 0.7f)
                        }
                    }
                },

                // Immigration and Social Events
                new EventTemplate
                {
                    EventType = PoliticalEventType.SocialMovement,
                    BaseFrequency = 0.2f,
                    BaseStrength = 0.5f,
                    BaseDuration = 30f,
                    ImpactVariation = 0.5f,
                    RelevantParties = new[] { DutchPoliticalParty.PVV, DutchPoliticalParty.GL, DutchPoliticalParty.DENK },
                    RelatedIssues = new[] { "Immigration and Integration" },
                    AffectedDemographics = new[] { "Lower Income", "Rural Residents", "Urban Minorities" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "Integration Success Stories Highlighted",
                            Description = "Media covers successful immigrant entrepreneurs and community leaders",
                            OpinionImpact = new float3(0.1f, 0.3f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Asylum Seeker Accommodation Crisis",
                            Description = "Shortage of housing for asylum seekers creates local tensions",
                            OpinionImpact = new float3(-0.1f, -0.4f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Multicultural Festival Success",
                            Description = "Large-scale cultural event celebrates diversity and integration",
                            OpinionImpact = new float3(0.0f, 0.4f, 0.0f)
                        }
                    }
                },

                // Political Scandals
                new EventTemplate
                {
                    EventType = PoliticalEventType.PoliticalScandal,
                    BaseFrequency = 0.15f,
                    BaseStrength = 0.8f,
                    BaseDuration = 75f,
                    ImpactVariation = 0.6f,
                    RelevantParties = new[] { DutchPoliticalParty.None }, // Can affect any party
                    RelatedIssues = new string[0],
                    AffectedDemographics = new[] { "All Demographics" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "Coalition Partner Disagreement",
                            Description = "Major policy disagreement threatens government stability",
                            OpinionImpact = new float3(0.0f, -0.3f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Minister Resignation",
                            Description = "Cabinet minister resigns over policy failure or scandal",
                            OpinionImpact = new float3(-0.2f, -0.4f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Parliamentary Investigation Announced",
                            Description = "Serious allegations prompt formal parliamentary inquiry",
                            OpinionImpact = new float3(-0.1f, -0.5f, 0.0f)
                        }
                    }
                },

                // Healthcare Events
                new EventTemplate
                {
                    EventType = PoliticalEventType.HealthCrisis,
                    BaseFrequency = 0.2f,
                    BaseStrength = 0.6f,
                    BaseDuration = 50f,
                    ImpactVariation = 0.4f,
                    RelevantParties = new[] { DutchPoliticalParty.SP, DutchPoliticalParty.PvdA, DutchPoliticalParty.ChristenUnie },
                    RelatedIssues = new[] { "Healthcare System" },
                    AffectedDemographics = new[] { "Older Adults", "Lower Income", "Chronically Ill" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "Hospital Staff Shortage Crisis",
                            Description = "Critical shortage of nurses and doctors affects patient care",
                            OpinionImpact = new float3(-0.3f, 0.2f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Healthcare Innovation Success",
                            Description = "Dutch medical breakthrough gains international recognition",
                            OpinionImpact = new float3(0.2f, 0.3f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "Mental Health Awareness Campaign",
                            Description = "Government launches major mental health support initiative",
                            OpinionImpact = new float3(0.0f, 0.4f, 0.0f)
                        }
                    }
                },

                // Agricultural/Rural Events
                new EventTemplate
                {
                    EventType = PoliticalEventType.PolicyAnnouncement,
                    BaseFrequency = 0.18f,
                    BaseStrength = 0.5f,
                    BaseDuration = 40f,
                    ImpactVariation = 0.3f,
                    RelevantParties = new[] { DutchPoliticalParty.BBB, DutchPoliticalParty.CDA, DutchPoliticalParty.GL },
                    RelatedIssues = new[] { "Agricultural Regulations", "Climate Policy" },
                    AffectedDemographics = new[] { "Rural Residents", "Farmers", "Traditional Communities" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "Nitrogen Emission Limits Tightened",
                            Description = "Stricter environmental regulations impact farming operations",
                            OpinionImpact = new float3(-0.4f, -0.2f, 0.5f)
                        },
                        new EventVariant
                        {
                            EventName = "Agricultural Innovation Funding",
                            Description = "Government announces support for sustainable farming technology",
                            OpinionImpact = new float3(0.2f, 0.0f, 0.3f)
                        },
                        new EventVariant
                        {
                            EventName = "Farmer Protest Movement",
                            Description = "Large-scale farmer protests against environmental regulations",
                            OpinionImpact = new float3(0.1f, -0.3f, -0.4f)
                        }
                    }
                },

                // International Events
                new EventTemplate
                {
                    EventType = PoliticalEventType.InternationalEvent,
                    BaseFrequency = 0.12f,
                    BaseStrength = 0.4f,
                    BaseDuration = 35f,
                    ImpactVariation = 0.5f,
                    RelevantParties = new[] { DutchPoliticalParty.D66, DutchPoliticalParty.Volt, DutchPoliticalParty.FvD },
                    RelatedIssues = new string[0],
                    AffectedDemographics = new[] { "Highly Educated", "Urban Residents", "International Workers" },
                    EventVariants = new List<EventVariant>
                    {
                        new EventVariant
                        {
                            EventName = "EU Summit Agreement",
                            Description = "Netherlands plays key role in major EU policy agreement",
                            OpinionImpact = new float3(0.1f, 0.2f, 0.1f)
                        },
                        new EventVariant
                        {
                            EventName = "International Trade Dispute",
                            Description = "Trade tensions affect Dutch export-dependent economy",
                            OpinionImpact = new float3(-0.3f, -0.1f, 0.0f)
                        },
                        new EventVariant
                        {
                            EventName = "NATO Defense Spending Debate",
                            Description = "Pressure to increase military spending sparks political debate",
                            OpinionImpact = new float3(-0.2f, -0.1f, 0.0f)
                        }
                    }
                }
            };
        }
    }

    [Serializable]
    public class EventTemplate
    {
        public PoliticalEventType EventType;
        public float BaseFrequency;        // Likelihood of this event type
        public float BaseStrength;         // Base impact strength
        public float BaseDuration;         // Base duration in seconds
        public float ImpactVariation;      // How much impact can vary (0.0-1.0)
        public DutchPoliticalParty[] RelevantParties;
        public string[] RelatedIssues;
        public string[] AffectedDemographics;
        public List<EventVariant> EventVariants;
    }

    [Serializable]
    public class EventVariant
    {
        public string EventName;
        public string Description;
        public float3 OpinionImpact;      // Base impact on Economic, Social, Environmental
    }
}