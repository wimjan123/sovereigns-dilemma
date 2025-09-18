#!/bin/bash
# Staging Deployment Script for The Sovereign's Dilemma
# Deploys builds to staging infrastructure with health checks

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${1:-staging-builds}"
STAGING_URL="${STAGING_URL:-https://staging.sovereignsdilemma.com}"
HEALTH_CHECK_TIMEOUT=300  # 5 minutes

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date '+%Y-%m-%d %H:%M:%S')] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date '+%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date '+%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
    exit 1
}

check_prerequisites() {
    log "Checking deployment prerequisites..."

    # Check required tools
    command -v kubectl >/dev/null 2>&1 || error "kubectl is required but not installed"
    command -v docker >/dev/null 2>&1 || error "docker is required but not installed"
    command -v curl >/dev/null 2>&1 || error "curl is required but not installed"

    # Check environment variables
    [[ -n "${STAGING_REGISTRY_TOKEN:-}" ]] || error "STAGING_REGISTRY_TOKEN environment variable is required"
    [[ -n "${KUBECONFIG:-}" ]] || error "KUBECONFIG environment variable is required"

    # Validate Kubernetes connection
    kubectl cluster-info >/dev/null || error "Unable to connect to Kubernetes cluster"

    log "Prerequisites check passed"
}

backup_current_deployment() {
    log "Creating backup of current staging deployment..."

    # Create backup namespace if it doesn't exist
    kubectl create namespace staging-backup --dry-run=client -o yaml | kubectl apply -f -

    # Backup current deployment
    kubectl get deployment sovereigns-dilemma -n staging -o yaml > /tmp/staging-backup-$(date +%s).yaml 2>/dev/null || {
        warn "No existing deployment found to backup"
        return 0
    }

    # Store current image for rollback
    CURRENT_IMAGE=$(kubectl get deployment sovereigns-dilemma -n staging -o jsonpath='{.spec.template.spec.containers[0].image}' 2>/dev/null || echo "none")
    echo "$CURRENT_IMAGE" > /tmp/staging-rollback-image.txt

    log "Backup completed. Current image: $CURRENT_IMAGE"
}

