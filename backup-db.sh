#!/bin/bash
# Backup production database locally before deployment
# Keeps last 7 daily backups

set -e

BACKUP_DIR="./backups"
SERVER_IP="134.199.146.192"
DB_PATH="/var/www/firestop/data/firestop_evac_tracker.db"
SSH_KEY="$HOME/.ssh/id_ed25519"

# Create backup directory if it doesn't exist
mkdir -p "$BACKUP_DIR"

# Generate timestamp
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/firestop_evac_tracker_${TIMESTAMP}.db"

echo "🔄 Backing up production database..."
echo "   From: root@$SERVER_IP:$DB_PATH"
echo "   To:   $BACKUP_FILE"

# Copy database from production server
scp -i "$SSH_KEY" "root@$SERVER_IP:$DB_PATH" "$BACKUP_FILE"

if [ -f "$BACKUP_FILE" ]; then
    SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
    echo "✅ Backup successful: $SIZE"
else
    echo "❌ Backup failed: File not created"
    exit 1
fi

# Keep only last 7 backups (delete oldest)
echo "🧹 Cleaning up old backups (keeping last 7)..."
BACKUP_COUNT=$(ls -1 "$BACKUP_DIR"/firestop_evac_tracker_*.db 2>/dev/null | wc -l)

if [ "$BACKUP_COUNT" -gt 7 ]; then
    # Delete oldest backups, keep 7
    ls -1t "$BACKUP_DIR"/firestop_evac_tracker_*.db | tail -n +8 | xargs rm -f
    echo "🗑️  Removed $(($BACKUP_COUNT - 7)) old backup(s)"
fi

echo "📦 Recent backups:"
ls -lh "$BACKUP_DIR"/firestop_evac_tracker_*.db | tail -5
