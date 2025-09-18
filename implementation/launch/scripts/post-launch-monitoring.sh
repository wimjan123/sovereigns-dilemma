#!/bin/bash

# Post-Launch Monitoring for The Sovereign's Dilemma
# Part of Phase 4.8: Launch Preparation Framework
# Comprehensive post-launch metrics collection and alerting

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LAUNCH_DIR="$(dirname "$SCRIPT_DIR")"
LOG_FILE="$LAUNCH_DIR/logs/post-launch-$(date +%Y%m%d_%H%M%S).log"
METRICS_DIR="$LAUNCH_DIR/metrics"

# Create directories
mkdir -p "$LAUNCH_DIR/logs"
mkdir -p "$METRICS_DIR"
mkdir -p "$LAUNCH_DIR/alerts"
mkdir -p "$LAUNCH_DIR/reports"

# Configuration
MONITORING_INTERVAL="${MONITORING_INTERVAL:-300}"  # 5 minutes
ALERT_COOLDOWN="${ALERT_COOLDOWN:-1800}"           # 30 minutes
STEAM_APP_ID="${STEAM_APP_ID:-your_app_id}"
ITCH_USERNAME="${ITCH_USERNAME:-your_username}"
ITCH_GAME="${ITCH_GAME:-the-sovereigns-dilemma}"
DISCORD_WEBHOOK="${DISCORD_WEBHOOK:-}"
SLACK_WEBHOOK="${SLACK_WEBHOOK:-}"

# Thresholds
MAX_CPU_USAGE=80
MAX_MEMORY_USAGE=85
MIN_DISK_SPACE=10  # GB
MAX_ERROR_RATE=5   # percentage
MIN_SUCCESS_RATE=95 # percentage

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
    log "INFO: $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
    log "SUCCESS: $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
    log "WARNING: $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
    log "ERROR: $1"
}

print_alert() {
    echo -e "${PURPLE}[ALERT]${NC} $1"
    log "ALERT: $1"
}

print_critical() {
    echo -e "${CYAN}[CRITICAL]${NC} $1"
    log "CRITICAL: $1"
}

# Notification functions
send_notification() {
    local message="$1"
    local level="${2:-info}"
    local timestamp=$(date -u +%Y-%m-%dT%H:%M:%SZ)

    # Discord notification
    if [ -n "$DISCORD_WEBHOOK" ]; then
        local color=3447003  # Blue default
        case "$level" in
            "success") color=65280 ;;      # Green
            "warning") color=16776960 ;;   # Yellow
            "error") color=16711680 ;;     # Red
            "critical") color=10181046 ;;  # Purple
        esac

        curl -H "Content-Type: application/json" \
             -X POST \
             -d "{\"embeds\":[{\"title\":\"🎮 The Sovereign's Dilemma - Post-Launch\",\"description\":\"$message\",\"color\":$color,\"timestamp\":\"$timestamp\"}]}" \
             "$DISCORD_WEBHOOK" &>/dev/null || true
    fi

    # Slack notification
    if [ -n "$SLACK_WEBHOOK" ]; then
        local slack_color="good"
        case "$level" in
            "warning") slack_color="warning" ;;
            "error"|"critical") slack_color="danger" ;;
        esac

        curl -X POST -H 'Content-type: application/json' \
             --data "{\"attachments\":[{\"color\":\"$slack_color\",\"title\":\"🎮 The Sovereign's Dilemma - Post-Launch\",\"text\":\"$message\",\"ts\":$(date +%s)}]}" \
             "$SLACK_WEBHOOK" &>/dev/null || true
    fi
}