prepare_build_images() {
    log "Preparing container images for deployment..."

    # Create staging namespace if it doesn't exist
    kubectl create namespace staging --dry-run=client -o yaml | kubectl apply -f -

    # Build and tag container images for each platform
    for platform_dir in "$BUILD_DIR"/*-build; do
        if [[ -d "$platform_dir" ]]; then
            platform=$(basename "$platform_dir" | sed 's/-build$//')
            log "Processing $platform build..."

            # Create Dockerfile for the build
            cat > "$platform_dir/Dockerfile" << EOF
FROM ubuntu:20.04

# Install required dependencies
RUN apt-get update && apt-get install -y \\
    libc6-dev \\
    libglu1-mesa \\
    xvfb \\
    && rm -rf /var/lib/apt/lists/*

# Copy game files
COPY . /app/
WORKDIR /app

# Make executable
RUN chmod +x SovereignsDilemma || chmod +x SovereignsDilemma.exe || true

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \\
    CMD curl -f http://localhost:8080/health || exit 1

# Expose game port
EXPOSE 8080

# Start game in server mode
CMD ["./SovereignsDilemma", "-batchmode", "-nographics", "-server", "-port", "8080"]
EOF

            # Build container image
            IMAGE_TAG="ghcr.io/sovereigns-dilemma/game-server:staging-$platform-$(date +%s)"
            docker build -t "$IMAGE_TAG" "$platform_dir"

            # Push to registry
            echo "$STAGING_REGISTRY_TOKEN" | docker login ghcr.io -u staging --password-stdin
            docker push "$IMAGE_TAG"

            echo "$IMAGE_TAG" >> /tmp/staging-images.txt
            log "Built and pushed $IMAGE_TAG"
        fi
    done
}

deploy_application() {
    log "Deploying application to staging environment..."

    # Read the main image (use Linux build for server)
    MAIN_IMAGE=$(grep "linux" /tmp/staging-images.txt | head -1)
    [[ -n "$MAIN_IMAGE" ]] || error "No Linux image found for deployment"

    log "Deploying with image: $MAIN_IMAGE"

    # Create deployment manifest
    cat > /tmp/staging-deployment.yaml << EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: sovereigns-dilemma
  namespace: staging
  labels:
    app: sovereigns-dilemma
    environment: staging
spec:
  replicas: 2
  selector:
    matchLabels:
      app: sovereigns-dilemma
  template:
    metadata:
      labels:
        app: sovereigns-dilemma
        environment: staging
    spec:
      containers:
      - name: game-server
        image: ${MAIN_IMAGE}
        ports:
        - containerPort: 8080
        env:
        - name: ENVIRONMENT
          value: "staging"
        - name: LOG_LEVEL
          value: "INFO"
        - name: NVIDIA_NIM_ENDPOINT
          value: "https://staging-nim.sovereignsdilemma.com"
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 60
          periodSeconds: 30
          timeoutSeconds: 10
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 15
          timeoutSeconds: 5
          failureThreshold: 2
        securityContext:
          allowPrivilegeEscalation: false
          runAsNonRoot: true
          runAsUser: 1000
          readOnlyRootFilesystem: true
          capabilities:
            drop:
            - ALL
---
apiVersion: v1
kind: Service
metadata:
  name: sovereigns-dilemma-service
  namespace: staging
spec:
  selector:
    app: sovereigns-dilemma
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
  type: ClusterIP
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: sovereigns-dilemma-ingress
  namespace: staging
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-staging
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-window: "1m"
spec:
  tls:
  - hosts:
    - staging.sovereignsdilemma.com
    secretName: staging-tls
  rules:
  - host: staging.sovereignsdilemma.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: sovereigns-dilemma-service
            port:
              number: 80
EOF

    # Apply deployment
    kubectl apply -f /tmp/staging-deployment.yaml

    log "Deployment manifest applied"
}

wait_for_deployment() {
    log "Waiting for deployment to be ready..."

    # Wait for deployment to be ready
    kubectl rollout status deployment/sovereigns-dilemma -n staging --timeout=300s || {
        error "Deployment failed to become ready within timeout"
    }

    # Wait for pods to be ready
    kubectl wait --for=condition=ready pod -l app=sovereigns-dilemma -n staging --timeout=300s || {
        error "Pods failed to become ready within timeout"
    }

    log "Deployment is ready"
}

run_health_checks() {
    log "Running deployment health checks..."

    # Wait for service to be responsive
    local attempts=0
    local max_attempts=30

    while [[ $attempts -lt $max_attempts ]]; do
        if curl -f -s "$STAGING_URL/health" >/dev/null 2>&1; then
            log "Health check passed"
            break
        fi

        attempts=$((attempts + 1))
        log "Health check attempt $attempts/$max_attempts failed, retrying in 10s..."
        sleep 10
    done

    if [[ $attempts -eq $max_attempts ]]; then
        error "Health checks failed after $max_attempts attempts"
    fi

    # Run additional functional tests
    log "Running functional health checks..."

    # Test API endpoints
    curl -f -s "$STAGING_URL/api/status" | jq '.status' | grep -q "ok" || error "API status check failed"
    curl -f -s "$STAGING_URL/api/metrics" >/dev/null || error "Metrics endpoint check failed"

    # Test game initialization
    response=$(curl -f -s "$STAGING_URL/api/game/init" | jq -r '.initialized')
    [[ "$response" == "true" ]] || error "Game initialization check failed"

    log "All health checks passed"
}

run_integration_tests() {
    log "Running staging integration tests..."

    # Create test configuration
    cat > /tmp/staging-test-config.json << EOF
{
    "baseUrl": "$STAGING_URL",
    "testSuite": "staging",
    "timeout": 300,
    "tests": [
        "api_connectivity",
        "game_initialization",
        "voter_simulation",
        "ai_integration",
        "performance_baseline"
    ]
}
EOF

    # Run integration test suite
    if [[ -f "$SCRIPT_DIR/run-integration-tests.py" ]]; then
        python3 "$SCRIPT_DIR/run-integration-tests.py" /tmp/staging-test-config.json || {
            error "Integration tests failed"
        }
    else
        warn "Integration test script not found, skipping detailed tests"

        # Basic smoke tests
        curl -f "$STAGING_URL/api/game/create-session" >/dev/null || error "Session creation test failed"
        curl -f "$STAGING_URL/api/voters/count" | jq '.count' | grep -q '[0-9]' || error "Voter count test failed"
    fi

    log "Integration tests completed successfully"
}

setup_monitoring() {
    log "Setting up staging monitoring..."

    # Deploy monitoring configuration
    cat > /tmp/staging-monitoring.yaml << EOF
apiVersion: v1
kind: ConfigMap
metadata:
  name: staging-monitoring-config
  namespace: staging
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
    scrape_configs:
    - job_name: 'sovereigns-dilemma-staging'
      static_configs:
      - targets: ['sovereigns-dilemma-service:80']
      metrics_path: '/metrics'
      scrape_interval: 10s
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prometheus-staging
  namespace: staging
spec:
  replicas: 1
  selector:
    matchLabels:
      app: prometheus-staging
  template:
    metadata:
      labels:
        app: prometheus-staging
    spec:
      containers:
      - name: prometheus
        image: prom/prometheus:latest
        ports:
        - containerPort: 9090
        volumeMounts:
        - name: config
          mountPath: /etc/prometheus
        command:
        - /bin/prometheus
        - --config.file=/etc/prometheus/prometheus.yml
        - --storage.tsdb.path=/prometheus/
        - --web.console.libraries=/etc/prometheus/console_libraries
        - --web.console.templates=/etc/prometheus/consoles
        - --web.enable-lifecycle
      volumes:
      - name: config
        configMap:
          name: staging-monitoring-config
EOF

    kubectl apply -f /tmp/staging-monitoring.yaml

    log "Monitoring setup completed"
}

cleanup() {
    log "Cleaning up temporary files..."
    rm -f /tmp/staging-*.yaml /tmp/staging-*.txt /tmp/staging-*.json
}

rollback_on_failure() {
    error "Deployment failed, initiating rollback..."

    if [[ -f /tmp/staging-rollback-image.txt ]]; then
        ROLLBACK_IMAGE=$(cat /tmp/staging-rollback-image.txt)
        if [[ "$ROLLBACK_IMAGE" != "none" ]]; then
            log "Rolling back to previous image: $ROLLBACK_IMAGE"
            kubectl set image deployment/sovereigns-dilemma game-server="$ROLLBACK_IMAGE" -n staging
            kubectl rollout status deployment/sovereigns-dilemma -n staging --timeout=300s
            log "Rollback completed"
        else
            warn "No previous deployment found for rollback"
        fi
    fi

    cleanup
    exit 1
}

# Main deployment flow
main() {
    log "Starting staging deployment process..."

    # Set up error handling
    trap rollback_on_failure ERR

    check_prerequisites
    backup_current_deployment
    prepare_build_images
    deploy_application
    wait_for_deployment
    run_health_checks
    run_integration_tests
    setup_monitoring

    cleanup

    log "🚀 Staging deployment completed successfully!"
    log "Application is available at: $STAGING_URL"
    log "Monitoring dashboard: $STAGING_URL/monitoring"
}

# Execute main function
main "$@"