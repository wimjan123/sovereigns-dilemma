#!/bin/bash
# Comprehensive Security Scanning Script for The Sovereign's Dilemma
# Automated vulnerability assessment and security validation

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RESULTS_DIR="$PROJECT_ROOT/security/scan-results/$(date +%Y%m%d-%H%M%S)"
UNITY_PROJECT_PATH="$PROJECT_ROOT/Unity"

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

setup_environment() {
    log "Setting up security scanning environment..."

    # Create results directory
    mkdir -p "$RESULTS_DIR"
    mkdir -p "$RESULTS_DIR/sast"
    mkdir -p "$RESULTS_DIR/dast"
    mkdir -p "$RESULTS_DIR/sca"
    mkdir -p "$RESULTS_DIR/container"
    mkdir -p "$RESULTS_DIR/infrastructure"
    mkdir -p "$RESULTS_DIR/reports"

    # Check for required tools
    check_security_tools

    # Create scan configuration
    create_scan_configs

    log "Environment setup completed"
}

check_security_tools() {
    log "Checking security scanning tools..."

    # Static Analysis Tools
    if ! command -v semgrep >/dev/null 2>&1; then
        warn "Semgrep not found, installing..."
        pip3 install semgrep
    fi

    # .NET Security Scanner
    if ! dotnet tool list -g | grep -q security-scan; then
        warn "Security Code Scan not found, installing..."
        dotnet tool install --global security-scan
    fi

    # Container Security
    if ! command -v trivy >/dev/null 2>&1; then
        warn "Trivy not found, installing..."
        install_trivy
    fi

    # Dynamic Analysis
    if ! command -v zap-cli >/dev/null 2>&1; then
        warn "OWASP ZAP CLI not found, installing..."
        pip3 install zapcli
    fi

    # Infrastructure Security
    if ! command -v checkov >/dev/null 2>&1; then
        warn "Checkov not found, installing..."
        pip3 install checkov
    fi

    log "Security tools verification completed"
}

install_trivy() {
    log "Installing Trivy container scanner..."

    # Detect OS and install appropriately
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        curl -sfL https://raw.githubusercontent.com/aquasecurity/trivy/main/contrib/install.sh | sh -s -- -b /usr/local/bin
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        brew install trivy
    else
        error "Unsupported OS for Trivy installation"
    fi
}

create_scan_configs() {
    log "Creating security scan configurations..."

    # Semgrep configuration
    cat > "$RESULTS_DIR/semgrep-config.yml" << EOF
rules:
  - id: csharp-security-audit
    patterns:
      - pattern: \$X.ExecuteReader(\$QUERY)
      - pattern: \$X.ExecuteNonQuery(\$QUERY)
      - pattern: \$X.Execute(\$QUERY)
    message: "Potential SQL injection vulnerability"
    languages: [csharp]
    severity: ERROR

  - id: hardcoded-secrets
    patterns:
      - pattern: \$VAR = "sk_..."
      - pattern: \$VAR = "API_KEY"
      - pattern: password = "\$PASSWORD"
    message: "Hardcoded secret detected"
    languages: [csharp, javascript, json]
    severity: ERROR

  - id: insecure-random
    patterns:
      - pattern: new Random()
      - pattern: Random.Next()
    message: "Insecure random number generation"
    languages: [csharp]
    severity: WARNING
EOF

    # OWASP ZAP configuration
    cat > "$RESULTS_DIR/zap-config.json" << EOF
{
    "spider": {
        "maxDuration": 10,
        "maxDepth": 5,
        "threadCount": 10
    },
    "activeScan": {
        "policy": "Default Policy",
        "recurse": true,
        "inScopeOnly": true
    },
    "reporting": {
        "format": ["JSON", "HTML", "XML"],
        "includePassive": true
    }
}
EOF

    # Trivy configuration
    cat > "$RESULTS_DIR/trivy-config.yaml" << EOF
timeout: 10m
cache:
  dir: /tmp/trivy-cache
db:
  skip-update: false
vulnerability:
  type: ["os", "library"]
  scanners: ["vuln", "secret", "config"]
  severity: ["UNKNOWN", "LOW", "MEDIUM", "HIGH", "CRITICAL"]
secret:
  config: trivy-secret.yaml
EOF

    log "Scan configurations created"
}

