#!/bin/bash
# Beta Tester Recruitment Automation Script for The Sovereign's Dilemma
# Manages recruitment campaigns, application processing, and tester onboarding

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BETA_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_ROOT="$(cd "$BETA_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date '+%Y-%m-%d %H:%M:%S')] $1${NC}"
}

info() {
    echo -e "${BLUE}[$(date '+%Y-%m-%d %H:%M:%S')] INFO: $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date '+%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date '+%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
    exit 1
}

# Configuration
BETA_DATABASE="$BETA_DIR/data/beta-testers.json"
APPLICATIONS_DIR="$BETA_DIR/data/applications"
REPORTS_DIR="$BETA_DIR/reports"
TEMPLATES_DIR="$BETA_DIR/templates"

# Recruitment targets
TARGET_TESTERS=50
WEEKLY_RECRUITMENT_GOAL=15
MIN_POLITICAL_INTEREST=6  # Out of 10
MIN_GAMING_EXPERIENCE=4   # Out of 10

# Create necessary directories
mkdir -p "$(dirname "$BETA_DATABASE")" "$APPLICATIONS_DIR" "$REPORTS_DIR" "$TEMPLATES_DIR"

# Initialize beta tester database if it doesn't exist
init_database() {
    if [[ ! -f "$BETA_DATABASE" ]]; then
        log "Initializing beta tester database..."
        cat > "$BETA_DATABASE" << EOF
{
  "program_info": {
    "start_date": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "target_testers": $TARGET_TESTERS,
    "current_phase": "recruitment",
    "last_updated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  },
  "recruitment_stats": {
    "applications_received": 0,
    "applications_approved": 0,
    "applications_rejected": 0,
    "applications_pending": 0,
    "target_demographics_met": {
      "political_enthusiasts": 0,
      "strategy_gamers": 0,
      "dutch_expatriates": 0,
      "political_students": 0,
      "political_experts": 0,
      "game_industry": 0
    }
  },
  "testers": [],
  "waitlist": []
}
EOF
    fi
}

create_application_form() {
    log "Creating beta application form template..."

    cat > "$TEMPLATES_DIR/application-form.html" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>The Sovereign's Dilemma - Beta Testing Application</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f5f5f5;
            color: #333;
        }
        .container {
            background: white;
            padding: 40px;
            border-radius: 8px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        h1 {
            color: #2c3e50;
            text-align: center;
            margin-bottom: 10px;
        }
        .subtitle {
            text-align: center;
            color: #7f8c8d;
            margin-bottom: 30px;
            font-style: italic;
        }
        .section {
            margin: 30px 0;
            padding: 20px;
            border-left: 4px solid #3498db;
            background-color: #f8f9fa;
        }
        .section h3 {
            color: #2c3e50;
            margin-top: 0;
        }
        .form-group {
            margin: 15px 0;
        }
        label {
            display: block;
            margin-bottom: 5px;
            font-weight: 600;
            color: #34495e;
        }
        input, select, textarea {
            width: 100%;
            padding: 10px;
            border: 1px solid #bdc3c7;
            border-radius: 4px;
            font-size: 14px;
        }
        textarea {
            height: 100px;
            resize: vertical;
        }
        .rating-scale {
            display: flex;
            gap: 10px;
            align-items: center;
        }
        .rating-scale input {
            width: auto;
        }
        .checkbox-group {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 10px;
            margin-top: 10px;
        }
        .checkbox-group label {
            display: flex;
            align-items: center;
            font-weight: normal;
        }
        .checkbox-group input {
            width: auto;
            margin-right: 8px;
        }
        .submit-btn {
            background-color: #3498db;
            color: white;
            padding: 15px 30px;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            cursor: pointer;
            width: 100%;
            margin-top: 20px;
        }
        .submit-btn:hover {
            background-color: #2980b9;
        }
        .privacy-notice {
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            padding: 15px;
            border-radius: 4px;
            margin: 20px 0;
            font-size: 14px;
        }
        .required {
            color: #e74c3c;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>The Sovereign's Dilemma</h1>
        <p class="subtitle">Beta Testing Program Application</p>

        <p>Join us in testing an innovative Dutch political simulation game featuring 10,000 AI-driven voters. Help us create an authentic and engaging political experience that educates and entertains.</p>

        <form id="betaApplication" action="/submit-application" method="POST">

            <div class="section">
                <h3>Personal Information</h3>

                <div class="form-group">
                    <label for="fullName">Full Name <span class="required">*</span></label>
                    <input type="text" id="fullName" name="fullName" required>
                </div>

                <div class="form-group">
                    <label for="email">Email Address <span class="required">*</span></label>
                    <input type="email" id="email" name="email" required>
                </div>

                <div class="form-group">
                    <label for="ageRange">Age Range <span class="required">*</span></label>
                    <select id="ageRange" name="ageRange" required>
                        <option value="">Select age range</option>
                        <option value="18-24">18-24</option>
                        <option value="25-34">25-34</option>
                        <option value="35-44">35-44</option>
                        <option value="45-54">45-54</option>
                        <option value="55-64">55-64</option>
                        <option value="65+">65+</option>
                    </select>
                </div>

                <div class="form-group">
                    <label for="location">Location (City, Country) <span class="required">*</span></label>
                    <input type="text" id="location" name="location" placeholder="e.g., Amsterdam, Netherlands" required>
                </div>

                <div class="form-group">
                    <label for="primaryLanguage">Primary Language <span class="required">*</span></label>
                    <select id="primaryLanguage" name="primaryLanguage" required>
                        <option value="">Select primary language</option>
                        <option value="Dutch">Dutch</option>
                        <option value="English">English</option>
                        <option value="German">German</option>
                        <option value="French">French</option>
                        <option value="Other">Other</option>
                    </select>
                </div>
            </div>

            <div class="section">
                <h3>Political Background</h3>

                <div class="form-group">
                    <label for="politicalInterest">Interest in Dutch Politics (1-10 scale) <span class="required">*</span></label>
                    <div class="rating-scale">
                        <span>Not interested</span>
                        <input type="range" id="politicalInterest" name="politicalInterest" min="1" max="10" value="5" required>
                        <span>Very interested</span>
                        <span id="politicalInterestValue">5</span>
                    </div>
                </div>

                <div class="form-group">
                    <label for="votingFrequency">How often do you vote in Dutch elections?</label>
                    <select id="votingFrequency" name="votingFrequency">
                        <option value="">Select frequency</option>
                        <option value="always">Always vote</option>
                        <option value="usually">Usually vote</option>
                        <option value="sometimes">Sometimes vote</option>
                        <option value="rarely">Rarely vote</option>
                        <option value="never">Never vote</option>
                        <option value="not_eligible">Not eligible to vote in Netherlands</option>
                    </select>
                </div>

                <div class="form-group">
                    <label for="politicalKnowledge">Knowledge of Dutch Political System (1-10 scale)</label>
                    <div class="rating-scale">
                        <span>No knowledge</span>
                        <input type="range" id="politicalKnowledge" name="politicalKnowledge" min="1" max="10" value="5">
                        <span>Expert level</span>
                        <span id="politicalKnowledgeValue">5</span>
                    </div>
                </div>

                <div class="form-group">
                    <label for="politicalParty">Political Party Preference (Optional)</label>
                    <select id="politicalParty" name="politicalParty">
                        <option value="">Prefer not to say</option>
                        <option value="VVD">VVD (People's Party for Freedom and Democracy)</option>
                        <option value="PVV">PVV (Party for Freedom)</option>
                        <option value="CDA">CDA (Christian Democratic Appeal)</option>
                        <option value="D66">D66 (Democrats 66)</option>
                        <option value="GL">GL (GreenLeft)</option>
                        <option value="SP">SP (Socialist Party)</option>
                        <option value="PvdA">PvdA (Labour Party)</option>
                        <option value="CU">CU (Christian Union)</option>
                        <option value="PvdD">PvdD (Party for the Animals)</option>
                        <option value="50PLUS">50PLUS</option>
                        <option value="SGP">SGP (Reformed Political Party)</option>
                        <option value="DENK">DENK</option>
                        <option value="FvD">FvD (Forum for Democracy)</option>
                        <option value="Other">Other</option>
                    </select>
                </div>
            </div>

            <div class="section">
                <h3>Gaming Experience</h3>

                <div class="form-group">
                    <label>Gaming Platform Preferences (Select all that apply)</label>
                    <div class="checkbox-group">
                        <label><input type="checkbox" name="gamingPlatforms" value="PC"> PC (Windows)</label>
                        <label><input type="checkbox" name="gamingPlatforms" value="Mac"> Mac</label>
                        <label><input type="checkbox" name="gamingPlatforms" value="Linux"> Linux</label>
                        <label><input type="checkbox" name="gamingPlatforms" value="Steam"> Steam</label>
                        <label><input type="checkbox" name="gamingPlatforms" value="itch.io"> itch.io</label>
                        <label><input type="checkbox" name="gamingPlatforms" value="Console"> Console Gaming</label>
                    </div>
                </div>

                <div class="form-group">
                    <label for="strategyGameExperience">Strategy Game Experience (1-10 scale) <span class="required">*</span></label>
                    <div class="rating-scale">
                        <span>No experience</span>
                        <input type="range" id="strategyGameExperience" name="strategyGameExperience" min="1" max="10" value="5" required>
                        <span>Expert level</span>
                        <span id="strategyGameExperienceValue">5</span>
                    </div>
                </div>

                <div class="form-group">
                    <label>Favorite Strategy Games (Select all that apply)</label>
                    <div class="checkbox-group">
                        <label><input type="checkbox" name="favoriteGames" value="Civilization"> Civilization Series</label>
                        <label><input type="checkbox" name="favoriteGames" value="EU4"> Europa Universalis IV</label>
                        <label><input type="checkbox" name="favoriteGames" value="CK3"> Crusader Kings III</label>
                        <label><input type="checkbox" name="favoriteGames" value="HOI4"> Hearts of Iron IV</label>
                        <label><input type="checkbox" name="favoriteGames" value="AoE"> Age of Empires</label>
                        <label><input type="checkbox" name="favoriteGames" value="Total War"> Total War Series</label>
                        <label><input type="checkbox" name="favoriteGames" value="SimCity"> SimCity/Cities Skylines</label>
                        <label><input type="checkbox" name="favoriteGames" value="Political"> Political Simulation Games</label>
                    </div>
                </div>

                <div class="form-group">
                    <label for="timeCommitment">Time Available for Beta Testing (hours per week)</label>
                    <select id="timeCommitment" name="timeCommitment">
                        <option value="">Select time commitment</option>
                        <option value="1-2">1-2 hours</option>
                        <option value="3-5">3-5 hours</option>
                        <option value="6-10">6-10 hours</option>
                        <option value="10+">10+ hours</option>
                    </select>
                </div>

                <div class="form-group">
                    <label for="betaExperience">Previous Beta Testing Experience</label>
                    <select id="betaExperience" name="betaExperience">
                        <option value="">Select experience level</option>
                        <option value="none">No previous experience</option>
                        <option value="some">Some beta testing experience</option>
                        <option value="extensive">Extensive beta testing experience</option>
                        <option value="professional">Professional QA/Testing background</option>
                    </select>
                </div>
            </div>

            <div class="section">
                <h3>Technical Setup</h3>

                <div class="form-group">
                    <label for="operatingSystem">Operating System <span class="required">*</span></label>
                    <select id="operatingSystem" name="operatingSystem" required>
                        <option value="">Select OS</option>
                        <option value="Windows 11">Windows 11</option>
                        <option value="Windows 10">Windows 10</option>
                        <option value="macOS">macOS</option>
                        <option value="Linux">Linux</option>
                        <option value="Other">Other</option>
                    </select>
                </div>

                <div class="form-group">
                    <label for="hardware">Hardware Specifications</label>
                    <textarea id="hardware" name="hardware" placeholder="Please describe your computer specifications (CPU, RAM, Graphics Card, etc.)"></textarea>
                </div>

                <div class="form-group">
                    <label for="internetConnection">Internet Connection Type</label>
                    <select id="internetConnection" name="internetConnection">
                        <option value="">Select connection type</option>
                        <option value="fiber">Fiber (100+ Mbps)</option>
                        <option value="broadband">Broadband (25-100 Mbps)</option>
                        <option value="dsl">DSL (5-25 Mbps)</option>
                        <option value="mobile">Mobile/4G</option>
                        <option value="satellite">Satellite</option>
                        <option value="other">Other</option>
                    </select>
                </div>
            </div>

            <div class="section">
                <h3>Motivation and Expectations</h3>

                <div class="form-group">
                    <label for="motivation">Why do you want to participate in beta testing?</label>
                    <textarea id="motivation" name="motivation" placeholder="Tell us what interests you about testing this political simulation game..."></textarea>
                </div>

                <div class="form-group">
                    <label for="expectations">What do you hope to learn from this political simulation?</label>
                    <textarea id="expectations" name="expectations" placeholder="Describe your learning goals and what you hope to get out of the experience..."></textarea>
                </div>

                <div class="form-group">
                    <label for="feedbackStyle">How do you prefer to provide feedback?</label>
                    <div class="checkbox-group">
                        <label><input type="checkbox" name="feedbackStyle" value="written"> Written reports</label>
                        <label><input type="checkbox" name="feedbackStyle" value="video"> Video recordings</label>
                        <label><input type="checkbox" name="feedbackStyle" value="voice"> Voice calls</label>
                        <label><input type="checkbox" name="feedbackStyle" value="chat"> Live chat/Discord</label>
                        <label><input type="checkbox" name="feedbackStyle" value="surveys"> Structured surveys</label>
                    </div>
                </div>
            </div>

            <div class="privacy-notice">
                <h4>Privacy Notice</h4>
                <p><strong>Data Collection:</strong> We collect this information to select appropriate beta testers and improve our game. Your data will be used solely for beta program management and will be deleted after the program concludes.</p>
                <p><strong>GDPR Compliance:</strong> If you're an EU resident, you have the right to access, correct, or delete your data at any time. Contact us at privacy@sovereignsdilemma.com</p>
                <p><strong>Consent:</strong> By submitting this application, you consent to participate in beta testing and provide feedback about the game.</p>
            </div>

            <div class="form-group">
                <label>
                    <input type="checkbox" name="consent" value="yes" required>
                    I agree to the privacy policy and consent to participate in beta testing <span class="required">*</span>
                </label>
            </div>

            <button type="submit" class="submit-btn">Submit Beta Application</button>
        </form>
    </div>

    <script>
        // Update range sliders display
        document.getElementById('politicalInterest').addEventListener('input', function() {
            document.getElementById('politicalInterestValue').textContent = this.value;
        });

        document.getElementById('politicalKnowledge').addEventListener('input', function() {
            document.getElementById('politicalKnowledgeValue').textContent = this.value;
        });

        document.getElementById('strategyGameExperience').addEventListener('input', function() {
            document.getElementById('strategyGameExperienceValue').textContent = this.value;
        });

        // Form submission handling
        document.getElementById('betaApplication').addEventListener('submit', function(e) {
            e.preventDefault();

            // Basic validation
            const requiredFields = ['fullName', 'email', 'ageRange', 'location', 'primaryLanguage', 'politicalInterest', 'strategyGameExperience', 'operatingSystem'];
            let isValid = true;

            requiredFields.forEach(field => {
                const input = document.getElementById(field);
                if (!input.value) {
                    input.style.borderColor = '#e74c3c';
                    isValid = false;
                } else {
                    input.style.borderColor = '#bdc3c7';
                }
            });

            if (!document.querySelector('input[name="consent"]:checked')) {
                alert('Please agree to the privacy policy and consent to participate.');
                return;
            }

            if (isValid) {
                // Collect form data
                const formData = new FormData(this);

                // Show success message
                alert('Thank you for your application! We will review it and contact you within 1 week.');

                // In a real implementation, this would submit to a server
                console.log('Form data:', Object.fromEntries(formData));
            } else {
                alert('Please fill in all required fields.');
            }
        });
    </script>
</body>
</html>
EOF

    log "Beta application form created at $TEMPLATES_DIR/application-form.html"
}

process_application() {
    local application_file="$1"

    if [[ ! -f "$application_file" ]]; then
        error "Application file not found: $application_file"
    fi

    local application_data=$(cat "$application_file")
    local applicant_email=$(echo "$application_data" | jq -r '.email // empty')
    local political_interest=$(echo "$application_data" | jq -r '.politicalInterest // 0' | sed 's/[^0-9]//g')
    local gaming_experience=$(echo "$application_data" | jq -r '.strategyGameExperience // 0' | sed 's/[^0-9]//g')

    # Scoring system
    local score=0
    local demographic_category=""

    # Political interest scoring (25 points max)
    score=$((score + political_interest * 25 / 10))

    # Gaming experience scoring (20 points max)
    score=$((score + gaming_experience * 20 / 10))

    # Demographic categorization and bonus scoring
    local age_range=$(echo "$application_data" | jq -r '.ageRange // empty')
    local location=$(echo "$application_data" | jq -r '.location // empty')
    local primary_language=$(echo "$application_data" | jq -r '.primaryLanguage // empty')

    # Category determination and demographic scoring (20 points max)
    if [[ "$primary_language" == "Dutch" && "$location" =~ Netherlands ]]; then
        if [[ "$age_range" =~ ^(25-34|35-44|45-54)$ ]]; then
            demographic_category="political_enthusiasts"
            score=$((score + 20))
        elif [[ "$age_range" =~ ^(18-24|25-34)$ ]]; then
            demographic_category="political_students"
            score=$((score + 15))
        fi
    elif [[ "$primary_language" == "Dutch" ]]; then
        demographic_category="dutch_expatriates"
        score=$((score + 15))
    else
        demographic_category="strategy_gamers"
        score=$((score + 10))
    fi

    # Time commitment scoring (15 points max)
    local time_commitment=$(echo "$application_data" | jq -r '.timeCommitment // empty')
    case "$time_commitment" in
        "10+") score=$((score + 15)) ;;
        "6-10") score=$((score + 12)) ;;
        "3-5") score=$((score + 8)) ;;
        "1-2") score=$((score + 4)) ;;
    esac

    # Beta experience scoring (10 points max)
    local beta_experience=$(echo "$application_data" | jq -r '.betaExperience // empty')
    case "$beta_experience" in
        "professional") score=$((score + 10)) ;;
        "extensive") score=$((score + 8)) ;;
        "some") score=$((score + 5)) ;;
        "none") score=$((score + 2)) ;;
    esac

    # Technical setup scoring (10 points max)
    local os=$(echo "$application_data" | jq -r '.operatingSystem // empty')
    local connection=$(echo "$application_data" | jq -r '.internetConnection // empty')

    [[ "$os" =~ Windows ]] && score=$((score + 5))
    [[ "$connection" =~ fiber|broadband ]] && score=$((score + 5))

    # Decision logic
    local decision="pending"
    local reason=""

    if [[ $political_interest -lt $MIN_POLITICAL_INTEREST ]]; then
        decision="rejected"
        reason="Insufficient political interest (${political_interest}/10, minimum ${MIN_POLITICAL_INTEREST})"
    elif [[ $gaming_experience -lt $MIN_GAMING_EXPERIENCE ]]; then
        decision="rejected"
        reason="Insufficient gaming experience (${gaming_experience}/10, minimum ${MIN_GAMING_EXPERIENCE})"
    elif [[ $score -ge 75 ]]; then
        decision="approved"
        reason="High score ($score/100) with strong demographic fit"
    elif [[ $score -ge 60 ]]; then
        decision="waitlist"
        reason="Good score ($score/100) - eligible for waitlist"
    else
        decision="rejected"
        reason="Low overall score ($score/100)"
    fi

    # Update application with decision
    echo "$application_data" | jq --arg decision "$decision" --arg reason "$reason" --argjson score "$score" --arg category "$demographic_category" \
        '. + {
            "processing": {
                "decision": $decision,
                "reason": $reason,
                "score": $score,
                "demographic_category": $category,
                "processed_date": now | strftime("%Y-%m-%dT%H:%M:%SZ")
            }
        }' > "${application_file}.processed"

    info "Processed application for $applicant_email: $decision (score: $score/100)"
    echo "$decision"
}

