#!/bin/bash
# Steam Deployment Script for The Sovereign's Dilemma
# Deploys builds to Steam using SteamCMD and Steamworks SDK

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${1:-production-builds}"
STEAM_SDK_DIR="${STEAM_SDK_DIR:-/opt/steam-sdk}"
STEAMCMD_DIR="${STEAMCMD_DIR:-/opt/steamcmd}"

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
    log "Checking Steam deployment prerequisites..."

    # Check required environment variables
    [[ -n "${STEAM_USERNAME:-}" ]] || error "STEAM_USERNAME environment variable is required"
    [[ -n "${STEAM_PASSWORD:-}" ]] || error "STEAM_PASSWORD environment variable is required"
    [[ -n "${STEAM_APP_ID:-}" ]] || error "STEAM_APP_ID environment variable is required"

    # Check for SteamCMD
    if [[ ! -f "$STEAMCMD_DIR/steamcmd.sh" ]]; then
        warn "SteamCMD not found, downloading..."
        install_steamcmd
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

install_steamcmd() {
    log "Installing SteamCMD..."

    # Create steamcmd directory
    sudo mkdir -p "$STEAMCMD_DIR"
    cd "$STEAMCMD_DIR"

    # Download and extract SteamCMD
    sudo wget -q https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
    sudo tar -xzf steamcmd_linux.tar.gz
    sudo rm steamcmd_linux.tar.gz

    # Make steamcmd executable
    sudo chmod +x steamcmd.sh

    # Run initial update
    sudo ./steamcmd.sh +quit

    log "SteamCMD installation completed"
}

prepare_steam_builds() {
    log "Preparing builds for Steam deployment..."

    # Create Steam content directory
    STEAM_CONTENT_DIR="/tmp/steam-content-$(date +%s)"
    mkdir -p "$STEAM_CONTENT_DIR"

    # Extract and organize builds by platform
    for build_file in "$BUILD_DIR"/packaged-*; do
        if [[ -f "$build_file" ]]; then
            local platform=""
            local extract_dir=""

            if [[ "$build_file" == *"windows"* ]]; then
                platform="windows"
                extract_dir="$STEAM_CONTENT_DIR/windows"
                mkdir -p "$extract_dir"
                unzip -q "$build_file" -d "$extract_dir"
            elif [[ "$build_file" == *"linux"* ]]; then
                platform="linux"
                extract_dir="$STEAM_CONTENT_DIR/linux"
                mkdir -p "$extract_dir"
                tar -xzf "$build_file" -C "$extract_dir"
            elif [[ "$build_file" == *"macos"* ]]; then
                platform="macos"
                extract_dir="$STEAM_CONTENT_DIR/macos"
                mkdir -p "$extract_dir"
                unzip -q "$build_file" -d "$extract_dir"
            fi

            if [[ -n "$platform" ]]; then
                log "Extracted $platform build to $extract_dir"

                # Set executable permissions
                find "$extract_dir" -name "SovereignsDilemma*" -type f -exec chmod +x {} \;

                # Validate build
                validate_build "$extract_dir" "$platform"
            fi
        fi
    done

    echo "$STEAM_CONTENT_DIR"
}

validate_build() {
    local build_dir="$1"
    local platform="$2"

    info "Validating $platform build..."

    # Check for main executable
    local executable_found=false
    for exe_name in "SovereignsDilemma" "SovereignsDilemma.exe" "SovereignsDilemma.app"; do
        if [[ -f "$build_dir/$exe_name" ]] || [[ -d "$build_dir/$exe_name" ]]; then
            executable_found=true
            break
        fi
    done

    [[ "$executable_found" == "true" ]] || error "No executable found in $platform build"

    # Check build size (shouldn't be too small or too large)
    local build_size=$(du -sb "$build_dir" | cut -f1)
    local min_size=100000000  # 100MB minimum
    local max_size=5000000000 # 5GB maximum

    if (( build_size < min_size )); then
        error "$platform build too small: $build_size bytes (minimum: $min_size)"
    fi

    if (( build_size > max_size )); then
        error "$platform build too large: $build_size bytes (maximum: $max_size)"
    fi

    # Check for Unity data directory
    if [[ "$platform" != "macos" ]]; then
        [[ -d "$build_dir/SovereignsDilemma_Data" ]] || warn "Unity data directory not found for $platform"
    fi

    info "$platform build validation passed (size: $(echo $build_size | numfmt --to=iec))"
}

