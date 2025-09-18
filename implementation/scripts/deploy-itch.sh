#!/bin/bash
# itch.io Deployment Script for The Sovereign's Dilemma
# Deploys builds to itch.io using butler (itch.io command-line tool)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${1:-production-builds}"
BUTLER_VERSION="${BUTLER_VERSION:-15.21.0}"
BUTLER_DIR="${BUTLER_DIR:-/opt/butler}"

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

check_prerequisites() {
    log "Checking itch.io deployment prerequisites..."

    # Check required environment variables
    [[ -n "${ITCH_API_KEY:-}" ]] || error "ITCH_API_KEY environment variable is required"
    [[ -n "${ITCH_USER:-}" ]] || error "ITCH_USER environment variable is required"
    [[ -n "${ITCH_GAME:-}" ]] || error "ITCH_GAME environment variable is required"

    # Check for butler CLI tool
    if [[ ! -f "$BUTLER_DIR/butler" ]]; then
        warn "Butler CLI tool not found, downloading..."
        install_butler
    fi

    # Validate build directory
    [[ -d "$BUILD_DIR" ]] || error "Build directory $BUILD_DIR does not exist"

    # Check for required build files
    local found_builds=false
    for platform in windows linux macos; do
        if find "$BUILD_DIR" -name "*$platform*" -type f | grep -q .; then
            found_builds=true
            break
        fi
    done

    [[ "$found_builds" == "true" ]] || error "No build files found in $BUILD_DIR"

    log "Prerequisites check passed"
}

install_butler() {
    log "Installing butler CLI tool..."

    # Create butler directory
    sudo mkdir -p "$BUTLER_DIR"
    cd "$BUTLER_DIR"

    # Determine platform and architecture
    local platform=""
    local arch=""

    case "$(uname -s)" in
        Linux*)  platform="linux" ;;
        Darwin*) platform="darwin" ;;
        *)       error "Unsupported platform: $(uname -s)" ;;
    esac

    case "$(uname -m)" in
        x86_64) arch="amd64" ;;
        arm64)  arch="arm64" ;;
        *)      error "Unsupported architecture: $(uname -m)" ;;
    esac

    # Download butler
    local butler_url="https://broth.itch.ovh/butler/${platform}-${arch}/${BUTLER_VERSION}/archive/default"
    log "Downloading butler from: $butler_url"

    sudo wget -q -O butler.zip "$butler_url"
    sudo unzip -q butler.zip
    sudo chmod +x butler
    sudo rm butler.zip

    # Verify installation
    if ./butler --version; then
        log "Butler CLI tool installed successfully"
    else
        error "Butler installation verification failed"
    fi
}

authenticate_butler() {
    log "Authenticating with itch.io..."

    # Set up butler credentials
    export BUTLER_API_KEY="$ITCH_API_KEY"

    # Test authentication
    if "$BUTLER_DIR/butler" status >/dev/null 2>&1; then
        log "Authentication successful"
    else
        error "Authentication failed - check ITCH_API_KEY"
    fi
}

prepare_itch_builds() {
    log "Preparing builds for itch.io deployment..."

    # Create itch.io content directory
    ITCH_CONTENT_DIR="/tmp/itch-content-$(date +%s)"
    mkdir -p "$ITCH_CONTENT_DIR"

    # Extract and organize builds by platform
    for build_file in "$BUILD_DIR"/packaged-*; do
        if [[ -f "$build_file" ]]; then
            local platform=""
            local extract_dir=""
            local channel=""

            if [[ "$build_file" == *"windows"* ]]; then
                platform="windows"
                channel="windows"
                extract_dir="$ITCH_CONTENT_DIR/windows"
                mkdir -p "$extract_dir"
                unzip -q "$build_file" -d "$extract_dir"
            elif [[ "$build_file" == *"linux"* ]]; then
                platform="linux"
                channel="linux"
                extract_dir="$ITCH_CONTENT_DIR/linux"
                mkdir -p "$extract_dir"
                tar -xzf "$build_file" -C "$extract_dir"
            elif [[ "$build_file" == *"macos"* ]]; then
                platform="macos"
                channel="osx"  # itch.io uses 'osx' for macOS
                extract_dir="$ITCH_CONTENT_DIR/macos"
                mkdir -p "$extract_dir"
                unzip -q "$build_file" -d "$extract_dir"
            fi

            if [[ -n "$platform" ]]; then
                log "Extracted $platform build to $extract_dir"

                # Set executable permissions
                find "$extract_dir" -name "SovereignsDilemma*" -type f -exec chmod +x {} \;

                # Create .itch.toml file for each platform
                create_itch_config "$extract_dir" "$platform"

                # Validate build
                validate_itch_build "$extract_dir" "$platform"

                # Store channel info for deployment
                echo "$channel:$extract_dir" >> "$ITCH_CONTENT_DIR/channels.txt"
            fi
        fi
    done

    echo "$ITCH_CONTENT_DIR"
}