update_recruitment_stats() {
    local decision="$1"
    local demographic_category="$2"

    # Update database statistics
    local temp_file=$(mktemp)

    jq --arg decision "$decision" --arg category "$demographic_category" '
        .recruitment_stats.applications_received += 1 |
        if $decision == "approved" then
            .recruitment_stats.applications_approved += 1
        elif $decision == "rejected" then
            .recruitment_stats.applications_rejected += 1
        else
            .recruitment_stats.applications_pending += 1
        end |
        if $category != "" then
            .recruitment_stats.target_demographics_met[$category] += 1
        else
            .
        end |
        .program_info.last_updated = now | strftime("%Y-%m-%dT%H:%M:%SZ")
    ' "$BETA_DATABASE" > "$temp_file" && mv "$temp_file" "$BETA_DATABASE"
}

generate_recruitment_report() {
    log "Generating recruitment progress report..."

    local report_file="$REPORTS_DIR/recruitment-report-$(date '+%Y%m%d').md"
    local stats=$(cat "$BETA_DATABASE")

    cat > "$report_file" << EOF
# Beta Tester Recruitment Report

**Date**: $(date '+%Y-%m-%d')
**Program Phase**: $(echo "$stats" | jq -r '.program_info.current_phase')

## Application Statistics

$(echo "$stats" | jq -r '
"- **Total Applications**: \(.recruitment_stats.applications_received)
- **Approved**: \(.recruitment_stats.applications_approved)
- **Pending Review**: \(.recruitment_stats.applications_pending)
- **Rejected**: \(.recruitment_stats.applications_rejected)
- **Approval Rate**: \((.recruitment_stats.applications_approved / (.recruitment_stats.applications_received | if . == 0 then 1 else . end) * 100) | floor)%"
')

## Target Demographics Progress

$(echo "$stats" | jq -r '
.recruitment_stats.target_demographics_met | to_entries[] |
"- **\(.key | gsub("_"; " ") | ascii_upcase)**: \(.value) testers"
')

## Progress Toward Goals

- **Target Total Testers**: $TARGET_TESTERS
- **Current Approved**: $(echo "$stats" | jq -r '.recruitment_stats.applications_approved')
- **Progress**: $(echo "$stats" | jq -r '(.recruitment_stats.applications_approved / '"$TARGET_TESTERS"' * 100) | floor')%

## Next Steps

$(if [[ $(echo "$stats" | jq -r '.recruitment_stats.applications_approved') -lt $TARGET_TESTERS ]]; then
    echo "- Continue recruitment campaigns
- Focus on underrepresented demographics
- Accelerate application processing"
else
    echo "- Begin beta testing phase
- Onboard approved testers
- Prepare testing infrastructure"
fi)

---
*Generated automatically by recruitment automation system*
EOF

    log "Recruitment report saved: $report_file"
    cat "$report_file"
}

send_acceptance_email() {
    local applicant_email="$1"
    local applicant_name="$2"

    # Create acceptance email template
    cat > "$TEMPLATES_DIR/acceptance-email.txt" << EOF
Subject: Welcome to The Sovereign's Dilemma Beta Testing Program!

Dear $applicant_name,

Congratulations! You have been selected to participate in the beta testing program for "The Sovereign's Dilemma" - our innovative Dutch political simulation game.

## What's Next?

1. **Download Instructions**: You will receive Steam beta access within 24 hours
2. **Discord Community**: Join our beta tester Discord server at [DISCORD_LINK]
3. **Testing Guidelines**: Review the beta testing guidelines attached
4. **First Session**: Plan for a 1-hour guided session this week

## Beta Access Details

- **Platform**: Steam (Beta Branch)
- **Alternative**: itch.io beta channel
- **Testing Period**: 6 weeks
- **Time Commitment**: 3-5 hours per week recommended

## Important Information

- **Privacy**: All game content is confidential until public release
- **Feedback**: We encourage detailed feedback through our Discord channels
- **Support**: Direct access to the development team for questions and issues

## Getting Started

1. Join Discord: [DISCORD_LINK]
2. Complete system check: Verify your hardware meets requirements
3. Download beta build: Instructions will be provided via Steam/itch.io
4. Attend orientation: Optional live session this Friday at 7 PM CET

We're excited to have you as part of our beta testing community! Your feedback will directly shape the final version of the game.

Best regards,
The Sovereign's Dilemma Development Team

---
Questions? Reply to this email or reach out on Discord.
Game Website: https://sovereignsdilemma.com
Beta Testing Guide: [Attached]
EOF

    info "Acceptance email template created for $applicant_email"
    # In a real implementation, this would send the actual email
}

# Main execution functions
recruitment_campaign() {
    log "🎯 Starting beta tester recruitment campaign..."

    init_database
    create_application_form

    # Simulate application processing (in real implementation, this would process actual applications)
    info "Application form available at: $TEMPLATES_DIR/application-form.html"
    info "Application processing system ready"
    info "Recruitment target: $TARGET_TESTERS testers"

    log "✅ Recruitment campaign infrastructure ready"
}

process_applications() {
    log "📋 Processing beta testing applications..."

    if [[ ! -d "$APPLICATIONS_DIR" ]]; then
        warn "No applications directory found. Creating sample applications for testing..."
        mkdir -p "$APPLICATIONS_DIR"

        # Create sample applications for demonstration
        for i in {1..5}; do
            cat > "$APPLICATIONS_DIR/application_$i.json" << EOF
{
    "fullName": "Test Applicant $i",
    "email": "tester$i@example.com",
    "ageRange": "25-34",
    "location": "Amsterdam, Netherlands",
    "primaryLanguage": "Dutch",
    "politicalInterest": "$((6 + RANDOM % 4))",
    "strategyGameExperience": "$((5 + RANDOM % 5))",
    "timeCommitment": "6-10",
    "betaExperience": "some",
    "operatingSystem": "Windows 11",
    "internetConnection": "fiber"
}
EOF
        done
    fi

    local processed_count=0
    for application_file in "$APPLICATIONS_DIR"/*.json; do
        if [[ -f "$application_file" && ! -f "${application_file}.processed" ]]; then
            decision=$(process_application "$application_file")

            # Extract demographic info for stats
            local demographic_category=$(jq -r '.processing.demographic_category // empty' "${application_file}.processed")
            update_recruitment_stats "$decision" "$demographic_category"

            # Send notifications based on decision
            if [[ "$decision" == "approved" ]]; then
                local applicant_name=$(jq -r '.fullName' "$application_file")
                local applicant_email=$(jq -r '.email' "$application_file")
                send_acceptance_email "$applicant_email" "$applicant_name"
            fi

            ((processed_count++))
        fi
    done

    log "✅ Processed $processed_count applications"
}

# Main script execution
main() {
    case "${1:-campaign}" in
        "campaign")
            recruitment_campaign
            ;;
        "process")
            process_applications
            ;;
        "report")
            generate_recruitment_report
            ;;
        "all")
            recruitment_campaign
            process_applications
            generate_recruitment_report
            ;;
        *)
            echo "Usage: $0 {campaign|process|report|all}"
            echo "  campaign - Set up recruitment infrastructure"
            echo "  process  - Process pending applications"
            echo "  report   - Generate recruitment progress report"
            echo "  all      - Run complete recruitment workflow"
            exit 1
            ;;
    esac
}

# Execute main function with arguments
main "$@"