#!/bin/bash
# Database maintenance script for The Sovereign's Dilemma

DB_PATH="${1:-../data/sovereigns_dilemma.db}"
BACKUP_DIR="${2:-../backups}"

echo "Starting database maintenance for: $DB_PATH"

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Backup database
echo "Creating backup..."
cp "$DB_PATH" "$BACKUP_DIR/sovereigns_dilemma_backup_$(date +%Y%m%d_%H%M%S).db"

# Run optimization
echo "Running database optimization..."
sqlite3 "$DB_PATH" < database_optimizer.sql

# Vacuum database
echo "Vacuuming database..."
sqlite3 "$DB_PATH" "VACUUM;"

# Update statistics
echo "Updating table statistics..."
sqlite3 "$DB_PATH" "ANALYZE;"

# Check integrity
echo "Checking database integrity..."
integrity_result=$(sqlite3 "$DB_PATH" "PRAGMA integrity_check;")
if [ "$integrity_result" = "ok" ]; then
    echo "✅ Database integrity check passed"
else
    echo "❌ Database integrity check failed: $integrity_result"
fi

# Performance test
echo "Running performance test..."
sqlite3 "$DB_PATH" << 'SQL'
.timer on
SELECT COUNT(*) FROM voters;
SELECT COUNT(*) FROM political_events WHERE event_timestamp > datetime('now', '-1 day');
SELECT political_leaning, COUNT(*) FROM voters GROUP BY political_leaning;
.timer off
SQL

echo "Database maintenance completed successfully"