# System metrics collection
collect_system_metrics() {
    local timestamp=$(date +%s)
    local metrics_file="$METRICS_DIR/system_metrics_$(date +%Y%m%d).json"

    # CPU usage
    local cpu_usage=$(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1 | sed 's/,/./')

    # Memory usage
    local memory_info=$(free -m)
    local memory_total=$(echo "$memory_info" | awk 'NR==2{print $2}')
    local memory_used=$(echo "$memory_info" | awk 'NR==2{print $3}')
    local memory_percent=$(awk "BEGIN {printf \"%.1f\", ($memory_used/$memory_total)*100}")

    # Disk usage
    local disk_info=$(df -h / | awk 'NR==2{print $4,$5}')
    local disk_available=$(echo "$disk_info" | awk '{print $1}' | sed 's/G//')
    local disk_percent=$(echo "$disk_info" | awk '{print $2}' | cut -d'%' -f1)

    # Load average
    local load_avg=$(uptime | awk -F'load average:' '{print $2}' | awk '{print $1}' | sed 's/,//')

    # Network connectivity
    local steam_status="DOWN"
    local itch_status="DOWN"
    local website_status="DOWN"

    if curl -s --max-time 10 "https://store.steampowered.com" &>/dev/null; then
        steam_status="UP"
    fi

    if curl -s --max-time 10 "https://itch.io" &>/dev/null; then
        itch_status="UP"
    fi

    if curl -s --max-time 10 "https://httpbin.org/status/200" &>/dev/null; then
        website_status="UP"
    fi

    # Create metrics JSON
    local metrics_json=$(cat << EOF
{
  "timestamp": $timestamp,
  "datetime": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "system": {
    "cpu_usage": $cpu_usage,
    "memory_usage_percent": $memory_percent,
    "memory_used_mb": $memory_used,
    "memory_total_mb": $memory_total,
    "disk_usage_percent": $disk_percent,
    "disk_available_gb": "$disk_available",
    "load_average": $load_avg
  },
  "connectivity": {
    "steam": "$steam_status",
    "itch": "$itch_status",
    "general": "$website_status"
  }
}
EOF
)

    echo "$metrics_json" >> "$metrics_file"

    # Check thresholds and alert if necessary
    check_system_thresholds "$cpu_usage" "$memory_percent" "$disk_available" "$disk_percent"
}

# Application metrics collection
collect_app_metrics() {
    local timestamp=$(date +%s)
    local metrics_file="$METRICS_DIR/app_metrics_$(date +%Y%m%d).json"

    # Simulated application metrics (replace with actual game telemetry)
    local concurrent_players=$((RANDOM % 1000 + 100))
    local sessions_started=$((RANDOM % 50 + 20))
    local avg_session_length=$((RANDOM % 3600 + 1800))  # 30-90 minutes
    local crash_rate=$(awk "BEGIN {printf \"%.2f\", ($(($RANDOM % 500))/10000.0)}")  # 0-5%
    local performance_fps=$((RANDOM % 20 + 50))  # 50-70 FPS

    # Platform-specific metrics (simulated)
    local steam_reviews_positive=$((RANDOM % 20 + 80))  # 80-100%
    local steam_concurrent=$((concurrent_players * 70 / 100))  # 70% on Steam
    local itch_downloads=$((RANDOM % 100 + 50))
    local itch_rating=$(awk "BEGIN {printf \"%.1f\", ($(($RANDOM % 15 + 40))/10.0)}")  # 4.0-5.5

    # Create metrics JSON
    local app_metrics_json=$(cat << EOF
{
  "timestamp": $timestamp,
  "datetime": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "players": {
    "concurrent_total": $concurrent_players,
    "sessions_started_hourly": $sessions_started,
    "avg_session_length_seconds": $avg_session_length,
    "crash_rate_percent": $crash_rate
  },
  "performance": {
    "avg_fps": $performance_fps,
    "memory_usage_gb": $(awk "BEGIN {printf \"%.2f\", ($(($RANDOM % 500 + 500))/1000.0)}"),
    "load_time_seconds": $((RANDOM % 10 + 15))
  },
  "platforms": {
    "steam": {
      "concurrent_players": $steam_concurrent,
      "review_score_positive": $steam_reviews_positive,
      "total_reviews": $((RANDOM % 500 + 100))
    },
    "itch": {
      "downloads_hourly": $itch_downloads,
      "rating": $itch_rating,
      "total_ratings": $((RANDOM % 100 + 20))
    }
  }
}
EOF
)

    echo "$app_metrics_json" >> "$metrics_file"

    # Check application thresholds
    check_app_thresholds "$crash_rate" "$performance_fps" "$steam_reviews_positive"
}

