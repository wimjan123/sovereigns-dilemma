using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using SovereignsDilemma.Political.Components;

namespace SovereignsDilemma.Political.Systems
{
    /// <summary>
    /// Comprehensive Dutch political context system for realistic party dynamics and voting behavior.
    /// Models the actual Dutch political landscape with accurate party positions and electoral mechanics.
    /// </summary>
    [Serializable]
    public class DutchPoliticalContext
    {
        private readonly Dictionary<DutchPoliticalParty, PartyProfile> _partyProfiles;
        private readonly List<PoliticalIssue> _currentIssues;

        public DutchPoliticalContext()
        {
            _partyProfiles = InitializeDutchParties();
            _currentIssues = InitializeCurrentIssues();

            Debug.Log($"Dutch Political Context initialized with {_partyProfiles.Count} parties and {_currentIssues.Count} issues");
        }

        private Dictionary<DutchPoliticalParty, PartyProfile> InitializeDutchParties()
        {
            return new Dictionary<DutchPoliticalParty, PartyProfile>
            {
                [DutchPoliticalParty.VVD] = new PartyProfile
                {
                    PartyName = "VVD (People's Party for Freedom and Democracy)",
                    EconomicPosition = 0.6f,      // Right-wing, liberal economics
                    SocialPosition = 0.2f,        // Moderately progressive on social issues
                    EnvironmentalPosition = -0.1f, // Moderate on environment
                    CoreVoterBase = new[] { IncomeLevel.Middle, IncomeLevel.High },
                    EducationAppeal = new[] { EducationLevel.Higher, EducationLevel.Vocational },
                    KeyIssues = new[] { "Economic Growth", "Entrepreneurship", "Individual Freedom" },
                    MarketShare = 0.22f, // Approximate vote share
                    Coalition = CoalitionType.Center
                },

                [DutchPoliticalParty.PVV] = new PartyProfile
                {
                    PartyName = "PVV (Party for Freedom)",
                    EconomicPosition = 0.1f,       // Mixed economic positions
                    SocialPosition = -0.7f,        // Conservative on social issues
                    EnvironmentalPosition = -0.4f, // Skeptical of environmental policies
                    CoreVoterBase = new[] { IncomeLevel.Low, IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Primary, EducationLevel.Secondary },
                    KeyIssues = new[] { "Immigration Control", "National Sovereignty", "Direct Democracy" },
                    MarketShare = 0.17f,
                    Coalition = CoalitionType.Opposition
                },

                [DutchPoliticalParty.CDA] = new PartyProfile
                {
                    PartyName = "CDA (Christian Democratic Appeal)",
                    EconomicPosition = 0.3f,       // Center-right economics
                    SocialPosition = -0.3f,        // Traditional on social issues
                    EnvironmentalPosition = 0.2f,  // Moderate environmental support
                    CoreVoterBase = new[] { IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Secondary, EducationLevel.Vocational },
                    KeyIssues = new[] { "Christian Values", "Community", "Balanced Policies" },
                    MarketShare = 0.09f,
                    Coalition = CoalitionType.Center
                },

                [DutchPoliticalParty.D66] = new PartyProfile
                {
                    PartyName = "D66 (Democrats 66)",
                    EconomicPosition = 0.2f,       // Center economics with liberal elements
                    SocialPosition = 0.6f,         // Progressive on social issues
                    EnvironmentalPosition = 0.5f,  // Strong environmental support
                    CoreVoterBase = new[] { IncomeLevel.Middle, IncomeLevel.High },
                    EducationAppeal = new[] { EducationLevel.Higher },
                    KeyIssues = new[] { "Education", "Climate Action", "European Integration" },
                    MarketShare = 0.15f,
                    Coalition = CoalitionType.Center
                },

                [DutchPoliticalParty.GL] = new PartyProfile
                {
                    PartyName = "GL (GreenLeft)",
                    EconomicPosition = -0.4f,      // Left-wing economics
                    SocialPosition = 0.8f,         // Very progressive on social issues
                    EnvironmentalPosition = 0.9f,  // Very strong environmental focus
                    CoreVoterBase = new[] { IncomeLevel.Low, IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Higher },
                    KeyIssues = new[] { "Climate Emergency", "Social Justice", "Sustainability" },
                    MarketShare = 0.06f,
                    Coalition = CoalitionType.Left
                },

                [DutchPoliticalParty.PvdA] = new PartyProfile
                {
                    PartyName = "PvdA (Labour Party)",
                    EconomicPosition = -0.5f,      // Left-wing economics
                    SocialPosition = 0.4f,         // Progressive on social issues
                    EnvironmentalPosition = 0.4f,  // Environmental support
                    CoreVoterBase = new[] { IncomeLevel.Low, IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Secondary, EducationLevel.Higher },
                    KeyIssues = new[] { "Workers' Rights", "Social Security", "Equal Opportunity" },
                    MarketShare = 0.06f,
                    Coalition = CoalitionType.Left
                },

                [DutchPoliticalParty.SP] = new PartyProfile
                {
                    PartyName = "SP (Socialist Party)",
                    EconomicPosition = -0.7f,      // Very left-wing economics
                    SocialPosition = 0.3f,         // Progressive but populist
                    EnvironmentalPosition = 0.3f,  // Environmental support
                    CoreVoterBase = new[] { IncomeLevel.Low },
                    EducationAppeal = new[] { EducationLevel.Secondary, EducationLevel.Vocational },
                    KeyIssues = new[] { "Anti-Establishment", "Healthcare", "Public Services" },
                    MarketShare = 0.06f,
                    Coalition = CoalitionType.Left
                },

                [DutchPoliticalParty.ChristenUnie] = new PartyProfile
                {
                    PartyName = "ChristenUnie (ChristianUnion)",
                    EconomicPosition = -0.1f,      // Center economics
                    SocialPosition = -0.5f,        // Conservative on social issues
                    EnvironmentalPosition = 0.4f,  // Strong environmental support
                    CoreVoterBase = new[] { IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Secondary, EducationLevel.Higher },
                    KeyIssues = new[] { "Christian Values", "Stewardship", "Social Care" },
                    MarketShare = 0.03f,
                    Coalition = CoalitionType.Center
                },

                [DutchPoliticalParty.FvD] = new PartyProfile
                {
                    PartyName = "FvD (Forum for Democracy)",
                    EconomicPosition = 0.4f,       // Right-wing economics
                    SocialPosition = -0.6f,        // Conservative on social issues
                    EnvironmentalPosition = -0.7f, // Climate skeptical
                    CoreVoterBase = new[] { IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Higher, EducationLevel.Secondary },
                    KeyIssues = new[] { "National Identity", "Direct Democracy", "Anti-EU" },
                    MarketShare = 0.05f,
                    Coalition = CoalitionType.Opposition
                },

                [DutchPoliticalParty.PvdD] = new PartyProfile
                {
                    PartyName = "PvdD (Party for the Animals)",
                    EconomicPosition = -0.2f,      // Left-leaning economics
                    SocialPosition = 0.5f,         // Progressive on social issues
                    EnvironmentalPosition = 1.0f,  // Maximum environmental focus
                    CoreVoterBase = new[] { IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Higher },
                    KeyIssues = new[] { "Animal Rights", "Sustainability", "Planet Over Profit" },
                    MarketShare = 0.04f,
                    Coalition = CoalitionType.Left
                },

                [DutchPoliticalParty.Volt] = new PartyProfile
                {
                    PartyName = "Volt Netherlands",
                    EconomicPosition = 0.1f,       // Center economics
                    SocialPosition = 0.7f,         // Very progressive on social issues
                    EnvironmentalPosition = 0.8f,  // Strong environmental focus
                    CoreVoterBase = new[] { IncomeLevel.Middle, IncomeLevel.High },
                    EducationAppeal = new[] { EducationLevel.Higher },
                    KeyIssues = new[] { "European Integration", "Innovation", "Climate Action" },
                    MarketShare = 0.02f,
                    Coalition = CoalitionType.Center
                },

                [DutchPoliticalParty.BBB] = new PartyProfile
                {
                    PartyName = "BBB (BoerBurgerBeweging)",
                    EconomicPosition = 0.2f,       // Center-right economics
                    SocialPosition = -0.2f,        // Moderately conservative
                    EnvironmentalPosition = -0.5f, // Against strict environmental regulations
                    CoreVoterBase = new[] { IncomeLevel.Middle },
                    EducationAppeal = new[] { EducationLevel.Secondary, EducationLevel.Vocational },
                    KeyIssues = new[] { "Rural Interests", "Farming Rights", "Regional Development" },
                    MarketShare = 0.01f,
                    Coalition = CoalitionType.Opposition
                }
            };
        }