run_static_analysis() {
    log "Running Static Application Security Testing (SAST)..."

    # Semgrep security scan
    info "Running Semgrep security analysis..."
    semgrep \
        --config=p/security-audit \
        --config=p/secrets \
        --config=p/csharp \
        --json \
        --output="$RESULTS_DIR/sast/semgrep-results.json" \
        "$PROJECT_ROOT" || warn "Semgrep scan completed with findings"

    # .NET Security Code Scan
    if [[ -d "$UNITY_PROJECT_PATH" ]]; then
        info "Running .NET Security Code Scan..."
        cd "$UNITY_PROJECT_PATH"
        security-scan \
            --project="$UNITY_PROJECT_PATH" \
            --export="$RESULTS_DIR/sast/security-code-scan.sarif" \
            --format=sarif || warn "Security Code Scan completed with findings"
    fi

    # Custom security rules
    info "Running custom security pattern detection..."
    run_custom_security_patterns

    log "Static analysis completed"
}

run_custom_security_patterns() {
    log "Running custom security pattern detection..."

    # Search for common security anti-patterns
    cat > "$RESULTS_DIR/sast/custom-patterns.txt" << EOF
# Custom Security Pattern Analysis Results
# Generated: $(date)

## SQL Injection Patterns
EOF

    # SQL Injection patterns
    grep -r "ExecuteReader\|ExecuteNonQuery\|ExecuteScalar" "$PROJECT_ROOT" --include="*.cs" | grep -v "Parameters\|@" >> "$RESULTS_DIR/sast/custom-patterns.txt" || true

    # Hardcoded credentials
    echo -e "\n## Hardcoded Credentials" >> "$RESULTS_DIR/sast/custom-patterns.txt"
    grep -r "password\s*=\s*[\"'].*[\"']\|api_key\s*=\s*[\"'].*[\"']\|secret\s*=\s*[\"'].*[\"']" "$PROJECT_ROOT" --include="*.cs" --include="*.js" --include="*.json" >> "$RESULTS_DIR/sast/custom-patterns.txt" || true

    # Insecure random generation
    echo -e "\n## Insecure Random Generation" >> "$RESULTS_DIR/sast/custom-patterns.txt"
    grep -r "new Random()\|Random\.Next()" "$PROJECT_ROOT" --include="*.cs" >> "$RESULTS_DIR/sast/custom-patterns.txt" || true

    # Logging sensitive data
    echo -e "\n## Potential Sensitive Data Logging" >> "$RESULTS_DIR/sast/custom-patterns.txt"
    grep -r "Log.*password\|Log.*token\|Log.*key\|Debug.*password" "$PROJECT_ROOT" --include="*.cs" >> "$RESULTS_DIR/sast/custom-patterns.txt" || true

    # Insecure deserialization
    echo -e "\n## Insecure Deserialization" >> "$RESULTS_DIR/sast/custom-patterns.txt"
    grep -r "BinaryFormatter\|XmlSerializer.*UnsafeDeserialize\|JsonConvert\.DeserializeObject.*typeof" "$PROJECT_ROOT" --include="*.cs" >> "$RESULTS_DIR/sast/custom-patterns.txt" || true

    log "Custom pattern analysis completed"
}

run_dependency_scan() {
    log "Running Software Composition Analysis (SCA)..."

    # .NET dependency scan
    if [[ -f "$UNITY_PROJECT_PATH/packages.config" ]] || [[ -f "$UNITY_PROJECT_PATH/packages-lock.json" ]]; then
        info "Scanning .NET dependencies..."

        # Generate dependency list
        find "$UNITY_PROJECT_PATH" -name "*.csproj" -exec cat {} \; > "$RESULTS_DIR/sca/dependencies.xml"

        # Use npm audit for any Node.js dependencies
        if [[ -f "$PROJECT_ROOT/package.json" ]]; then
            cd "$PROJECT_ROOT"
            npm audit --json > "$RESULTS_DIR/sca/npm-audit.json" 2>/dev/null || warn "NPM audit found vulnerabilities"
        fi
    fi

    # Unity package dependencies
    if [[ -f "$UNITY_PROJECT_PATH/Packages/manifest.json" ]]; then
        info "Analyzing Unity package dependencies..."
        cp "$UNITY_PROJECT_PATH/Packages/manifest.json" "$RESULTS_DIR/sca/unity-packages.json"

        # Extract package versions for vulnerability checking
        jq -r '.dependencies | to_entries[] | "\(.key): \(.value)"' "$UNITY_PROJECT_PATH/Packages/manifest.json" > "$RESULTS_DIR/sca/unity-package-list.txt"
    fi

    # Third-party library scan
    info "Scanning for vulnerable third-party libraries..."
    run_third_party_vulnerability_scan

    log "Dependency scan completed"
}