# Social media metrics collection
collect_social_metrics() {
    local timestamp=$(date +%s)
    local metrics_file="$METRICS_DIR/social_metrics_$(date +%Y%m%d).json"

    # Simulated social media metrics
    local twitter_mentions=$((RANDOM % 50 + 10))
    local discord_members=$((RANDOM % 100 + 200))
    local reddit_upvotes=$((RANDOM % 200 + 50))
    local youtube_views=$((RANDOM % 5000 + 1000))

    # Sentiment analysis (simulated)
    local positive_sentiment=$((RANDOM % 30 + 60))  # 60-90%
    local neutral_sentiment=$((RANDOM % 20 + 5))    # 5-25%
    local negative_sentiment=$((100 - positive_sentiment - neutral_sentiment))

    # Create metrics JSON
    local social_metrics_json=$(cat << EOF
{
  "timestamp": $timestamp,
  "datetime": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "platforms": {
    "twitter": {
      "mentions_hourly": $twitter_mentions,
      "hashtag_usage": $((RANDOM % 20 + 5))
    },
    "discord": {
      "active_members": $discord_members,
      "messages_hourly": $((RANDOM % 100 + 20))
    },
    "reddit": {
      "upvotes": $reddit_upvotes,
      "comments": $((RANDOM % 50 + 10))
    },
    "youtube": {
      "views": $youtube_views,
      "likes": $((youtube_views * (RANDOM % 10 + 2) / 100))
    }
  },
  "sentiment": {
    "positive_percent": $positive_sentiment,
    "neutral_percent": $neutral_sentiment,
    "negative_percent": $negative_sentiment
  }
}
EOF
)

    echo "$social_metrics_json" >> "$metrics_file"

    # Check social media thresholds
    check_social_thresholds "$positive_sentiment" "$negative_sentiment"
}

# Threshold checking functions
check_system_thresholds() {
    local cpu_usage="$1"
    local memory_percent="$2"
    local disk_available="$3"
    local disk_percent="$4"

    # CPU usage check
    if (( $(echo "$cpu_usage > $MAX_CPU_USAGE" | bc -l) )); then
        send_alert "🔥 High CPU Usage: ${cpu_usage}% (threshold: ${MAX_CPU_USAGE}%)" "warning"
    fi

    # Memory usage check
    if (( $(echo "$memory_percent > $MAX_MEMORY_USAGE" | bc -l) )); then
        send_alert "🧠 High Memory Usage: ${memory_percent}% (threshold: ${MAX_MEMORY_USAGE}%)" "warning"
    fi

    # Disk space check
    local disk_gb=$(echo "$disk_available" | sed 's/[^0-9.]//g')
    if (( $(echo "$disk_gb < $MIN_DISK_SPACE" | bc -l) )); then
        send_alert "💾 Low Disk Space: ${disk_available} available (threshold: ${MIN_DISK_SPACE}GB)" "error"
    fi
}

check_app_thresholds() {
    local crash_rate="$1"
    local performance_fps="$2"
    local steam_reviews="$3"

    # Crash rate check
    if (( $(echo "$crash_rate > $MAX_ERROR_RATE" | bc -l) )); then
        send_alert "💥 High Crash Rate: ${crash_rate}% (threshold: ${MAX_ERROR_RATE}%)" "error"
    fi

    # Performance check
    if [ "$performance_fps" -lt 50 ]; then
        send_alert "🐌 Low Performance: ${performance_fps} FPS (target: 60+ FPS)" "warning"
    fi

    # Review score check
    if [ "$steam_reviews" -lt 80 ]; then
        send_alert "⭐ Low Review Score: ${steam_reviews}% positive (target: 80%+)" "warning"
    fi
}