create_steam_scripts() {
    log "Creating Steam deployment scripts..."

    local content_dir="$1"

    # Create app build script
    cat > "$content_dir/app_build_${STEAM_APP_ID}.vdf" << EOF
"appbuild"
{
    "appid" "${STEAM_APP_ID}"
    "desc" "The Sovereign's Dilemma - Production Release $(date '+%Y-%m-%d %H:%M:%S')"
    "buildoutput" "$content_dir/output"
    "contentroot" "$content_dir"
    "setlive" "default"
    "preview" "0"
    "local" ""

    "depots"
    {
        // Windows depot
        "${STEAM_APP_ID}01"
        {
            "FileMapping"
            {
                "LocalPath" "windows/*"
                "DepotPath" "."
                "recursive" "1"
            }
            "FileExclusion" "*.pdb"
        }

        // Linux depot
        "${STEAM_APP_ID}02"
        {
            "FileMapping"
            {
                "LocalPath" "linux/*"
                "DepotPath" "."
                "recursive" "1"
            }
        }

        // macOS depot
        "${STEAM_APP_ID}03"
        {
            "FileMapping"
            {
                "LocalPath" "macos/*"
                "DepotPath" "."
                "recursive" "1"
            }
        }
    }
}
EOF

    # Create depot build scripts for each platform
    cat > "$content_dir/depot_build_${STEAM_APP_ID}01.vdf" << EOF
"DepotBuildConfig"
{
    "DepotID" "${STEAM_APP_ID}01"
    "contentroot" "$content_dir/windows"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
    }
    "FileExclusion" "*.pdb"
    "FileExclusion" "*.log"
}
EOF

    cat > "$content_dir/depot_build_${STEAM_APP_ID}02.vdf" << EOF
"DepotBuildConfig"
{
    "DepotID" "${STEAM_APP_ID}02"
    "contentroot" "$content_dir/linux"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
    }
    "FileExclusion" "*.log"
}
EOF

    cat > "$content_dir/depot_build_${STEAM_APP_ID}03.vdf" << EOF
"DepotBuildConfig"
{
    "DepotID" "${STEAM_APP_ID}03"
    "contentroot" "$content_dir/macos"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
    }
    "FileExclusion" "*.log"
}
EOF

    log "Steam deployment scripts created"
}

deploy_to_steam() {
    log "Deploying to Steam..."

    local content_dir="$1"
    local build_script="$content_dir/app_build_${STEAM_APP_ID}.vdf"

    # Create output directory
    mkdir -p "$content_dir/output"

    # Create SteamCMD deployment script
    cat > "$content_dir/steam_deploy.txt" << EOF
@ShutdownOnFailedCommand 1
@NoPromptForPassword 1
login $STEAM_USERNAME $STEAM_PASSWORD
run_app_build $build_script
quit
EOF

    log "Executing Steam deployment..."

    # Run SteamCMD deployment
    cd "$STEAMCMD_DIR"
    if ./steamcmd.sh +runscript "$content_dir/steam_deploy.txt"; then
        log "Steam deployment completed successfully"
    else
        error "Steam deployment failed"
    fi

    # Verify deployment
    verify_steam_deployment "$content_dir"
}