        private List<PoliticalIssue> InitializeCurrentIssues()
        {
            return new List<PoliticalIssue>
            {
                new PoliticalIssue
                {
                    IssueName = "Climate Policy",
                    Importance = 0.8f,
                    EconomicImpact = -0.3f,
                    SocialImpact = 0.2f,
                    EnvironmentalImpact = 0.9f,
                    AffectedDemographics = new[] { "Young Adults", "Urban Residents", "Highly Educated" }
                },
                new PoliticalIssue
                {
                    IssueName = "Immigration and Integration",
                    Importance = 0.7f,
                    EconomicImpact = 0.1f,
                    SocialImpact = -0.4f,
                    EnvironmentalImpact = 0.0f,
                    AffectedDemographics = new[] { "Lower Income", "Rural Residents", "Older Adults" }
                },
                new PoliticalIssue
                {
                    IssueName = "Housing Crisis",
                    Importance = 0.9f,
                    EconomicImpact = -0.6f,
                    SocialImpact = 0.3f,
                    EnvironmentalImpact = -0.2f,
                    AffectedDemographics = new[] { "Young Adults", "Middle Income", "Urban Residents" }
                },
                new PoliticalIssue
                {
                    IssueName = "Healthcare System",
                    Importance = 0.8f,
                    EconomicImpact = -0.2f,
                    SocialImpact = 0.5f,
                    EnvironmentalImpact = 0.0f,
                    AffectedDemographics = new[] { "Older Adults", "Lower Income", "Chronically Ill" }
                },
                new PoliticalIssue
                {
                    IssueName = "Economic Recovery",
                    Importance = 0.7f,
                    EconomicImpact = 0.6f,
                    SocialImpact = 0.1f,
                    EnvironmentalImpact = -0.1f,
                    AffectedDemographics = new[] { "Business Owners", "Working Age", "Urban Residents" }
                },
                new PoliticalIssue
                {
                    IssueName = "Agricultural Regulations",
                    Importance = 0.6f,
                    EconomicImpact = -0.4f,
                    SocialImpact = -0.2f,
                    EnvironmentalImpact = 0.5f,
                    AffectedDemographics = new[] { "Rural Residents", "Farmers", "Traditional Communities" }
                }
            };
        }