check_social_thresholds() {
    local positive_sentiment="$1"
    local negative_sentiment="$2"

    # Sentiment checks
    if [ "$positive_sentiment" -lt 60 ]; then
        send_alert "😟 Low Positive Sentiment: ${positive_sentiment}% (target: 60%+)" "warning"
    fi

    if [ "$negative_sentiment" -gt 25 ]; then
        send_alert "😠 High Negative Sentiment: ${negative_sentiment}% (threshold: 25%)" "error"
    fi
}

# Alert management
send_alert() {
    local message="$1"
    local level="${2:-warning}"
    local alert_key=$(echo "$message" | md5sum | cut -d' ' -f1)
    local alert_file="$LAUNCH_DIR/alerts/${alert_key}.txt"
    local current_time=$(date +%s)

    # Check if alert was recently sent (cooldown)
    if [ -f "$alert_file" ]; then
        local last_alert=$(cat "$alert_file")
        local time_diff=$((current_time - last_alert))

        if [ "$time_diff" -lt "$ALERT_COOLDOWN" ]; then
            print_status "Alert suppressed (cooldown): $message"
            return
        fi
    fi

    # Send alert
    print_alert "$message"
    send_notification "$message" "$level"

    # Record alert time
    echo "$current_time" > "$alert_file"
}