create_itch_config() {
    local build_dir="$1"
    local platform="$2"

    log "Creating itch.io configuration for $platform..."

    # Create .itch.toml configuration file
    cat > "$build_dir/.itch.toml" << EOF
[[actions]]
name = "Play The Sovereign's Dilemma"
path = "{{ .Itch.ExecutableName }}"
icon = "icon.ico"
scope = "profile:me"

[launch]
EOF

    # Platform-specific launch configuration
    case "$platform" in
        "windows")
            cat >> "$build_dir/.itch.toml" << EOF
[[launch.actions]]
name = "Play Game"
path = "SovereignsDilemma.exe"
scope = "profile:me"
EOF
            ;;
        "linux")
            cat >> "$build_dir/.itch.toml" << EOF
[[launch.actions]]
name = "Play Game"
path = "SovereignsDilemma"
scope = "profile:me"
EOF
            ;;
        "macos")
            # Check if it's an app bundle or standalone executable
            if [[ -d "$build_dir/SovereignsDilemma.app" ]]; then
                cat >> "$build_dir/.itch.toml" << EOF
[[launch.actions]]
name = "Play Game"
path = "SovereignsDilemma.app"
scope = "profile:me"
EOF
            else
                cat >> "$build_dir/.itch.toml" << EOF
[[launch.actions]]
name = "Play Game"
path = "SovereignsDilemma"
scope = "profile:me"
EOF
            fi
            ;;
    esac

    # Add game metadata
    cat >> "$build_dir/.itch.toml" << EOF

[game]
name = "The Sovereign's Dilemma"
author = "Political Simulation Studio"
description = "Interactive Dutch political simulation featuring 10,000 AI voters"
version = "$(date '+%Y.%m.%d')"
tags = ["political", "simulation", "strategy", "ai", "dutch"]

[build]
exclude = ["*.log", "*.tmp", "*.pdb", "Temp/", "Logs/"]
EOF

    log "itch.io configuration created for $platform"
}

validate_itch_build() {
    local build_dir="$1"
    local platform="$2"

    info "Validating $platform build for itch.io..."

    # Check for main executable
    local executable_found=false
    case "$platform" in
        "windows")
            [[ -f "$build_dir/SovereignsDilemma.exe" ]] && executable_found=true
            ;;
        "linux")
            [[ -f "$build_dir/SovereignsDilemma" ]] && executable_found=true
            ;;
        "macos")
            [[ -f "$build_dir/SovereignsDilemma" ]] || [[ -d "$build_dir/SovereignsDilemma.app" ]] && executable_found=true
            ;;
    esac

    [[ "$executable_found" == "true" ]] || error "No executable found in $platform build"

    # Check build size (itch.io has size limits)
    local build_size=$(du -sb "$build_dir" | cut -f1)
    local max_size=2147483648 # 2GB limit for itch.io

    if (( build_size > max_size )); then
        error "$platform build too large: $build_size bytes (maximum: $max_size)"
    fi

    # Check for Unity data directory (except macOS app bundles)
    if [[ "$platform" != "macos" ]] || [[ ! -d "$build_dir/SovereignsDilemma.app" ]]; then
        [[ -d "$build_dir/SovereignsDilemma_Data" ]] || warn "Unity data directory not found for $platform"
    fi

    # Validate .itch.toml file
    [[ -f "$build_dir/.itch.toml" ]] || error "Missing .itch.toml configuration file"

    info "$platform build validation passed (size: $(echo $build_size | numfmt --to=iec))"
}

