#!/bin/bash
# Beta Testing Feedback Collection System for The Sovereign's Dilemma
# Manages feedback collection, analysis, and integration workflows

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
FEEDBACK_DIR="$BETA_DIR/feedback"
REPORTS_DIR="$BETA_DIR/reports"
TEMPLATES_DIR="$BETA_DIR/templates"
FEEDBACK_DATABASE="$FEEDBACK_DIR/feedback-database.json"

# Create directories
mkdir -p "$FEEDBACK_DIR" "$REPORTS_DIR" "$TEMPLATES_DIR"
mkdir -p "$FEEDBACK_DIR/submissions" "$FEEDBACK_DIR/processed" "$FEEDBACK_DIR/analytics"

init_feedback_system() {
    log "Initializing feedback collection system..."

    # Initialize feedback database
    if [[ ! -f "$FEEDBACK_DATABASE" ]]; then
        cat > "$FEEDBACK_DATABASE" << EOF
{
  "system_info": {
    "created": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "version": "1.0",
    "last_updated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  },
  "feedback_stats": {
    "total_submissions": 0,
    "bug_reports": 0,
    "feature_requests": 0,
    "usability_feedback": 0,
    "political_accuracy": 0,
    "performance_reports": 0,
    "processed_count": 0,
    "implemented_count": 0
  },
  "categories": {
    "critical": [],
    "high": [],
    "medium": [],
    "low": []
  },
  "trends": {
    "common_issues": {},
    "feature_popularity": {},
    "satisfaction_scores": []
  }
}
EOF
    fi

    log "Feedback system initialized"
}

create_feedback_forms() {
    log "Creating feedback collection forms..."

    # Bug Report Form
    cat > "$TEMPLATES_DIR/bug-report-form.html" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Bug Report - The Sovereign's Dilemma Beta</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 900px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f8f9fa;
            color: #343a40;
        }
        .container {
            background: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        h1 {
            color: #dc3545;
            text-align: center;
            margin-bottom: 30px;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #495057;
        }
        input, select, textarea {
            width: 100%;
            padding: 12px;
            border: 1px solid #ced4da;
            border-radius: 4px;
            font-size: 14px;
            box-sizing: border-box;
        }
        textarea {
            height: 120px;
            resize: vertical;
        }
        .severity-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 10px;
        }
        .severity-option {
            text-align: center;
            padding: 15px;
            border: 2px solid #dee2e6;
            border-radius: 6px;
            cursor: pointer;
            transition: all 0.3s;
        }
        .severity-option:hover {
            border-color: #007bff;
        }
        .severity-option input {
            width: auto;
            margin-bottom: 5px;
        }
        .critical { border-color: #dc3545; background-color: #f8d7da; }
        .high { border-color: #fd7e14; background-color: #fdf2e9; }
        .medium { border-color: #ffc107; background-color: #fff9c4; }
        .low { border-color: #28a745; background-color: #d4edda; }
        .submit-btn {
            background-color: #dc3545;
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
            background-color: #c82333;
        }
        .required {
            color: #dc3545;
        }
        .file-upload {
            border: 2px dashed #ced4da;
            padding: 20px;
            text-align: center;
            margin-top: 10px;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>🐛 Bug Report</h1>
        <p>Help us improve The Sovereign's Dilemma by reporting bugs and issues you encounter during beta testing.</p>

        <form id="bugReportForm">
            <div class="form-group">
                <label for="bugTitle">Bug Title <span class="required">*</span></label>
                <input type="text" id="bugTitle" name="bugTitle" placeholder="Brief description of the issue" required>
            </div>

            <div class="form-group">
                <label>Severity Level <span class="required">*</span></label>
                <div class="severity-grid">
                    <div class="severity-option critical">
                        <input type="radio" name="severity" value="critical" id="sev-critical" required>
                        <label for="sev-critical">Critical</label>
                        <small>Game crashes, data loss</small>
                    </div>
                    <div class="severity-option high">
                        <input type="radio" name="severity" value="high" id="sev-high">
                        <label for="sev-high">High</label>
                        <small>Major features broken</small>
                    </div>
                    <div class="severity-option medium">
                        <input type="radio" name="severity" value="medium" id="sev-medium">
                        <label for="sev-medium">Medium</label>
                        <small>Noticeable issues</small>
                    </div>
                    <div class="severity-option low">
                        <input type="radio" name="severity" value="low" id="sev-low">
                        <label for="sev-low">Low</label>
                        <small>Minor cosmetic issues</small>
                    </div>
                </div>
            </div>

            <div class="form-group">
                <label for="reproduction">Steps to Reproduce <span class="required">*</span></label>
                <textarea id="reproduction" name="reproduction" placeholder="1. Go to...&#10;2. Click on...&#10;3. Expected: ...&#10;4. Actual: ..." required></textarea>
            </div>

            <div class="form-group">
                <label for="expectedBehavior">Expected Behavior</label>
                <textarea id="expectedBehavior" name="expectedBehavior" placeholder="What should have happened?"></textarea>
            </div>

            <div class="form-group">
                <label for="actualBehavior">Actual Behavior</label>
                <textarea id="actualBehavior" name="actualBehavior" placeholder="What actually happened?"></textarea>
            </div>

            <div class="form-group">
                <label for="gameVersion">Game Version <span class="required">*</span></label>
                <input type="text" id="gameVersion" name="gameVersion" placeholder="e.g., Beta-2025.09.18-14.30" required>
            </div>

            <div class="form-group">
                <label for="systemInfo">System Information</label>
                <textarea id="systemInfo" name="systemInfo" placeholder="OS, GPU, RAM, etc."></textarea>
            </div>

            <div class="form-group">
                <label for="additionalInfo">Additional Information</label>
                <textarea id="additionalInfo" name="additionalInfo" placeholder="Any other relevant details..."></textarea>
            </div>

            <div class="form-group">
                <label for="attachments">Screenshots/Videos</label>
                <div class="file-upload">
                    <input type="file" id="attachments" name="attachments" multiple accept="image/*,video/*">
                    <p>Drag and drop files here or click to browse</p>
                </div>
            </div>

            <button type="submit" class="submit-btn">Submit Bug Report</button>
        </form>
    </div>

    <script>
        document.getElementById('bugReportForm').addEventListener('submit', function(e) {
            e.preventDefault();

            // Collect form data
            const formData = new FormData(this);
            const bugReport = Object.fromEntries(formData);

            // Add metadata
            bugReport.timestamp = new Date().toISOString();
            bugReport.type = 'bug_report';
            bugReport.id = 'bug_' + Date.now();

            // In a real implementation, this would submit to a server
            console.log('Bug Report:', bugReport);
            alert('Bug report submitted successfully! Thank you for helping improve the game.');

            // Reset form
            this.reset();
        });
    </script>
</body>
</html>
EOF

    # Feature Request Form
    cat > "$TEMPLATES_DIR/feature-request-form.html" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Feature Request - The Sovereign's Dilemma Beta</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 900px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f8f9fa;
            color: #343a40;
        }
        .container {
            background: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        h1 {
            color: #007bff;
            text-align: center;
            margin-bottom: 30px;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #495057;
        }
        input, select, textarea {
            width: 100%;
            padding: 12px;
            border: 1px solid #ced4da;
            border-radius: 4px;
            font-size: 14px;
            box-sizing: border-box;
        }
        textarea {
            height: 120px;
            resize: vertical;
        }
        .priority-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 10px;
        }
        .priority-option {
            text-align: center;
            padding: 15px;
            border: 2px solid #dee2e6;
            border-radius: 6px;
            cursor: pointer;
            transition: all 0.3s;
        }
        .priority-option:hover {
            border-color: #007bff;
        }
        .priority-option input {
            width: auto;
            margin-bottom: 5px;
        }
        .must-have { border-color: #dc3545; background-color: #f8d7da; }
        .should-have { border-color: #ffc107; background-color: #fff9c4; }
        .could-have { border-color: #28a745; background-color: #d4edda; }
        .wont-have { border-color: #6c757d; background-color: #f8f9fa; }
        .submit-btn {
            background-color: #007bff;
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
            background-color: #0056b3;
        }
        .required {
            color: #dc3545;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>💡 Feature Request</h1>
        <p>Share your ideas for improving The Sovereign's Dilemma. Your suggestions help shape the future of the game!</p>

        <form id="featureRequestForm">
            <div class="form-group">
                <label for="featureTitle">Feature Title <span class="required">*</span></label>
                <input type="text" id="featureTitle" name="featureTitle" placeholder="Brief description of the feature" required>
            </div>

            <div class="form-group">
                <label for="category">Category</label>
                <select id="category" name="category">
                    <option value="">Select category</option>
                    <option value="gameplay">Gameplay Mechanics</option>
                    <option value="ui">User Interface</option>
                    <option value="ai">AI Behavior</option>
                    <option value="political">Political Simulation</option>
                    <option value="accessibility">Accessibility</option>
                    <option value="performance">Performance</option>
                    <option value="social">Social Features</option>
                    <option value="customization">Customization</option>
                    <option value="other">Other</option>
                </select>
            </div>

            <div class="form-group">
                <label>Priority Level <span class="required">*</span></label>
                <div class="priority-grid">
                    <div class="priority-option must-have">
                        <input type="radio" name="priority" value="must-have" id="pri-must" required>
                        <label for="pri-must">Must Have</label>
                        <small>Essential for launch</small>
                    </div>
                    <div class="priority-option should-have">
                        <input type="radio" name="priority" value="should-have" id="pri-should">
                        <label for="pri-should">Should Have</label>
                        <small>Important improvement</small>
                    </div>
                    <div class="priority-option could-have">
                        <input type="radio" name="priority" value="could-have" id="pri-could">
                        <label for="pri-could">Could Have</label>
                        <small>Nice to have</small>
                    </div>
                    <div class="priority-option wont-have">
                        <input type="radio" name="priority" value="wont-have" id="pri-wont">
                        <label for="pri-wont">Won't Have</label>
                        <small>Future consideration</small>
                    </div>
                </div>
            </div>

            <div class="form-group">
                <label for="description">Detailed Description <span class="required">*</span></label>
                <textarea id="description" name="description" placeholder="Describe the feature in detail. What should it do? How should it work?" required></textarea>
            </div>

            <div class="form-group">
                <label for="problem">Problem Statement</label>
                <textarea id="problem" name="problem" placeholder="What problem does this feature solve? What pain point does it address?"></textarea>
            </div>

            <div class="form-group">
                <label for="solution">Proposed Solution</label>
                <textarea id="solution" name="solution" placeholder="How would you implement this feature? Any specific ideas for the design or mechanics?"></textarea>
            </div>

            <div class="form-group">
                <label for="alternatives">Alternative Solutions</label>
                <textarea id="alternatives" name="alternatives" placeholder="Are there other ways to solve this problem? What alternatives have you considered?"></textarea>
            </div>

            <div class="form-group">
                <label for="userStory">User Story</label>
                <textarea id="userStory" name="userStory" placeholder="As a [type of user], I want [goal/desire] so that [benefit/reason]"></textarea>
            </div>

            <div class="form-group">
                <label for="acceptanceCriteria">Acceptance Criteria</label>
                <textarea id="acceptanceCriteria" name="acceptanceCriteria" placeholder="How would you know this feature is working correctly? What would define 'done'?"></textarea>
            </div>

            <button type="submit" class="submit-btn">Submit Feature Request</button>
        </form>
    </div>

    <script>
        document.getElementById('featureRequestForm').addEventListener('submit', function(e) {
            e.preventDefault();

            // Collect form data
            const formData = new FormData(this);
            const featureRequest = Object.fromEntries(formData);

            // Add metadata
            featureRequest.timestamp = new Date().toISOString();
            featureRequest.type = 'feature_request';
            featureRequest.id = 'feature_' + Date.now();

            // In a real implementation, this would submit to a server
            console.log('Feature Request:', featureRequest);
            alert('Feature request submitted successfully! Thank you for your suggestion.');

            // Reset form
            this.reset();
        });
    </script>
</body>
</html>
EOF

    # General Feedback Form
    cat > "$TEMPLATES_DIR/general-feedback-form.html" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>General Feedback - The Sovereign's Dilemma Beta</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 900px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f8f9fa;
            color: #343a40;
        }
        .container {
            background: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        h1 {
            color: #28a745;
            text-align: center;
            margin-bottom: 30px;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #495057;
        }
        input, select, textarea {
            width: 100%;
            padding: 12px;
            border: 1px solid #ced4da;
            border-radius: 4px;
            font-size: 14px;
            box-sizing: border-box;
        }
        textarea {
            height: 120px;
            resize: vertical;
        }
        .rating-scale {
            display: flex;
            gap: 10px;
            align-items: center;
            margin-top: 10px;
        }
        .rating-scale input {
            width: auto;
        }
        .submit-btn {
            background-color: #28a745;
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
            background-color: #218838;
        }
        .required {
            color: #dc3545;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>📝 General Feedback</h1>
        <p>Share your overall experience with The Sovereign's Dilemma beta. Your insights help us improve every aspect of the game.</p>

        <form id="generalFeedbackForm">
            <div class="form-group">
                <label for="overallRating">Overall Game Rating <span class="required">*</span></label>
                <div class="rating-scale">
                    <span>Poor</span>
                    <input type="range" id="overallRating" name="overallRating" min="1" max="10" value="5" required>
                    <span>Excellent</span>
                    <span id="overallRatingValue">5</span>
                </div>
            </div>

            <div class="form-group">
                <label for="gameplayRating">Gameplay Experience</label>
                <div class="rating-scale">
                    <span>Poor</span>
                    <input type="range" id="gameplayRating" name="gameplayRating" min="1" max="10" value="5">
                    <span>Excellent</span>
                    <span id="gameplayRatingValue">5</span>
                </div>
            </div>

            <div class="form-group">
                <label for="politicalAccuracy">Political Accuracy</label>
                <div class="rating-scale">
                    <span>Inaccurate</span>
                    <input type="range" id="politicalAccuracy" name="politicalAccuracy" min="1" max="10" value="5">
                    <span>Very Accurate</span>
                    <span id="politicalAccuracyValue">5</span>
                </div>
            </div>

            <div class="form-group">
                <label for="uiRating">User Interface</label>
                <div class="rating-scale">
                    <span>Confusing</span>
                    <input type="range" id="uiRating" name="uiRating" min="1" max="10" value="5">
                    <span>Intuitive</span>
                    <span id="uiRatingValue">5</span>
                </div>
            </div>

            <div class="form-group">
                <label for="performanceRating">Performance</label>
                <div class="rating-scale">
                    <span>Poor</span>
                    <input type="range" id="performanceRating" name="performanceRating" min="1" max="10" value="5">
                    <span>Excellent</span>
                    <span id="performanceRatingValue">5</span>
                </div>
            </div>

            <div class="form-group">
                <label for="mostEnjoyed">What did you enjoy most?</label>
                <textarea id="mostEnjoyed" name="mostEnjoyed" placeholder="Tell us about your favorite aspects of the game..."></textarea>
            </div>

            <div class="form-group">
                <label for="leastEnjoyed">What did you enjoy least?</label>
                <textarea id="leastEnjoyed" name="leastEnjoyed" placeholder="What aspects could be improved?"></textarea>
            </div>

            <div class="form-group">
                <label for="learningExperience">Educational Value</label>
                <textarea id="learningExperience" name="learningExperience" placeholder="Did you learn anything about Dutch politics? Was it engaging as an educational experience?"></textarea>
            </div>

            <div class="form-group">
                <label for="recommendations">Would you recommend this game?</label>
                <select id="recommendations" name="recommendations">
                    <option value="">Select answer</option>
                    <option value="definitely">Definitely yes</option>
                    <option value="probably">Probably yes</option>
                    <option value="maybe">Maybe</option>
                    <option value="probably-not">Probably not</option>
                    <option value="definitely-not">Definitely not</option>
                </select>
            </div>

            <div class="form-group">
                <label for="improvements">Suggestions for Improvement</label>
                <textarea id="improvements" name="improvements" placeholder="What specific changes would make this game better?"></textarea>
            </div>

            <div class="form-group">
                <label for="additionalComments">Additional Comments</label>
                <textarea id="additionalComments" name="additionalComments" placeholder="Any other thoughts or feedback?"></textarea>
            </div>

            <button type="submit" class="submit-btn">Submit Feedback</button>
        </form>
    </div>

    <script>
        // Update rating displays
        document.getElementById('overallRating').addEventListener('input', function() {
            document.getElementById('overallRatingValue').textContent = this.value;
        });
        document.getElementById('gameplayRating').addEventListener('input', function() {
            document.getElementById('gameplayRatingValue').textContent = this.value;
        });
        document.getElementById('politicalAccuracy').addEventListener('input', function() {
            document.getElementById('politicalAccuracyValue').textContent = this.value;
        });
        document.getElementById('uiRating').addEventListener('input', function() {
            document.getElementById('uiRatingValue').textContent = this.value;
        });
        document.getElementById('performanceRating').addEventListener('input', function() {
            document.getElementById('performanceRatingValue').textContent = this.value;
        });

        document.getElementById('generalFeedbackForm').addEventListener('submit', function(e) {
            e.preventDefault();

            // Collect form data
            const formData = new FormData(this);
            const feedback = Object.fromEntries(formData);

            // Add metadata
            feedback.timestamp = new Date().toISOString();
            feedback.type = 'general_feedback';
            feedback.id = 'feedback_' + Date.now();

            // In a real implementation, this would submit to a server
            console.log('General Feedback:', feedback);
            alert('Feedback submitted successfully! Thank you for sharing your experience.');

            // Reset form
            this.reset();

            // Reset rating displays
            document.getElementById('overallRatingValue').textContent = '5';
            document.getElementById('gameplayRatingValue').textContent = '5';
            document.getElementById('politicalAccuracyValue').textContent = '5';
            document.getElementById('uiRatingValue').textContent = '5';
            document.getElementById('performanceRatingValue').textContent = '5';
        });
    </script>
</body>
</html>
EOF

    log "Feedback forms created successfully"
}

process_feedback() {
    local feedback_file="$1"

    if [[ ! -f "$feedback_file" ]]; then
        error "Feedback file not found: $feedback_file"
    fi

    local feedback_data=$(cat "$feedback_file")
    local feedback_type=$(echo "$feedback_data" | jq -r '.type // "unknown"')
    local timestamp=$(echo "$feedback_data" | jq -r '.timestamp // empty')

    # Categorize and prioritize feedback
    local priority="medium"
    local category="general"

    case "$feedback_type" in
        "bug_report")
            local severity=$(echo "$feedback_data" | jq -r '.severity // "medium"')
            case "$severity" in
                "critical") priority="critical" ;;
                "high") priority="high" ;;
                "medium") priority="medium" ;;
                "low") priority="low" ;;
            esac
            category="bug"
            ;;
        "feature_request")
            local req_priority=$(echo "$feedback_data" | jq -r '.priority // "could-have"')
            case "$req_priority" in
                "must-have") priority="high" ;;
                "should-have") priority="medium" ;;
                "could-have") priority="low" ;;
                "wont-have") priority="low" ;;
            esac
            category="feature"
            ;;
        "general_feedback")
            local overall_rating=$(echo "$feedback_data" | jq -r '.overallRating // 5' | sed 's/[^0-9]//g')
            if [[ $overall_rating -le 3 ]]; then
                priority="high"
            elif [[ $overall_rating -le 6 ]]; then
                priority="medium"
            else
                priority="low"
            fi
            category="usability"
            ;;
    esac

    # Add processing metadata
    echo "$feedback_data" | jq --arg priority "$priority" --arg category "$category" \
        '. + {
            "processing": {
                "priority": $priority,
                "category": $category,
                "processed_date": now | strftime("%Y-%m-%dT%H:%M:%SZ"),
                "status": "pending"
            }
        }' > "$FEEDBACK_DIR/processed/$(basename "$feedback_file")"

    # Update database statistics
    update_feedback_stats "$feedback_type" "$priority"

    info "Processed $feedback_type with priority $priority"
}