# Generate daily report
generate_daily_report() {
    local report_date="${1:-$(date +%Y%m%d)}"
    local report_file="$LAUNCH_DIR/reports/daily_report_${report_date}.md"

    print_status "Generating daily report for $report_date..."

    # Aggregate metrics from the day
    local system_file="$METRICS_DIR/system_metrics_${report_date}.json"
    local app_file="$METRICS_DIR/app_metrics_${report_date}.json"
    local social_file="$METRICS_DIR/social_metrics_${report_date}.json"

    cat > "$report_file" << EOF
# The Sovereign's Dilemma - Daily Report
**Date**: $(date -d "$report_date" +%Y-%m-%d)
**Generated**: $(date '+%Y-%m-%d %H:%M:%S %Z')

## Executive Summary

### Key Metrics
$(if [ -f "$app_file" ]; then
    echo "- **Peak Concurrent Players**: $(tail -n 5 "$app_file" | jq -r '.players.concurrent_total' | sort -n | tail -1 2>/dev/null || echo "Data unavailable")"
    echo "- **Total Sessions Started**: $(tail -n 20 "$app_file" | jq -r '.players.sessions_started_hourly' | awk '{sum+=$1} END {print sum}' 2>/dev/null || echo "Data unavailable")"
    echo "- **Average Crash Rate**: $(tail -n 20 "$app_file" | jq -r '.players.crash_rate_percent' | awk '{sum+=$1} END {printf "%.2f%%", sum/NR}' 2>/dev/null || echo "Data unavailable")"
else
    echo "- Application metrics not available"
fi)

### Platform Performance
$(if [ -f "$app_file" ]; then
    echo "- **Steam Reviews**: $(tail -n 5 "$app_file" | jq -r '.platforms.steam.review_score_positive' | tail -1 2>/dev/null || echo "N/A")% positive"
    echo "- **itch.io Rating**: $(tail -n 5 "$app_file" | jq -r '.platforms.itch.rating' | tail -1 2>/dev/null || echo "N/A")/5.0"
else
    echo "- Platform metrics not available"
fi)

### System Health
$(if [ -f "$system_file" ]; then
    echo "- **Average CPU Usage**: $(tail -n 20 "$system_file" | jq -r '.system.cpu_usage' | awk '{sum+=$1} END {printf "%.1f%%", sum/NR}' 2>/dev/null || echo "N/A")"
    echo "- **Peak Memory Usage**: $(tail -n 20 "$system_file" | jq -r '.system.memory_usage_percent' | sort -n | tail -1 2>/dev/null || echo "N/A")%"
else
    echo "- System metrics not available"
fi)

### Social Media Impact
$(if [ -f "$social_file" ]; then
    echo "- **Twitter Mentions**: $(tail -n 20 "$social_file" | jq -r '.platforms.twitter.mentions_hourly' | awk '{sum+=$1} END {print sum}' 2>/dev/null || echo "N/A")"
    echo "- **Discord Members**: $(tail -n 5 "$social_file" | jq -r '.platforms.discord.active_members' | tail -1 2>/dev/null || echo "N/A")"
    echo "- **Sentiment**: $(tail -n 10 "$social_file" | jq -r '.sentiment.positive_percent' | awk '{sum+=$1} END {printf "%.0f%%", sum/NR}' 2>/dev/null || echo "N/A") positive"
else
    echo "- Social media metrics not available"
fi)

## Detailed Analysis

### Performance Trends
$(if [ -f "$app_file" ]; then
    echo "Application performance has been $([ $(tail -n 5 "$app_file" | jq -r '.performance.avg_fps' | awk '{sum+=$1} END {print (sum/NR > 55) ? "good" : "concerning"}' 2>/dev/null) = "good" ] && echo "stable with good frame rates" || echo "variable with some performance concerns")."
    echo "Crash rate is $([ $(tail -n 10 "$app_file" | jq -r '.players.crash_rate_percent' | awk '{sum+=$1} END {print (sum/NR < 2) ? "excellent" : "acceptable"}' 2>/dev/null) = "excellent" ] && echo "excellent" || echo "within acceptable limits")."
else
    echo "Performance data not available for analysis."
fi)

### Community Response
$(if [ -f "$social_file" ]; then
    echo "Community sentiment is $(tail -n 5 "$social_file" | jq -r '.sentiment.positive_percent' | awk '{print ($1 > 70) ? "very positive" : ($1 > 60) ? "positive" : "mixed"}' | tail -1 2>/dev/null || echo "unknown")."
    echo "Social media engagement is $([ $(tail -n 10 "$social_file" | jq -r '.platforms.twitter.mentions_hourly' | awk '{sum+=$1} END {print sum}' 2>/dev/null || echo 0) -gt 200 ] && echo "high" || echo "moderate")."
else
    echo "Community response data not available."
fi)

## Action Items

### Immediate (Next 24 Hours)
- Monitor performance metrics for stability
- Respond to community feedback on Discord and Steam
- Track download/purchase velocity
- Address any technical issues promptly

### Short-term (Next Week)
- Analyze player behavior patterns
- Plan content updates based on feedback
- Optimize marketing based on social media performance
- Prepare performance improvements if needed

### Long-term (Next Month)
- Evaluate platform-specific strategies
- Plan major feature updates
- Assess community growth initiatives
- Review monetization performance

---

**Report Generated**: $(date '+%Y-%m-%d %H:%M:%S %Z')
**Next Report**: $(date -d "+1 day" '+%Y-%m-%d')

*For detailed metrics, see individual metric files in the metrics/ directory.*
EOF

    print_success "Daily report generated: $report_file"
    send_notification "📊 Daily report generated for $(date -d "$report_date" +%Y-%m-%d). Check reports/ directory for details." "info"
}

# Health check
health_check() {
    print_status "Performing health check..."

    local issues=0

    # Check log file
    if [ ! -f "$LOG_FILE" ]; then
        print_warning "Log file not accessible"
        ((issues++))
    fi

    # Check metrics directory
    if [ ! -d "$METRICS_DIR" ]; then
        print_warning "Metrics directory not found"
        mkdir -p "$METRICS_DIR"
    fi

    # Check disk space
    local available_space=$(df -BG / | awk 'NR==2{print $4}' | sed 's/G//')
    if [ "$available_space" -lt 5 ]; then
        print_warning "Low disk space: ${available_space}GB available"
        ((issues++))
    fi

    # Check connectivity
    if ! curl -s --max-time 10 "https://httpbin.org/status/200" &>/dev/null; then
        print_warning "Internet connectivity issues"
        ((issues++))
    fi

    if [ "$issues" -eq 0 ]; then
        print_success "Health check passed"
        return 0
    else
        print_warning "Health check found $issues issues"
        return 1
    fi
}

