-- Database Performance Optimization for The Sovereign's Dilemma
-- SQLite optimization queries and index creation

-- Enable optimization pragmas
PRAGMA optimize;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = 10000;
PRAGMA temp_store = memory;
PRAGMA mmap_size = 268435456; -- 256MB

-- Voter data optimization indexes
CREATE INDEX IF NOT EXISTS idx_voters_political_leaning ON voters(political_leaning);
CREATE INDEX IF NOT EXISTS idx_voters_age_group ON voters(age_group);
CREATE INDEX IF NOT EXISTS idx_voters_education_level ON voters(education_level);
CREATE INDEX IF NOT EXISTS idx_voters_income_bracket ON voters(income_bracket);
CREATE INDEX IF NOT EXISTS idx_voters_region ON voters(region);

-- Composite indexes for common queries
CREATE INDEX IF NOT EXISTS idx_voters_profile ON voters(political_leaning, age_group, education_level);
CREATE INDEX IF NOT EXISTS idx_voters_demographics ON voters(age_group, income_bracket, region);
CREATE INDEX IF NOT EXISTS idx_voters_engagement ON voters(political_engagement, last_activity);

-- Political events optimization
CREATE INDEX IF NOT EXISTS idx_events_timestamp ON political_events(event_timestamp);
CREATE INDEX IF NOT EXISTS idx_events_type ON political_events(event_type);
CREATE INDEX IF NOT EXISTS idx_events_impact ON political_events(impact_score);
CREATE INDEX IF NOT EXISTS idx_events_active ON political_events(is_active, event_timestamp);

-- AI interactions optimization
CREATE INDEX IF NOT EXISTS idx_ai_requests_timestamp ON ai_requests(request_timestamp);
CREATE INDEX IF NOT EXISTS idx_ai_requests_voter ON ai_requests(voter_id);
CREATE INDEX IF NOT EXISTS idx_ai_requests_status ON ai_requests(status);
CREATE INDEX IF NOT EXISTS idx_ai_requests_response_time ON ai_requests(response_time_ms);

-- Game sessions optimization
CREATE INDEX IF NOT EXISTS idx_sessions_start_time ON game_sessions(start_time);
CREATE INDEX IF NOT EXISTS idx_sessions_duration ON game_sessions(duration_minutes);
CREATE INDEX IF NOT EXISTS idx_sessions_user ON game_sessions(user_id);

-- Performance monitoring indexes
CREATE INDEX IF NOT EXISTS idx_performance_timestamp ON performance_metrics(measurement_timestamp);
CREATE INDEX IF NOT EXISTS idx_performance_metric_type ON performance_metrics(metric_type);

-- Covering indexes for common SELECT operations
CREATE INDEX IF NOT EXISTS idx_voters_summary
ON voters(voter_id, political_leaning, age_group, engagement_score, last_activity);

CREATE INDEX IF NOT EXISTS idx_events_summary
ON political_events(event_id, event_type, event_timestamp, impact_score, description);

-- Analyze tables for query planner optimization
ANALYZE voters;
ANALYZE political_events;
ANALYZE ai_requests;
ANALYZE game_sessions;
ANALYZE performance_metrics;

-- Create views for common queries
CREATE VIEW IF NOT EXISTS voter_demographics_summary AS
SELECT
    political_leaning,
    age_group,
    education_level,
    COUNT(*) as voter_count,
    AVG(engagement_score) as avg_engagement
FROM voters
WHERE is_active = 1
GROUP BY political_leaning, age_group, education_level;

CREATE VIEW IF NOT EXISTS recent_political_events AS
SELECT
    event_id,
    event_type,
    event_timestamp,
    impact_score,
    description
FROM political_events
WHERE event_timestamp > datetime('now', '-7 days')
ORDER BY event_timestamp DESC;

CREATE VIEW IF NOT EXISTS ai_performance_summary AS
SELECT
    DATE(request_timestamp) as request_date,
    COUNT(*) as total_requests,
    AVG(response_time_ms) as avg_response_time,
    COUNT(CASE WHEN status = 'success' THEN 1 END) as successful_requests,
    COUNT(CASE WHEN status = 'error' THEN 1 END) as failed_requests
FROM ai_requests
GROUP BY DATE(request_timestamp)
ORDER BY request_date DESC;