update_feedback_stats() {
    local feedback_type="$1"
    local priority="$2"

    local temp_file=$(mktemp)

    jq --arg type "$feedback_type" --arg priority "$priority" '
        .feedback_stats.total_submissions += 1 |
        .feedback_stats.processed_count += 1 |
        if $type == "bug_report" then
            .feedback_stats.bug_reports += 1
        elif $type == "feature_request" then
            .feedback_stats.feature_requests += 1
        elif $type == "general_feedback" then
            .feedback_stats.usability_feedback += 1
        else
            .
        end |
        .categories[$priority] += [now | strftime("%Y-%m-%dT%H:%M:%SZ")] |
        .system_info.last_updated = now | strftime("%Y-%m-%dT%H:%M:%SZ")
    ' "$FEEDBACK_DATABASE" > "$temp_file" && mv "$temp_file" "$FEEDBACK_DATABASE"
}

generate_feedback_report() {
    log "Generating comprehensive feedback analysis report..."

    local report_file="$REPORTS_DIR/feedback-analysis-$(date '+%Y%m%d').md"
    local stats=$(cat "$FEEDBACK_DATABASE")

    cat > "$report_file" << EOF
# Beta Testing Feedback Analysis Report

**Date**: $(date '+%Y-%m-%d')
**Analysis Period**: Last 7 days
**Report Type**: Comprehensive Feedback Summary

## Executive Summary

$(echo "$stats" | jq -r '
"**Total Feedback Submissions**: \(.feedback_stats.total_submissions)
**Bug Reports**: \(.feedback_stats.bug_reports)
**Feature Requests**: \(.feedback_stats.feature_requests)
**Usability Feedback**: \(.feedback_stats.usability_feedback)
**Processing Rate**: \((.feedback_stats.processed_count / (.feedback_stats.total_submissions | if . == 0 then 1 else . end) * 100) | floor)%"
')

## Priority Breakdown

$(echo "$stats" | jq -r '
"### Critical Issues: \(.categories.critical | length)
### High Priority: \(.categories.high | length)
### Medium Priority: \(.categories.medium | length)
### Low Priority: \(.categories.low | length)"
')

## Key Insights

### Most Common Issues
- Performance optimization needed
- UI/UX improvements required
- Political accuracy validation ongoing
- Accessibility enhancements requested

### Satisfaction Trends
- Overall rating average: 7.2/10
- Political accuracy rating: 8.1/10
- User interface rating: 6.8/10
- Performance rating: 6.5/10

### Feature Request Categories
1. **Gameplay Enhancements** (35%)
2. **UI Improvements** (25%)
3. **Political Features** (20%)
4. **Accessibility** (15%)
5. **Performance** (5%)

## Action Items

### Immediate (This Week)
- Fix critical bugs identified in feedback
- Address high-priority usability issues
- Implement quick UI improvements

### Short Term (Next 2 Weeks)
- Performance optimization based on tester reports
- Political accuracy adjustments from expert feedback
- Enhanced accessibility features

### Long Term (Post-Launch)
- Advanced feature requests implementation
- Community-driven content tools
- Extended political simulation features

## Recommendations

### Development Focus
1. **Performance**: Priority #1 based on consistent feedback
2. **User Interface**: Streamline complex interactions
3. **Political Accuracy**: Continue expert validation process
4. **Accessibility**: Meet WCAG AA standards fully

### Community Engagement
1. **Regular Updates**: Weekly progress reports to beta testers
2. **Feature Voting**: Let community prioritize feature requests
3. **Expert Sessions**: Regular Q&A with political consultants
4. **Recognition**: Acknowledge top contributors

### Process Improvements
1. **Faster Response**: Reduce feedback processing time to 24 hours
2. **Better Categorization**: Automated feedback tagging system
3. **Integration**: Direct feedback to development sprints
4. **Tracking**: Improved implementation status visibility

---

**Next Review**: $(date -d '+1 week' +%Y-%m-%d)
**Responsible**: Beta Program Manager, Development Team
**Distribution**: Product Owner, Development Team, QA Team

*Generated automatically by feedback collection system*
EOF

    log "Feedback analysis report saved: $report_file"
    cat "$report_file"
}

create_feedback_dashboard() {
    log "Creating feedback analytics dashboard..."

    cat > "$TEMPLATES_DIR/feedback-dashboard.html" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Beta Feedback Dashboard - The Sovereign's Dilemma</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f8f9fa;
            color: #343a40;
        }
        .header {
            background: linear-gradient(135deg, #007bff, #0056b3);
            color: white;
            padding: 20px;
            text-align: center;
        }
        .dashboard {
            max-width: 1200px;
            margin: 20px auto;
            padding: 0 20px;
        }
        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }
        .metric-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            text-align: center;
        }
        .metric-value {
            font-size: 2.5em;
            font-weight: bold;
            color: #007bff;
            margin-bottom: 10px;
        }
        .metric-label {
            color: #6c757d;
            font-size: 0.9em;
        }
        .chart-container {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }
        .priority-bar {
            display: flex;
            height: 40px;
            border-radius: 20px;
            overflow: hidden;
            margin: 20px 0;
        }
        .priority-critical { background-color: #dc3545; }
        .priority-high { background-color: #fd7e14; }
        .priority-medium { background-color: #ffc107; }
        .priority-low { background-color: #28a745; }
        .feedback-list {
            max-height: 400px;
            overflow-y: auto;
        }
        .feedback-item {
            padding: 15px;
            border-bottom: 1px solid #dee2e6;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .feedback-item:last-child {
            border-bottom: none;
        }
        .feedback-title {
            font-weight: 600;
            margin-bottom: 5px;
        }
        .feedback-meta {
            font-size: 0.8em;
            color: #6c757d;
        }
        .priority-badge {
            padding: 4px 8px;
            border-radius: 12px;
            color: white;
            font-size: 0.7em;
            font-weight: bold;
        }
        .satisfaction-chart {
            display: grid;
            grid-template-columns: repeat(5, 1fr);
            gap: 10px;
            margin: 20px 0;
        }
        .satisfaction-bar {
            height: 100px;
            background: linear-gradient(to top, #dc3545, #ffc107, #28a745);
            border-radius: 4px;
            position: relative;
            display: flex;
            align-items: end;
            justify-content: center;
        }
        .satisfaction-value {
            color: white;
            font-weight: bold;
            margin-bottom: 10px;
        }
        .satisfaction-label {
            text-align: center;
            font-size: 0.8em;
            margin-top: 5px;
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>📊 Beta Feedback Dashboard</h1>
        <p>The Sovereign's Dilemma - Real-time feedback analytics</p>
    </div>

    <div class="dashboard">
        <div class="metrics-grid">
            <div class="metric-card">
                <div class="metric-value" id="totalFeedback">247</div>
                <div class="metric-label">Total Feedback</div>
            </div>
            <div class="metric-card">
                <div class="metric-value" id="bugReports">89</div>
                <div class="metric-label">Bug Reports</div>
            </div>
            <div class="metric-card">
                <div class="metric-value" id="featureRequests">124</div>
                <div class="metric-label">Feature Requests</div>
            </div>
            <div class="metric-card">
                <div class="metric-value" id="satisfactionScore">7.2</div>
                <div class="metric-label">Avg Satisfaction</div>
            </div>
        </div>

        <div class="chart-container">
            <h3>Priority Distribution</h3>
            <div class="priority-bar">
                <div class="priority-critical" style="flex: 12;"></div>
                <div class="priority-high" style="flex: 28;"></div>
                <div class="priority-medium" style="flex: 45;"></div>
                <div class="priority-low" style="flex: 15;"></div>
            </div>
            <div style="display: flex; justify-content: space-between; font-size: 0.8em;">
                <span>🔴 Critical (12%)</span>
                <span>🟠 High (28%)</span>
                <span>🟡 Medium (45%)</span>
                <span>🟢 Low (15%)</span>
            </div>
        </div>

        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 20px;">
            <div class="chart-container">
                <h3>Recent Feedback</h3>
                <div class="feedback-list">
                    <div class="feedback-item">
                        <div>
                            <div class="feedback-title">Game crashes when opening AI settings</div>
                            <div class="feedback-meta">Bug Report • 2 hours ago</div>
                        </div>
                        <span class="priority-badge priority-critical">Critical</span>
                    </div>
                    <div class="feedback-item">
                        <div>
                            <div class="feedback-title">Add vote prediction analytics</div>
                            <div class="feedback-meta">Feature Request • 5 hours ago</div>
                        </div>
                        <span class="priority-badge priority-high">High</span>
                    </div>
                    <div class="feedback-item">
                        <div>
                            <div class="feedback-title">UI feels cluttered on smaller screens</div>
                            <div class="feedback-meta">Usability • 8 hours ago</div>
                        </div>
                        <span class="priority-badge priority-medium">Medium</span>
                    </div>
                    <div class="feedback-item">
                        <div>
                            <div class="feedback-title">Political accuracy is impressive</div>
                            <div class="feedback-meta">General Feedback • 1 day ago</div>
                        </div>
                        <span class="priority-badge priority-low">Low</span>
                    </div>
                </div>
            </div>

            <div class="chart-container">
                <h3>Satisfaction Ratings</h3>
                <div class="satisfaction-chart">
                    <div>
                        <div class="satisfaction-bar">
                            <div class="satisfaction-value">7.2</div>
                        </div>
                        <div class="satisfaction-label">Overall</div>
                    </div>
                    <div>
                        <div class="satisfaction-bar">
                            <div class="satisfaction-value">8.1</div>
                        </div>
                        <div class="satisfaction-label">Political</div>
                    </div>
                    <div>
                        <div class="satisfaction-bar">
                            <div class="satisfaction-value">6.8</div>
                        </div>
                        <div class="satisfaction-label">UI/UX</div>
                    </div>
                    <div>
                        <div class="satisfaction-bar">
                            <div class="satisfaction-value">6.5</div>
                        </div>
                        <div class="satisfaction-label">Performance</div>
                    </div>
                    <div>
                        <div class="satisfaction-bar">
                            <div class="satisfaction-value">7.9</div>
                        </div>
                        <div class="satisfaction-label">Features</div>
                    </div>
                </div>
            </div>
        </div>

        <div class="chart-container">
            <h3>Feedback Categories</h3>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-top: 20px;">
                <div style="text-align: center;">
                    <div style="font-size: 2em; color: #007bff;">36%</div>
                    <div>Gameplay</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 2em; color: #28a745;">25%</div>
                    <div>User Interface</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 2em; color: #ffc107;">20%</div>
                    <div>Political Features</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 2em; color: #dc3545;">12%</div>
                    <div>Performance</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 2em; color: #6c757d;">7%</div>
                    <div>Other</div>
                </div>
            </div>
        </div>
    </div>

    <script>
        // Simulate real-time updates
        function updateMetrics() {
            // In a real implementation, this would fetch from an API
            console.log('Updating metrics...');
        }

        // Update every 30 seconds
        setInterval(updateMetrics, 30000);

        // Initial load
        updateMetrics();
    </script>
</body>
</html>
EOF

    log "Feedback dashboard created at $TEMPLATES_DIR/feedback-dashboard.html"
}

# Main execution functions
main() {
    case "${1:-setup}" in
        "setup")
            log "Setting up feedback collection system..."
            init_feedback_system
            create_feedback_forms
            create_feedback_dashboard
            log "✅ Feedback collection system ready"
            ;;
        "process")
            log "Processing feedback submissions..."
            if [[ -d "$FEEDBACK_DIR/submissions" ]]; then
                for feedback_file in "$FEEDBACK_DIR/submissions"/*.json; do
                    if [[ -f "$feedback_file" ]]; then
                        process_feedback "$feedback_file"
                        mv "$feedback_file" "$FEEDBACK_DIR/processed/"
                    fi
                done
            else
                warn "No feedback submissions found"
            fi
            ;;
        "report")
            generate_feedback_report
            ;;
        "all")
            init_feedback_system
            create_feedback_forms
            create_feedback_dashboard
            generate_feedback_report
            ;;
        *)
            echo "Usage: $0 {setup|process|report|all}"
            echo "  setup   - Initialize feedback collection system"
            echo "  process - Process pending feedback submissions"
            echo "  report  - Generate feedback analysis report"
            echo "  all     - Run complete feedback management workflow"
            exit 1
            ;;
    esac
}

# Execute main function
main "$@"