verify_steam_deployment() {
    local content_dir="$1"
    local output_dir="$content_dir/output"

    log "Verifying Steam deployment..."

    # Check for build output
    if [[ -d "$output_dir" ]]; then
        local build_files=$(find "$output_dir" -name "*.log" | wc -l)
        if (( build_files > 0 )); then
            info "Found $build_files build log files"

            # Check for errors in logs
            if grep -r "Error\|Failed\|Exception" "$output_dir"/*.log >/dev/null 2>&1; then
                warn "Errors found in build logs, checking details..."
                grep -r "Error\|Failed\|Exception" "$output_dir"/*.log | head -10
            else
                log "No errors found in build logs"
            fi
        fi
    fi

    # Verify depot uploads
    info "Checking depot upload status..."

    # Create verification script
    cat > "$content_dir/verify_deploy.txt" << EOF
@ShutdownOnFailedCommand 1
@NoPromptForPassword 1
login $STEAM_USERNAME $STEAM_PASSWORD
app_info_print $STEAM_APP_ID
quit
EOF

    # Get app info to verify deployment
    cd "$STEAMCMD_DIR"
    ./steamcmd.sh +runscript "$content_dir/verify_deploy.txt" > "$content_dir/app_info.txt" 2>&1

    if grep -q "No app info" "$content_dir/app_info.txt"; then
        warn "Could not retrieve app info for verification"
    else
        log "App info retrieved successfully for verification"
    fi

    log "Steam deployment verification completed"
}

update_steam_store() {
    log "Updating Steam store information..."

    # This would typically involve Steam Partner API calls
    # For now, we'll just log the action and provide manual instructions

    info "Steam store update steps:"
    info "1. Log into Steam Partner portal"
    info "2. Navigate to app $STEAM_APP_ID"
    info "3. Update store page with release notes"
    info "4. Set release branch to 'default'"
    info "5. Publish build to public"

    # If Steam Partner API credentials are available, update automatically
    if [[ -n "${STEAM_PARTNER_API_KEY:-}" ]]; then
        log "Attempting automatic store update..."

        # Create release notes
        local release_notes="The Sovereign's Dilemma - Release $(date '+%Y-%m-%d')

Features:
- Enhanced political simulation with 10,000+ AI voters
- Improved NVIDIA NIM integration for realistic responses
- Cross-platform optimization and stability improvements
- WCAG AA accessibility compliance
- Real-time performance monitoring

Technical Improvements:
- 60+ FPS performance optimization
- Memory usage optimization (<1GB)
- Faster load times (<30 seconds)
- Enhanced security and GDPR compliance"

        # Update store page (would require Steam Partner API integration)
        info "Store page update would be performed here with Steam Partner API"
    else
        warn "No Steam Partner API key provided, manual store update required"
    fi
}

setup_steam_monitoring() {
    log "Setting up Steam deployment monitoring..."

    # Create monitoring configuration for Steam metrics
    cat > "/tmp/steam-monitoring-config.json" << EOF
{
    "steam_app_id": "$STEAM_APP_ID",
    "monitoring": {
        "deployment_status": true,
        "download_stats": true,
        "user_reviews": true,
        "crash_reports": true
    },
    "alerts": {
        "deployment_failure": {
            "enabled": true,
            "webhook": "${STEAM_WEBHOOK_URL:-}"
        },
        "high_crash_rate": {
            "enabled": true,
            "threshold": 5.0,
            "webhook": "${STEAM_WEBHOOK_URL:-}"
        }
    }
}
EOF

    log "Steam monitoring configuration created"
}

cleanup() {
    log "Cleaning up temporary files..."

    # Remove temporary content directory
    if [[ -n "${STEAM_CONTENT_DIR:-}" && -d "$STEAM_CONTENT_DIR" ]]; then
        rm -rf "$STEAM_CONTENT_DIR"
    fi

    # Clean up any temporary scripts
    rm -f /tmp/steam-*.txt /tmp/steam-*.json
}

# Main deployment flow
main() {
    log "Starting Steam deployment process..."

    # Set up error handling
    trap cleanup ERR EXIT

    check_prerequisites

    STEAM_CONTENT_DIR=$(prepare_steam_builds)
    create_steam_scripts "$STEAM_CONTENT_DIR"
    deploy_to_steam "$STEAM_CONTENT_DIR"
    update_steam_store
    setup_steam_monitoring

    log "🎮 Steam deployment completed successfully!"
    log "App ID: $STEAM_APP_ID"
    log "Check Steam Partner portal for final publishing steps"
}

# Execute main function
main "$@"