# Main monitoring loop
monitoring_loop() {
    print_status "Starting post-launch monitoring..."
    print_status "Monitoring interval: ${MONITORING_INTERVAL} seconds"
    print_status "Alert cooldown: ${ALERT_COOLDOWN} seconds"

    send_notification "📊 Post-launch monitoring started for The Sovereign's Dilemma" "info"

    local loop_count=0
    local daily_report_generated=false

    while true; do
        ((loop_count++))

        print_status "Monitoring cycle $loop_count started"

        # Health check every 10 cycles
        if [ $((loop_count % 10)) -eq 0 ]; then
            health_check
        fi

        # Collect metrics
        collect_system_metrics
        collect_app_metrics
        collect_social_metrics

        # Generate daily report at midnight or first run of the day
        local current_date=$(date +%Y%m%d)
        local current_hour=$(date +%H)

        if [ "$current_hour" = "00" ] && [ "$daily_report_generated" = false ]; then
            generate_daily_report
            daily_report_generated=true
        elif [ "$current_hour" != "00" ]; then
            daily_report_generated=false
        fi

        print_success "Monitoring cycle $loop_count completed"

        # Wait for next cycle
        sleep "$MONITORING_INTERVAL"
    done
}

# Cleanup function
cleanup() {
    print_status "Cleaning up monitoring processes..."

    # Remove old alert files (older than 1 day)
    find "$LAUNCH_DIR/alerts" -name "*.txt" -mtime +1 -delete 2>/dev/null || true

    # Compress old log files (older than 7 days)
    find "$LAUNCH_DIR/logs" -name "*.log" -mtime +7 -exec gzip {} \; 2>/dev/null || true

    print_success "Cleanup completed"
}

# Signal handlers
trap 'print_status "Monitoring stopped by user"; cleanup; exit 0' SIGINT SIGTERM

# Help information
show_help() {
    cat << EOF
The Sovereign's Dilemma - Post-Launch Monitoring

USAGE:
    $0 [OPTIONS]

OPTIONS:
    --start            Start monitoring (default)
    --health-check     Run health check only
    --report [DATE]    Generate daily report (format: YYYYMMDD)
    --cleanup          Clean up old files
    --help             Show this help message

ENVIRONMENT VARIABLES:
    MONITORING_INTERVAL    Seconds between monitoring cycles (default: 300)
    ALERT_COOLDOWN        Seconds between duplicate alerts (default: 1800)
    STEAM_APP_ID          Your Steam application ID
    ITCH_USERNAME         Your itch.io username
    ITCH_GAME            Your itch.io game name
    DISCORD_WEBHOOK       Discord webhook URL for notifications
    SLACK_WEBHOOK         Slack webhook URL for notifications

MONITORING FEATURES:
    • System metrics (CPU, memory, disk, network)
    • Application metrics (players, performance, crashes)
    • Social media metrics (mentions, sentiment)
    • Automated alerting with cooldown
    • Daily reporting
    • Health checks

THRESHOLDS:
    • CPU Usage: ${MAX_CPU_USAGE}%
    • Memory Usage: ${MAX_MEMORY_USAGE}%
    • Disk Space: ${MIN_DISK_SPACE}GB minimum
    • Crash Rate: ${MAX_ERROR_RATE}% maximum
    • Review Score: ${MIN_SUCCESS_RATE}% minimum

For more information, see the launch preparation documentation.
EOF
}

# Main execution
case "${1:---start}" in
    --start)
        monitoring_loop
        ;;
    --health-check)
        health_check
        exit $?
        ;;
    --report)
        generate_daily_report "$2"
        ;;
    --cleanup)
        cleanup
        ;;
    --help)
        show_help
        ;;
    *)
        print_error "Unknown option: $1"
        show_help
        exit 1
        ;;
esac