deploy_to_itch() {
    log "Deploying to itch.io..."

    local content_dir="$1"
    local channels_file="$content_dir/channels.txt"

    [[ -f "$channels_file" ]] || error "Channels file not found"

    # Read channel information
    while IFS=':' read -r channel build_path; do
        log "Deploying $channel channel from $build_path..."

        # Create version tag
        local version_tag="v$(date '+%Y.%m.%d-%H%M%S')"

        # Push build to itch.io
        if "$BUTLER_DIR/butler" push "$build_path" "$ITCH_USER/$ITCH_GAME:$channel" \
           --userversion "$version_tag" \
           --verbose; then
            log "Successfully deployed $channel channel"
        else
            error "Failed to deploy $channel channel"
        fi

        # Add deployment delay to avoid rate limiting
        sleep 10

    done < "$channels_file"

    log "All builds deployed to itch.io successfully"
}

update_itch_page() {
    log "Updating itch.io game page information..."

    # Create game page metadata
    local game_description="The Sovereign's Dilemma is an interactive political simulation game featuring 10,000 AI-driven Dutch voters. Experience real-time political dynamics, create compelling content, and watch as your posts influence an entire virtual electorate.

Key Features:
• 10,000+ AI voters with realistic political behavior
• Real-time political simulation based on Dutch politics
• NVIDIA NIM integration for authentic responses
• Cross-platform support (Windows, Linux, macOS)
• Accessibility compliant (WCAG AA)
• Offline mode support

System Requirements:
• RAM: 2GB minimum, 4GB recommended
• Storage: 2GB available space
• Graphics: DirectX 11 compatible
• Network: Broadband Internet connection (for AI features)

The game requires an internet connection for full AI functionality but includes an offline mode for extended play."

    # Game metadata for API update (if itch.io API is available)
    cat > "$content_dir/game_metadata.json" << EOF
{
    "title": "The Sovereign's Dilemma",
    "short_text": "Interactive Dutch political simulation with 10,000 AI voters",
    "description": "$game_description",
    "tags": ["political", "simulation", "strategy", "ai", "dutch", "politics", "democracy"],
    "classification": "game",
    "kind": "default",
    "price": 0,
    "can_be_bought": true,
    "published": true,
    "release_status": "released",
    "min_price": 0,
    "suggested_price": 0,
    "visibility": "public",
    "platforms": {
        "windows": true,
        "linux": true,
        "osx": true
    }
}
EOF

    # Update using butler if supported
    info "Game page metadata created at $content_dir/game_metadata.json"
    info "Manual page update required through itch.io dashboard:"
    info "1. Visit https://itch.io/game/edit/$ITCH_GAME"
    info "2. Update description with generated content"
    info "3. Set appropriate tags and classification"
    info "4. Upload screenshots and promotional images"
    info "5. Configure pricing and availability"

    # If itch.io API becomes available, update automatically
    if [[ -n "${ITCH_WEBHOOK_URL:-}" ]]; then
        log "Sending deployment notification..."
        curl -X POST -H "Content-Type: application/json" \
             -d '{"text":"The Sovereigns Dilemma deployed to itch.io successfully"}' \
             "$ITCH_WEBHOOK_URL" || warn "Failed to send notification"
    fi
}

setup_itch_monitoring() {
    log "Setting up itch.io deployment monitoring..."

    # Create monitoring configuration for itch.io metrics
    cat > "/tmp/itch-monitoring-config.json" << EOF
{
    "itch_user": "$ITCH_USER",
    "itch_game": "$ITCH_GAME",
    "monitoring": {
        "download_stats": true,
        "view_stats": true,
        "rating_stats": true,
        "revenue_stats": true
    },
    "alerts": {
        "deployment_failure": {
            "enabled": true,
            "webhook": "${ITCH_WEBHOOK_URL:-}"
        },
        "download_milestone": {
            "enabled": true,
            "thresholds": [100, 500, 1000, 5000],
            "webhook": "${ITCH_WEBHOOK_URL:-}"
        }
    },
    "analytics": {
        "daily_downloads": true,
        "user_feedback": true,
        "platform_breakdown": true
    }
}
EOF

    log "itch.io monitoring configuration created"
}