        public DutchPoliticalParty CalculateVotingIntention(PoliticalOpinion opinion)
        {
            float bestMatch = float.MinValue;
            DutchPoliticalParty bestParty = DutchPoliticalParty.None;

            foreach (var kvp in _partyProfiles)
            {
                var party = kvp.Key;
                var profile = kvp.Value;

                // Calculate ideological distance
                float economicDistance = math.abs(opinion.EconomicPosition - profile.EconomicPosition);
                float socialDistance = math.abs(opinion.SocialPosition - profile.SocialPosition);
                float environmentalDistance = math.abs(opinion.EnvironmentalPosition - profile.EnvironmentalPosition);

                // Weight the distances (economic and social typically more important)
                float totalDistance = (economicDistance * 0.4f) + (socialDistance * 0.4f) + (environmentalDistance * 0.2f);

                // Convert distance to similarity score
                float similarity = 1.0f - (totalDistance / 2.0f); // Normalize to 0-1

                // Apply party market share as a baseline probability
                float adjustedScore = similarity * (0.7f + profile.MarketShare * 0.3f);

                if (adjustedScore > bestMatch)
                {
                    bestMatch = adjustedScore;
                    bestParty = party;
                }
            }

            return bestParty;
        }

        public PartyProfile GetPartyProfile(DutchPoliticalParty party)
        {
            return _partyProfiles.TryGetValue(party, out var profile) ? profile : null;
        }

        public List<DutchPoliticalParty> GetCoalitionPartners(DutchPoliticalParty party)
        {
            if (!_partyProfiles.TryGetValue(party, out var targetProfile))
                return new List<DutchPoliticalParty>();

            return _partyProfiles
                .Where(kvp => kvp.Key != party && kvp.Value.Coalition == targetProfile.Coalition)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        public List<PoliticalIssue> GetCurrentIssues()
        {
            return new List<PoliticalIssue>(_currentIssues);
        }

        public PoliticalIssue GetMostImportantIssue()
        {
            return _currentIssues.OrderByDescending(i => i.Importance).FirstOrDefault();
        }

        public float CalculatePartySupport(DutchPoliticalParty party, List<PoliticalOpinion> voterOpinions)
        {
            if (voterOpinions.Count == 0) return 0f;

            int supporters = voterOpinions.Count(opinion => CalculateVotingIntention(opinion) == party);
            return (float)supporters / voterOpinions.Count;
        }

        public void UpdateIssueImportance(string issueName, float newImportance)
        {
            var issue = _currentIssues.FirstOrDefault(i => i.IssueName == issueName);
            if (issue != null)
            {
                issue.Importance = math.clamp(newImportance, 0f, 1f);
            }
        }
    }

    [Serializable]
    public class PartyProfile
    {
        public string PartyName;
        public float EconomicPosition;    // -1 (left) to +1 (right)
        public float SocialPosition;      // -1 (conservative) to +1 (progressive)
        public float EnvironmentalPosition; // -1 (skeptical) to +1 (activist)
        public IncomeLevel[] CoreVoterBase;
        public EducationLevel[] EducationAppeal;
        public string[] KeyIssues;
        public float MarketShare;         // Typical vote percentage
        public CoalitionType Coalition;
    }

    [Serializable]
    public class PoliticalIssue
    {
        public string IssueName;
        public float Importance;          // 0-1 scale of current importance
        public float EconomicImpact;      // -1 to +1 impact on economic policy
        public float SocialImpact;        // -1 to +1 impact on social policy
        public float EnvironmentalImpact; // -1 to +1 impact on environmental policy
        public string[] AffectedDemographics;
    }

    public enum CoalitionType
    {
        Left,        // Socialist, Green, Progressive parties
        Center,      // Liberal, Christian, Centrist parties
        Opposition   // Populist, Nationalist, Anti-establishment parties
    }
}