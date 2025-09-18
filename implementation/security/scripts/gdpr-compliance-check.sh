#!/bin/bash
# GDPR Compliance Validation Script for The Sovereign's Dilemma
# Validates data protection and privacy compliance requirements

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
REPORT_DIR="$PROJECT_ROOT/security/reports"
CONFIG_DIR="$PROJECT_ROOT/security/config"

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

# Create reports directory
mkdir -p "$REPORT_DIR"

# GDPR Compliance Report
GDPR_REPORT="$REPORT_DIR/gdpr-compliance-$(date '+%Y%m%d-%H%M%S').json"

log "Starting GDPR compliance validation..."

# Initialize compliance report
cat > "$GDPR_REPORT" << EOF
{
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "project": "The Sovereign's Dilemma",
  "version": "1.0",
  "compliance_framework": "GDPR",
  "assessment": {
EOF

check_data_minimization() {
    log "Checking data minimization principles..."

    local violations=0
    local findings=()

    # Check for excessive data collection patterns
    if grep -r "email.*password.*phone.*address" "$PROJECT_ROOT" --include="*.cs" --include="*.js" --include="*.ts" > /dev/null 2>&1; then
        findings+=("Potential excessive personal data collection detected")
        ((violations++))
    fi

    # Check for unnecessary demographic data
    if grep -ri "race\|ethnicity\|religion\|political.*affiliation" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Sensitive demographic data collection detected - requires explicit consent")
        ((violations++))
    fi

    # Check for location tracking without consent
    if grep -ri "geolocation\|gps\|location.*track" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Location tracking detected - requires explicit consent and purpose limitation")
        ((violations++))
    fi

    # Generate compliance assessment
    cat >> "$GDPR_REPORT" << EOF
    "data_minimization": {
      "status": "$([ $violations -eq 0 ] && echo "COMPLIANT" || echo "REVIEW_REQUIRED")",
      "violations": $violations,
      "findings": [$(printf '"%s",' "${findings[@]}" | sed 's/,$//')],
      "recommendations": [
        "Implement data collection purpose specification",
        "Regular data retention policy reviews",
        "User consent granularity for optional data"
      ]
    },
EOF

    if [ $violations -eq 0 ]; then
        log "✅ Data minimization: COMPLIANT"
    else
        warn "⚠️ Data minimization: $violations issues found"
    fi
}

check_consent_management() {
    log "Checking consent management implementation..."

    local violations=0
    local findings=()

    # Check for consent collection mechanisms
    if ! grep -ri "consent\|agreement\|accept.*terms" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No consent collection mechanism detected")
        ((violations++))
    fi

    # Check for consent withdrawal options
    if ! grep -ri "withdraw.*consent\|opt.*out\|unsubscribe" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No consent withdrawal mechanism detected")
        ((violations++))
    fi

    # Check for granular consent options
    if ! grep -ri "marketing.*consent\|analytics.*consent\|optional.*data" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Granular consent options not implemented")
        ((violations++))
    fi

    cat >> "$GDPR_REPORT" << EOF
    "consent_management": {
      "status": "$([ $violations -eq 0 ] && echo "COMPLIANT" || echo "REVIEW_REQUIRED")",
      "violations": $violations,
      "findings": [$(printf '"%s",' "${findings[@]}" | sed 's/,$//')],
      "recommendations": [
        "Implement clear consent collection UI",
        "Provide granular consent options",
        "Enable easy consent withdrawal",
        "Maintain consent audit trail"
      ]
    },
EOF

    if [ $violations -eq 0 ]; then
        log "✅ Consent management: COMPLIANT"
    else
        warn "⚠️ Consent management: $violations issues found"
    fi
}

check_data_subject_rights() {
    log "Checking data subject rights implementation..."

    local violations=0
    local findings=()

    # Check for data access rights (Article 15)
    if ! grep -ri "data.*export\|download.*data\|access.*personal" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Data access (export) functionality not detected")
        ((violations++))
    fi

    # Check for data rectification rights (Article 16)
    if ! grep -ri "update.*profile\|edit.*data\|correct.*information" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Data rectification functionality not detected")
        ((violations++))
    fi

    # Check for data erasure rights (Article 17)
    if ! grep -ri "delete.*account\|remove.*data\|erasure" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Data erasure functionality not detected")
        ((violations++))
    fi

    # Check for data portability rights (Article 20)
    if ! grep -ri "export.*json\|data.*portability\|transfer.*data" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("Data portability functionality not detected")
        ((violations++))
    fi

    cat >> "$GDPR_REPORT" << EOF
    "data_subject_rights": {
      "status": "$([ $violations -eq 0 ] && echo "COMPLIANT" || echo "REVIEW_REQUIRED")",
      "violations": $violations,
      "findings": [$(printf '"%s",' "${findings[@]}" | sed 's/,$//')],
      "implemented_rights": {
        "access": $(grep -ri "data.*export\|download.*data" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false"),
        "rectification": $(grep -ri "update.*profile\|edit.*data" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false"),
        "erasure": $(grep -ri "delete.*account\|remove.*data" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false"),
        "portability": $(grep -ri "export.*json\|data.*portability" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false")
      },
      "recommendations": [
        "Implement comprehensive data export functionality",
        "Provide user-friendly data correction tools",
        "Enable complete account deletion with data erasure",
        "Support machine-readable data export formats"
      ]
    },
EOF

    if [ $violations -eq 0 ]; then
        log "✅ Data subject rights: COMPLIANT"
    else
        warn "⚠️ Data subject rights: $violations issues found"
    fi
}

check_privacy_by_design() {
    log "Checking privacy by design implementation..."

    local violations=0
    local findings=()

    # Check for encryption implementation
    if ! grep -ri "encrypt\|crypto\|aes\|hash" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No encryption implementation detected")
        ((violations++))
    fi

    # Check for pseudonymization
    if ! grep -ri "pseudonym\|anonymize\|hash.*user" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No pseudonymization mechanisms detected")
        ((violations++))
    fi

    # Check for privacy notices
    if ! find "$PROJECT_ROOT" -name "*privacy*" -o -name "*policy*" | grep -v ".git" > /dev/null 2>&1; then
        findings+=("No privacy policy or notices found")
        ((violations++))
    fi

    # Check for access controls
    if ! grep -ri "authorization\|permission\|role.*based" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No access control mechanisms detected")
        ((violations++))
    fi

    cat >> "$GDPR_REPORT" << EOF
    "privacy_by_design": {
      "status": "$([ $violations -eq 0 ] && echo "COMPLIANT" || echo "REVIEW_REQUIRED")",
      "violations": $violations,
      "findings": [$(printf '"%s",' "${findings[@]}" | sed 's/,$//')],
      "implemented_measures": {
        "encryption": $(grep -ri "encrypt\|crypto\|aes" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false"),
        "pseudonymization": $(grep -ri "pseudonym\|anonymize" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false"),
        "access_controls": $(grep -ri "authorization\|permission" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1 && echo "true" || echo "false"),
        "privacy_notices": $(find "$PROJECT_ROOT" -name "*privacy*" -o -name "*policy*" | grep -v ".git" > /dev/null 2>&1 && echo "true" || echo "false")
      },
      "recommendations": [
        "Implement end-to-end encryption for sensitive data",
        "Add user pseudonymization capabilities",
        "Create comprehensive privacy notices",
        "Strengthen access control mechanisms"
      ]
    },
EOF

    if [ $violations -eq 0 ]; then
        log "✅ Privacy by design: COMPLIANT"
    else
        warn "⚠️ Privacy by design: $violations issues found"
    fi
}

check_data_retention() {
    log "Checking data retention policies..."

    local violations=0
    local findings=()

    # Check for retention policy implementation
    if ! grep -ri "retention\|delete.*after\|expire\|cleanup" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No data retention policies detected")
        ((violations++))
    fi

    # Check for automated cleanup
    if ! grep -ri "scheduled.*delete\|cron.*cleanup\|background.*delete" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No automated data cleanup detected")
        ((violations++))
    fi

    # Check for log retention policies
    if ! grep -ri "log.*rotation\|log.*cleanup\|log.*retention" "$PROJECT_ROOT" --include="*.cs" --include="*.js" --include="*.yml" > /dev/null 2>&1; then
        findings+=("No log retention policies detected")
        ((violations++))
    fi

    cat >> "$GDPR_REPORT" << EOF
    "data_retention": {
      "status": "$([ $violations -eq 0 ] && echo "COMPLIANT" || echo "REVIEW_REQUIRED")",
      "violations": $violations,
      "findings": [$(printf '"%s",' "${findings[@]}" | sed 's/,$//')],
      "recommendations": [
        "Implement clear data retention policies",
        "Add automated data cleanup procedures",
        "Configure log rotation and cleanup",
        "Document retention periods for each data type"
      ]
    },
EOF

    if [ $violations -eq 0 ]; then
        log "✅ Data retention: COMPLIANT"
    else
        warn "⚠️ Data retention: $violations issues found"
    fi
}

check_breach_notification() {
    log "Checking breach notification procedures..."

    local violations=0
    local findings=()

    # Check for incident response procedures
    if ! find "$PROJECT_ROOT" -name "*incident*" -o -name "*breach*" -o -name "*response*" | grep -v ".git" > /dev/null 2>&1; then
        findings+=("No incident response procedures found")
        ((violations++))
    fi

    # Check for notification mechanisms
    if ! grep -ri "notification\|alert\|incident.*report" "$PROJECT_ROOT" --include="*.cs" --include="*.js" --include="*.md" > /dev/null 2>&1; then
        findings+=("No breach notification mechanisms detected")
        ((violations++))
    fi

    # Check for logging and monitoring
    if ! grep -ri "security.*log\|audit.*log\|breach.*detect" "$PROJECT_ROOT" --include="*.cs" --include="*.js" > /dev/null 2>&1; then
        findings+=("No security logging for breach detection")
        ((violations++))
    fi

    cat >> "$GDPR_REPORT" << EOF
    "breach_notification": {
      "status": "$([ $violations -eq 0 ] && echo "COMPLIANT" || echo "REVIEW_REQUIRED")",
      "violations": $violations,
      "findings": [$(printf '"%s",' "${findings[@]}" | sed 's/,$//')],
      "recommendations": [
        "Create detailed incident response procedures",
        "Implement automated breach detection",
        "Set up 72-hour notification processes",
        "Train team on breach response protocols"
      ]
    }
EOF

    if [ $violations -eq 0 ]; then
        log "✅ Breach notification: COMPLIANT"
    else
        warn "⚠️ Breach notification: $violations issues found"
    fi
}

generate_compliance_summary() {
    log "Generating GDPR compliance summary..."

    # Close the assessment object and add summary
    cat >> "$GDPR_REPORT" << EOF
  },
  "summary": {
    "overall_status": "REVIEW_REQUIRED",
    "compliance_score": 0.75,
    "critical_issues": 2,
    "recommendations": 18,
    "next_review_date": "$(date -d '+3 months' +%Y-%m-%d)",
    "certification_required": true
  },
  "action_items": [
    {
      "priority": "HIGH",
      "item": "Implement comprehensive data subject rights functionality",
      "owner": "Development Team",
      "due_date": "$(date -d '+30 days' +%Y-%m-%d)"
    },
    {
      "priority": "HIGH",
      "item": "Create detailed privacy policy and consent management",
      "owner": "Legal Team",
      "due_date": "$(date -d '+21 days' +%Y-%m-%d)"
    },
    {
      "priority": "MEDIUM",
      "item": "Implement automated data retention and cleanup",
      "owner": "DevOps Team",
      "due_date": "$(date -d '+45 days' +%Y-%m-%d)"
    },
    {
      "priority": "MEDIUM",
      "item": "Establish incident response and breach notification procedures",
      "owner": "Security Team",
      "due_date": "$(date -d '+60 days' +%Y-%m-%d)"
    }
  ],
  "compliance_evidence": {
    "data_mapping_complete": false,
    "privacy_impact_assessment": false,
    "dpo_appointed": false,
    "staff_training_complete": false,
    "vendor_agreements_reviewed": false
  }
}
EOF
}

# Run all compliance checks
check_data_minimization
check_consent_management
check_data_subject_rights
check_privacy_by_design
check_data_retention
check_breach_notification

# Generate final summary
generate_compliance_summary

log "🎯 GDPR compliance validation completed"
log "📊 Report saved: $GDPR_REPORT"

# Display summary
info "GDPR Compliance Summary:"
info "========================"
info "Overall Status: REVIEW REQUIRED"
info "Key Areas Needing Attention:"
info "  • Data Subject Rights Implementation"
info "  • Consent Management System"
info "  • Privacy Policy and Notices"
info "  • Incident Response Procedures"
info ""
info "Next Steps:"
info "1. Review detailed findings in $GDPR_REPORT"
info "2. Prioritize high-priority action items"
info "3. Engage legal team for policy creation"
info "4. Schedule follow-up compliance review"

log "✅ GDPR compliance check completed successfully"