verify_deployment() {
    log "Verifying itch.io deployment..."

    local content_dir="$1"

    # Check deployment status using butler
    log "Checking deployment status..."
    if "$BUTLER_DIR/butler" status "$ITCH_USER/$ITCH_GAME"; then
        log "Deployment status check completed"
    else
        warn "Could not retrieve deployment status"
    fi

    # Verify channels
    log "Verifying deployed channels..."
    local channels_file="$content_dir/channels.txt"
    while IFS=':' read -r channel build_path; do
        info "Verifying $channel channel deployment..."

        # Use butler to check channel info
        if "$BUTLER_DIR/butler" ls "$ITCH_USER/$ITCH_GAME:$channel" >/dev/null 2>&1; then
            log "$channel channel verified successfully"
        else
            warn "$channel channel verification failed"
        fi
    done < "$channels_file"

    # Generate deployment report
    create_deployment_report "$content_dir"
}

create_deployment_report() {
    local content_dir="$1"
    local report_file="$content_dir/deployment_report.md"

    log "Creating deployment report..."

    cat > "$report_file" << EOF
# itch.io Deployment Report

**Game**: The Sovereign's Dilemma
**User**: $ITCH_USER
**Game ID**: $ITCH_GAME
**Deployment Date**: $(date '+%Y-%m-%d %H:%M:%S UTC')
**Version**: v$(date '+%Y.%m.%d-%H%M%S')

## Deployed Channels

EOF

    # Add channel information
    local channels_file="$content_dir/channels.txt"
    if [[ -f "$channels_file" ]]; then
        while IFS=':' read -r channel build_path; do
            local build_size=$(du -sh "$build_path" | cut -f1)
            cat >> "$report_file" << EOF
### $channel Channel
- **Path**: $build_path
- **Size**: $build_size
- **Status**: ✅ Deployed

EOF
        done < "$channels_file"
    fi

    cat >> "$report_file" << EOF

## Game Page Information

**URL**: https://$ITCH_USER.itch.io/$ITCH_GAME
**Direct Download**: https://itch.io/download/$ITCH_GAME

## Next Steps

1. Visit the game page to verify deployment
2. Update screenshots and promotional images
3. Configure pricing and visibility settings
4. Announce release to community
5. Monitor download statistics and user feedback

## Support Information

- **Documentation**: https://docs.sovereignsdilemma.com
- **Support Email**: support@sovereignsdilemma.com
- **Community**: https://discord.gg/sovereignsdilemma

EOF

    log "Deployment report created: $report_file"
    cat "$report_file"
}

cleanup() {
    log "Cleaning up temporary files..."

    # Remove temporary content directory
    if [[ -n "${ITCH_CONTENT_DIR:-}" && -d "$ITCH_CONTENT_DIR" ]]; then
        rm -rf "$ITCH_CONTENT_DIR"
    fi

    # Clean up any temporary files
    rm -f /tmp/itch-*.json /tmp/itch-*.txt
}

# Main deployment flow
main() {
    log "Starting itch.io deployment process..."

    # Set up error handling
    trap cleanup ERR EXIT

    check_prerequisites
    authenticate_butler

    ITCH_CONTENT_DIR=$(prepare_itch_builds)
    deploy_to_itch "$ITCH_CONTENT_DIR"
    update_itch_page
    setup_itch_monitoring
    verify_deployment "$ITCH_CONTENT_DIR"

    log "🎮 itch.io deployment completed successfully!"
    log "Game page: https://$ITCH_USER.itch.io/$ITCH_GAME"
    log "Check itch.io dashboard for final publishing steps"
}

# Execute main function
main "$@"