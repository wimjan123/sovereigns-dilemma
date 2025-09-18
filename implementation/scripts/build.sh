#!/bin/bash

# Build script for The Sovereign's Dilemma
# Automates Unity build process with proper error handling and validation

set -euo pipefail

# Configuration
UNITY_VERSION="6000.0.23f1"
PROJECT_PATH="$(dirname "$0")/../Unity"
BUILD_PATH="$(dirname "$0")/../builds"
UNITY_EXECUTABLE=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Detect Unity executable
detect_unity() {
    log_info "Detecting Unity executable..."

    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        UNITY_EXECUTABLE="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux
        UNITY_EXECUTABLE="$HOME/Unity/Hub/Editor/${UNITY_VERSION}/Editor/Unity"
    elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
        # Windows
        UNITY_EXECUTABLE="C:/Program Files/Unity/Hub/Editor/${UNITY_VERSION}/Editor/Unity.exe"
    else
        log_error "Unsupported operating system: $OSTYPE"
        exit 1
    fi

    if [[ ! -f "$UNITY_EXECUTABLE" ]]; then
        log_error "Unity executable not found at: $UNITY_EXECUTABLE"
        log_error "Please install Unity $UNITY_VERSION or update the UNITY_VERSION variable"
        exit 1
    fi

    log_info "Found Unity at: $UNITY_EXECUTABLE"
}

# Validate project
validate_project() {
    log_info "Validating Unity project..."

    if [[ ! -d "$PROJECT_PATH" ]]; then
        log_error "Unity project not found at: $PROJECT_PATH"
        exit 1
    fi

    if [[ ! -f "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt" ]]; then
        log_error "Invalid Unity project: ProjectSettings/ProjectVersion.txt not found"
        exit 1
    fi

    local project_version
    project_version=$(grep "m_EditorVersion:" "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)
    log_info "Project Unity version: $project_version"

    log_info "Project validation completed"
}

# Run tests
run_tests() {
    log_info "Running Unity tests..."

    "$UNITY_EXECUTABLE" \
        -batchmode \
        -quit \
        -projectPath "$PROJECT_PATH" \
        -runTests \
        -testResults "$BUILD_PATH/test-results.xml" \
        -testPlatform EditMode \
        -logFile "$BUILD_PATH/test.log"

    local exit_code=$?

    if [[ $exit_code -eq 0 ]]; then
        log_info "All tests passed"
    else
        log_error "Tests failed with exit code: $exit_code"
        if [[ -f "$BUILD_PATH/test.log" ]]; then
            log_error "Test log:"
            cat "$BUILD_PATH/test.log"
        fi
        exit $exit_code
    fi
}

# Run performance tests
run_performance_tests() {
    log_info "Running performance tests..."

    "$UNITY_EXECUTABLE" \
        -batchmode \
        -quit \
        -projectPath "$PROJECT_PATH" \
        -runTests \
        -testResults "$BUILD_PATH/performance-results.xml" \
        -testPlatform PlayMode \
        -testCategory "Performance" \
        -logFile "$BUILD_PATH/performance.log"

    local exit_code=$?

    if [[ $exit_code -eq 0 ]]; then
        log_info "Performance tests completed"
    else
        log_warn "Performance tests failed with exit code: $exit_code"
        if [[ -f "$BUILD_PATH/performance.log" ]]; then
            log_warn "Performance test log:"
            cat "$BUILD_PATH/performance.log"
        fi
        # Don't exit on performance test failure - continue with build
    fi
}

# Build for platform
build_platform() {
    local platform=$1
    local build_target=""
    local build_name="SovereignsDilemma"

    case $platform in
        "windows")
            build_target="StandaloneWindows64"
            build_name="${build_name}.exe"
            ;;
        "linux")
            build_target="StandaloneLinux64"
            ;;
        "macos")
            build_target="StandaloneOSX"
            build_name="${build_name}.app"
            ;;
        *)
            log_error "Unsupported platform: $platform"
            exit 1
            ;;
    esac

    local output_path="$BUILD_PATH/$platform/$build_name"

    log_info "Building for $platform ($build_target)..."

    mkdir -p "$(dirname "$output_path")"

    "$UNITY_EXECUTABLE" \
        -batchmode \
        -quit \
        -projectPath "$PROJECT_PATH" \
        -buildTarget "$build_target" \
        -executeMethod "SovereignsDilemma.Build.Builder.BuildProject" \
        -customBuildPath "$output_path" \
        -logFile "$BUILD_PATH/build-$platform.log"

    local exit_code=$?

    if [[ $exit_code -eq 0 ]]; then
        log_info "Build completed for $platform: $output_path"
    else
        log_error "Build failed for $platform with exit code: $exit_code"
        if [[ -f "$BUILD_PATH/build-$platform.log" ]]; then
            log_error "Build log:"
            cat "$BUILD_PATH/build-$platform.log"
        fi
        exit $exit_code
    fi
}

# Clean build directory
clean() {
    log_info "Cleaning build directory..."
    rm -rf "$BUILD_PATH"
    mkdir -p "$BUILD_PATH"
    log_info "Build directory cleaned"
}

# Show usage
usage() {
    echo "Usage: $0 [COMMAND] [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  clean                Clean build directory"
    echo "  test                 Run Unity tests"
    echo "  perf                 Run performance tests"
    echo "  build [platform]     Build for specific platform (windows|linux|macos)"
    echo "  all                  Run tests and build for all platforms"
    echo ""
    echo "Examples:"
    echo "  $0 test              # Run tests only"
    echo "  $0 build windows     # Build for Windows"
    echo "  $0 all               # Full CI pipeline"
}

# Main execution
main() {
    local command=${1:-"usage"}

    case $command in
        "clean")
            clean
            ;;
        "test")
            detect_unity
            validate_project
            mkdir -p "$BUILD_PATH"
            run_tests
            ;;
        "perf")
            detect_unity
            validate_project
            mkdir -p "$BUILD_PATH"
            run_performance_tests
            ;;
        "build")
            local platform=${2:-""}
            if [[ -z "$platform" ]]; then
                log_error "Platform required for build command"
                usage
                exit 1
            fi
            detect_unity
            validate_project
            mkdir -p "$BUILD_PATH"
            build_platform "$platform"
            ;;
        "all")
            detect_unity
            validate_project
            clean
            run_tests
            run_performance_tests

            log_info "Building for all platforms..."
            build_platform "windows"
            build_platform "linux"
            build_platform "macos"

            log_info "All builds completed successfully!"
            ;;
        "usage"|"help"|"-h"|"--help")
            usage
            ;;
        *)
            log_error "Unknown command: $command"
            usage
            exit 1
            ;;
    esac
}

main "$@"