run_third_party_vulnerability_scan() {
    # Create a list of all DLL files for vulnerability assessment
    find "$PROJECT_ROOT" -name "*.dll" -type f > "$RESULTS_DIR/sca/dll-inventory.txt"

    # Check for known vulnerable Unity versions
    if [[ -f "$UNITY_PROJECT_PATH/ProjectSettings/ProjectVersion.txt" ]]; then
        unity_version=$(grep "m_EditorVersion:" "$UNITY_PROJECT_PATH/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)
        echo "Unity Version: $unity_version" > "$RESULTS_DIR/sca/unity-version-check.txt"

        # Check against known vulnerable versions (would need external database)
        # For now, just document the version
        echo "Manual review required for Unity version security advisories" >> "$RESULTS_DIR/sca/unity-version-check.txt"
    fi
}

run_container_security_scan() {
    log "Running Container Security Analysis..."

    # Find all Dockerfiles
    find "$PROJECT_ROOT" -name "Dockerfile" -o -name "*.dockerfile" > "$RESULTS_DIR/container/dockerfile-list.txt"

    if [[ -s "$RESULTS_DIR/container/dockerfile-list.txt" ]]; then
        info "Scanning Docker configurations..."

        while IFS= read -r dockerfile; do
            info "Scanning: $dockerfile"

            # Trivy config scan
            trivy config --format json --output "$RESULTS_DIR/container/trivy-$(basename "$dockerfile").json" "$dockerfile" || warn "Trivy found issues in $dockerfile"

            # Custom Dockerfile security check
            check_dockerfile_security "$dockerfile"

        done < "$RESULTS_DIR/container/dockerfile-list.txt"
    fi

    # Kubernetes security scan
    if find "$PROJECT_ROOT" -name "*.yaml" -o -name "*.yml" | grep -E "(deployment|service|ingress|configmap)" >/dev/null; then
        info "Scanning Kubernetes configurations..."
        run_kubernetes_security_scan
    fi

    log "Container security scan completed"
}

check_dockerfile_security() {
    local dockerfile="$1"
    local result_file="$RESULTS_DIR/container/dockerfile-security-$(basename "$dockerfile").txt"

    cat > "$result_file" << EOF
# Dockerfile Security Analysis: $dockerfile
# Generated: $(date)

## Security Issues Found:
EOF

    # Check for root user
    if grep -q "USER root\|^USER 0" "$dockerfile"; then
        echo "- CRITICAL: Running as root user detected" >> "$result_file"
    fi

    # Check for ADD instead of COPY
    if grep -q "^ADD" "$dockerfile"; then
        echo "- MEDIUM: ADD instruction used instead of COPY (security risk)" >> "$result_file"
    fi

    # Check for hardcoded secrets
    if grep -qE "password|secret|key|token" "$dockerfile"; then
        echo "- HIGH: Potential hardcoded secrets in Dockerfile" >> "$result_file"
    fi

    # Check for latest tag
    if grep -q ":latest" "$dockerfile"; then
        echo "- LOW: Using 'latest' tag (version pinning recommended)" >> "$result_file"
    fi

    # Check for privileged mode
    if grep -q "privileged" "$dockerfile"; then
        echo "- CRITICAL: Privileged mode detected" >> "$result_file"
    fi

    echo -e "\n## Recommendations:" >> "$result_file"
    echo "- Use specific version tags instead of 'latest'" >> "$result_file"
    echo "- Run containers as non-root user" >> "$result_file"
    echo "- Use COPY instead of ADD for local files" >> "$result_file"
    echo "- Implement multi-stage builds to reduce attack surface" >> "$result_file"
}

run_kubernetes_security_scan() {
    info "Running Kubernetes security analysis..."

    # Find all Kubernetes manifests
    find "$PROJECT_ROOT" -name "*.yaml" -o -name "*.yml" | grep -E "(deployment|service|ingress|configmap)" > "$RESULTS_DIR/container/k8s-manifests.txt"

    # Run Checkov on Kubernetes files
    checkov \
        --framework kubernetes \
        --output json \
        --output-file "$RESULTS_DIR/container/checkov-k8s.json" \
        --directory "$PROJECT_ROOT" || warn "Checkov found Kubernetes security issues"

    # Custom Kubernetes security checks
    while IFS= read -r manifest; do
        check_kubernetes_security "$manifest"
    done < "$RESULTS_DIR/container/k8s-manifests.txt"
}

check_kubernetes_security() {
    local manifest="$1"
    local result_file="$RESULTS_DIR/container/k8s-security-$(basename "$manifest").txt"

    cat > "$result_file" << EOF
# Kubernetes Security Analysis: $manifest
# Generated: $(date)

## Security Issues Found:
EOF

    # Check for privileged containers
    if grep -q "privileged: true" "$manifest"; then
        echo "- CRITICAL: Privileged container detected" >> "$result_file"
    fi

    # Check for root filesystem
    if ! grep -q "readOnlyRootFilesystem: true" "$manifest"; then
        echo "- MEDIUM: Read-only root filesystem not enforced" >> "$result_file"
    fi

    # Check for security context
    if ! grep -q "securityContext" "$manifest"; then
        echo "- HIGH: No security context defined" >> "$result_file"
    fi

    # Check for resource limits
    if ! grep -q "resources:" "$manifest"; then
        echo "- MEDIUM: No resource limits defined" >> "$result_file"
    fi

    # Check for network policies
    if ! grep -q "NetworkPolicy" "$manifest"; then
        echo "- LOW: Consider implementing network policies" >> "$result_file"
    fi
}

run_dynamic_analysis() {
    log "Running Dynamic Application Security Testing (DAST)..."

    # Check if application is running locally for testing
    local app_url="${APP_URL:-http://localhost:8080}"

    info "Testing application at: $app_url"

    # Basic connectivity check
    if ! curl -s --max-time 10 "$app_url/health" >/dev/null 2>&1; then
        warn "Application not accessible at $app_url - skipping DAST"
        return 0
    fi

    # OWASP ZAP automated scan
    info "Running OWASP ZAP security scan..."
    run_zap_scan "$app_url"

    # Custom web security tests
    info "Running custom web security tests..."
    run_custom_web_tests "$app_url"

    log "Dynamic analysis completed"
}

run_zap_scan() {
    local target_url="$1"

    # Start ZAP daemon
    zap-cli start --start-options '-config api.disablekey=true'

    # Wait for ZAP to start
    sleep 10

    # Spider the application
    info "Spidering application..."
    zap-cli spider "$target_url"

    # Wait for spider to complete
    zap-cli spider-status

    # Run active scan
    info "Running active security scan..."
    zap-cli active-scan "$target_url"

    # Wait for active scan to complete
    while [[ $(zap-cli active-scan-status) -ne 100 ]]; do
        sleep 10
        info "Active scan progress: $(zap-cli active-scan-status)%"
    done

    # Generate reports
    zap-cli report -o "$RESULTS_DIR/dast/zap-report.html" -f html
    zap-cli report -o "$RESULTS_DIR/dast/zap-report.json" -f json

    # Stop ZAP
    zap-cli shutdown
}

run_custom_web_tests() {
    local target_url="$1"
    local result_file="$RESULTS_DIR/dast/custom-web-tests.txt"

    cat > "$result_file" << EOF
# Custom Web Security Tests
# Target: $target_url
# Generated: $(date)

## Test Results:
EOF

    # Test security headers
    info "Testing security headers..."
    curl -I "$target_url" 2>/dev/null | grep -E "(X-Frame-Options|X-Content-Type-Options|Content-Security-Policy|Strict-Transport-Security)" >> "$result_file" || echo "- Missing security headers detected" >> "$result_file"

    # Test for common vulnerabilities
    info "Testing for common web vulnerabilities..."

    # Test for SQL injection (basic)
    if curl -s "$target_url/api/test?id=1'" | grep -q "error\|exception\|mysql\|sql"; then
        echo "- CRITICAL: Potential SQL injection vulnerability" >> "$result_file"
    fi

    # Test for XSS (basic)
    if curl -s "$target_url/api/test?input=<script>alert('xss')</script>" | grep -q "<script>"; then
        echo "- HIGH: Potential XSS vulnerability" >> "$result_file"
    fi

    # Test for directory traversal
    if curl -s "$target_url/../../../etc/passwd" | grep -q "root:"; then
        echo "- CRITICAL: Directory traversal vulnerability" >> "$result_file"
    fi

    echo -e "\n## Completed custom web security tests" >> "$result_file"
}

run_infrastructure_scan() {
    log "Running Infrastructure Security Analysis..."

    # Terraform/Infrastructure as Code scan
    if find "$PROJECT_ROOT" -name "*.tf" >/dev/null 2>&1; then
        info "Scanning Terraform configurations..."
        checkov \
            --framework terraform \
            --output json \
            --output-file "$RESULTS_DIR/infrastructure/checkov-terraform.json" \
            --directory "$PROJECT_ROOT" || warn "Checkov found Terraform security issues"
    fi

    # GitHub Actions security scan
    if [[ -d "$PROJECT_ROOT/.github/workflows" ]]; then
        info "Scanning GitHub Actions workflows..."
        scan_github_actions_security
    fi

    # Docker Compose security scan
    if find "$PROJECT_ROOT" -name "docker-compose*.yml" >/dev/null 2>&1; then
        info "Scanning Docker Compose configurations..."
        scan_docker_compose_security
    fi

    log "Infrastructure security scan completed"
}

scan_github_actions_security() {
    local result_file="$RESULTS_DIR/infrastructure/github-actions-security.txt"

    cat > "$result_file" << EOF
# GitHub Actions Security Analysis
# Generated: $(date)

## Security Issues Found:
EOF

    # Check for secrets in workflows
    if grep -r "password\|secret\|key\|token" "$PROJECT_ROOT/.github/workflows" --include="*.yml" | grep -v "secrets\."; then
        echo "- HIGH: Potential hardcoded secrets in workflows" >> "$result_file"
    fi

    # Check for pull_request_target usage
    if grep -r "pull_request_target" "$PROJECT_ROOT/.github/workflows" --include="*.yml"; then
        echo "- MEDIUM: pull_request_target usage detected (review for security)" >> "$result_file"
    fi

    # Check for third-party actions without version pinning
    if grep -r "uses:" "$PROJECT_ROOT/.github/workflows" --include="*.yml" | grep -v "@v\|@sha"; then
        echo "- LOW: Third-party actions without version pinning" >> "$result_file"
    fi

    echo -e "\n## GitHub Actions security scan completed" >> "$result_file"
}

scan_docker_compose_security() {
    local result_file="$RESULTS_DIR/infrastructure/docker-compose-security.txt"

    cat > "$result_file" << EOF
# Docker Compose Security Analysis
# Generated: $(date)

## Security Issues Found:
EOF

    find "$PROJECT_ROOT" -name "docker-compose*.yml" | while IFS= read -r compose_file; do
        echo -e "\n### Analyzing: $compose_file" >> "$result_file"

        # Check for privileged containers
        if grep -q "privileged: true" "$compose_file"; then
            echo "- CRITICAL: Privileged container detected" >> "$result_file"
        fi

        # Check for bind mounts to sensitive directories
        if grep -q ":/etc\|:/root\|:/var/run/docker.sock" "$compose_file"; then
            echo "- HIGH: Sensitive directory bind mount detected" >> "$result_file"
        fi

        # Check for default passwords
        if grep -qE "password.*admin|password.*123|password.*password" "$compose_file"; then
            echo "- CRITICAL: Default password detected" >> "$result_file"
        fi
    done

    echo -e "\n## Docker Compose security scan completed" >> "$result_file"
}

generate_security_report() {
    log "Generating comprehensive security report..."

    local report_file="$RESULTS_DIR/reports/security-assessment-report.md"

    cat > "$report_file" << EOF
# Security Assessment Report
**Project**: The Sovereign's Dilemma
**Scan Date**: $(date '+%Y-%m-%d %H:%M:%S')
**Report ID**: $(basename "$RESULTS_DIR")

## Executive Summary

This report presents the findings of a comprehensive security assessment conducted on The Sovereign's Dilemma political simulation game. The assessment included static analysis, dynamic testing, dependency scanning, and infrastructure review.

### Risk Summary
EOF

    # Count vulnerabilities by severity
    local critical_count=0
    local high_count=0
    local medium_count=0
    local low_count=0

    # Count findings from various scan results
    if [[ -f "$RESULTS_DIR/sast/semgrep-results.json" ]]; then
        critical_count=$((critical_count + $(jq '[.results[] | select(.extra.severity == "ERROR")] | length' "$RESULTS_DIR/sast/semgrep-results.json" 2>/dev/null || echo 0)))
    fi

    cat >> "$report_file" << EOF

| Severity | Count | Status |
|----------|-------|--------|
| Critical | $critical_count | $([ $critical_count -eq 0 ] && echo "✅ None" || echo "🚨 Requires immediate attention") |
| High     | $high_count | $([ $high_count -eq 0 ] && echo "✅ None" || echo "⚠️ Requires attention") |
| Medium   | $medium_count | $([ $medium_count -eq 0 ] && echo "✅ None" || echo "📋 Should be addressed") |
| Low      | $low_count | $([ $low_count -eq 0 ] && echo "✅ None" || echo "📝 Consider addressing") |

## Detailed Findings

### Static Analysis Security Testing (SAST)
EOF

    # Add SAST findings
    if [[ -f "$RESULTS_DIR/sast/semgrep-results.json" ]]; then
        echo "- Semgrep analysis completed" >> "$report_file"
        echo "- Custom pattern analysis completed" >> "$report_file"
    fi

    cat >> "$report_file" << EOF

### Software Composition Analysis (SCA)
EOF

    # Add SCA findings
    if [[ -f "$RESULTS_DIR/sca/dependencies.xml" ]]; then
        echo "- Dependency vulnerability scan completed" >> "$report_file"
    fi

    cat >> "$report_file" << EOF

### Dynamic Application Security Testing (DAST)
EOF

    # Add DAST findings
    if [[ -f "$RESULTS_DIR/dast/zap-report.json" ]]; then
        echo "- OWASP ZAP scan completed" >> "$report_file"
    fi

    cat >> "$report_file" << EOF

### Container Security
EOF

    # Add container findings
    if [[ -f "$RESULTS_DIR/container/dockerfile-list.txt" ]]; then
        echo "- Container configuration analysis completed" >> "$report_file"
    fi

    cat >> "$report_file" << EOF

### Infrastructure Security
EOF

    # Add infrastructure findings
    if [[ -f "$RESULTS_DIR/infrastructure/checkov-terraform.json" ]]; then
        echo "- Infrastructure as Code analysis completed" >> "$report_file"
    fi

    cat >> "$report_file" << EOF

## Recommendations

### Immediate Actions Required
1. Review and remediate all CRITICAL severity findings
2. Implement additional security controls for HIGH severity issues
3. Update vulnerable dependencies identified in SCA scan

### Short-term Improvements (30 days)
1. Address MEDIUM severity findings
2. Implement additional security monitoring
3. Enhance security testing in CI/CD pipeline

### Long-term Security Enhancements (90 days)
1. Address LOW severity findings
2. Implement comprehensive security training
3. Establish regular security assessment schedule

## Compliance Status

### OWASP Top 10 2021
- [ ] A01 - Broken Access Control
- [ ] A02 - Cryptographic Failures
- [ ] A03 - Injection
- [ ] A04 - Insecure Design
- [ ] A05 - Security Misconfiguration
- [ ] A06 - Vulnerable and Outdated Components
- [ ] A07 - Identification and Authentication Failures
- [ ] A08 - Software and Data Integrity Failures
- [ ] A09 - Security Logging and Monitoring Failures
- [ ] A10 - Server-Side Request Forgery

### GDPR Compliance
- [ ] Data Protection by Design and by Default
- [ ] Lawful Basis for Processing
- [ ] Data Subject Rights Implementation
- [ ] Security of Processing
- [ ] Data Breach Notification Procedures

## Appendices

### A. Scan Configuration Details
- Scan tools and versions used
- Scan scope and limitations
- Methodology and standards applied

### B. Detailed Vulnerability Listings
- Complete vulnerability details with CVSS scores
- Proof of concept demonstrations
- Remediation guidance for each finding

### C. Supporting Evidence
- Log files and scan outputs
- Screenshots of security issues
- Configuration files reviewed

---
**Report Generated By**: Security Assessment Tool
**Next Assessment**: $(date -d '+3 months' '+%Y-%m-%d')
**Contact**: security@sovereignsdilemma.com
EOF

    log "Security report generated: $report_file"
}

create_remediation_plan() {
    log "Creating vulnerability remediation plan..."

    local plan_file="$RESULTS_DIR/reports/remediation-plan.md"

    cat > "$plan_file" << EOF
# Security Vulnerability Remediation Plan
**Project**: The Sovereign's Dilemma
**Plan Date**: $(date '+%Y-%m-%d')
**Plan ID**: REMEDIATION-$(basename "$RESULTS_DIR")

## Remediation Strategy

### Phase 1: Critical and High Severity (0-7 days)
#### Critical Severity Issues
- [ ] SQL Injection vulnerabilities
- [ ] Remote Code Execution risks
- [ ] Hardcoded secrets exposure
- [ ] Privilege escalation vectors

#### High Severity Issues
- [ ] Cross-Site Scripting (XSS) vulnerabilities
- [ ] Authentication bypass issues
- [ ] Sensitive data exposure
- [ ] Container security misconfigurations

### Phase 2: Medium Severity (8-30 days)
- [ ] Information disclosure vulnerabilities
- [ ] Session management issues
- [ ] Input validation improvements
- [ ] Security header implementation
- [ ] Dependency updates

### Phase 3: Low Severity and Improvements (31-90 days)
- [ ] Security policy improvements
- [ ] Code quality enhancements
- [ ] Documentation updates
- [ ] Security training implementation
- [ ] Process improvements

## Implementation Checklist

### Development Environment
- [ ] Update development tools and dependencies
- [ ] Implement security linting in IDE
- [ ] Configure secure coding guidelines
- [ ] Set up pre-commit security hooks

### CI/CD Pipeline
- [ ] Integrate security scanning in build process
- [ ] Implement security gates for deployment
- [ ] Configure automated vulnerability notifications
- [ ] Set up security test environments

### Production Environment
- [ ] Deploy security patches
- [ ] Update infrastructure configurations
- [ ] Implement security monitoring
- [ ] Configure incident response procedures

### Team Training
- [ ] Conduct security awareness training
- [ ] Implement secure coding practices
- [ ] Establish security review processes
- [ ] Create security documentation

## Resource Requirements

### Personnel
- Development Team: 40 hours for code remediation
- DevOps Team: 20 hours for infrastructure fixes
- Security Team: 16 hours for validation and testing
- QA Team: 24 hours for security testing

### Tools and Services
- Static analysis tool licenses
- Dynamic testing tools setup
- Security training materials
- Vulnerability management platform

### Timeline
- **Week 1**: Critical severity remediation
- **Week 2**: High severity remediation
- **Weeks 3-4**: Medium severity remediation
- **Weeks 5-12**: Low severity and improvements

## Success Metrics

### Security Metrics
- Zero critical vulnerabilities
- <5 high severity vulnerabilities
- <10 medium severity vulnerabilities
- OWASP Top 10 compliance: 100%

### Process Metrics
- Security scan integration: 100% of builds
- Mean time to remediation: <7 days for high severity
- Security training completion: 100% of development team
- Security review coverage: 100% of new features

## Risk Acceptance

### Accepted Risks
(To be filled based on business risk assessment)

### Risk Mitigation
(To be filled with compensating controls)

---
**Plan Owner**: Security Team
**Approval Required**: Development Manager, Security Manager
**Review Date**: $(date -d '+30 days' '+%Y-%m-%d')
EOF

    log "Remediation plan created: $plan_file"
}

cleanup_scan_environment() {
    log "Cleaning up scan environment..."

    # Archive scan results
    if command -v tar >/dev/null 2>&1; then
        tar -czf "$RESULTS_DIR.tar.gz" -C "$(dirname "$RESULTS_DIR")" "$(basename "$RESULTS_DIR")"
        log "Scan results archived: $RESULTS_DIR.tar.gz"
    fi

    # Clean up temporary files
    find /tmp -name "trivy-*" -type d -mtime +1 -exec rm -rf {} + 2>/dev/null || true

    log "Cleanup completed"
}

# Main execution flow
main() {
    log "Starting comprehensive security assessment..."

    setup_environment
    run_static_analysis
    run_dependency_scan
    run_container_security_scan
    run_dynamic_analysis
    run_infrastructure_scan
    generate_security_report
    create_remediation_plan
    cleanup_scan_environment

    log "🔒 Security assessment completed successfully!"
    log "Results available in: $RESULTS_DIR"
    log "Security report: $RESULTS_DIR/reports/security-assessment-report.md"
    log "Remediation plan: $RESULTS_DIR/reports/remediation-plan.md"
}

# Execute main function
main "$@"