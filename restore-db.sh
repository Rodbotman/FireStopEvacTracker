#!/bin/bash
# Restore database from local backup

set -e

BACKUP_DIR="./backups"
SERVER_IP="134.199.146.192"
DB_PATH="/var/www/firestop/data/firestop_evac_tracker.db"
SSH_KEY="$HOME/.ssh/id_ed25519"

echo "📋 Available backups:"
echo ""
ls -lh "$BACKUP_DIR"/firestop_evac_tracker_*.db | nl

echo ""
read -p "Enter backup number to restore (or filename): " SELECTION

# Handle numeric selection
if [[ "$SELECTION" =~ ^[0-9]+$ ]]; then
    BACKUP_FILE=$(ls -1t "$BACKUP_DIR"/firestop_evac_tracker_*.db | sed -n "${SELECTION}p")
else
    BACKUP_FILE="$BACKUP_DIR/$SELECTION"
fi

if [ ! -f "$BACKUP_FILE" ]; then
    echo "❌ Backup file not found: $BACKUP_FILE"
    exit 1
fi

TIMESTAMP=$(basename "$BACKUP_FILE" | sed 's/.*_\([^.]*\)\.db/\1/')
echo ""
echo "⚠️  IMPORTANT: This will replace the production database!"
echo "   Backup: $BACKUP_FILE"
echo "   Timestamp: $TIMESTAMP"
echo ""
read -p "Type 'YES' to confirm restore (or anything else to cancel): " CONFIRM

if [ "$CONFIRM" != "YES" ]; then
    echo "❌ Restore cancelled"
    exit 0
fi

echo ""
echo "🔄 Restoring database..."

# Create backup of current prod DB before restore
ssh -i "$SSH_KEY" root@$SERVER_IP "
    cd /var/www/firestop
    cp data/firestop_evac_tracker.db data/firestop_evac_tracker_pre_restore_$(date +%Y%m%d_%H%M%S).db
    echo '✅ Pre-restore backup created'
"

# Upload backup and restore
scp -i "$SSH_KEY" "$BACKUP_FILE" "root@$SERVER_IP:/tmp/restore_db.db"

ssh -i "$SSH_KEY" root@$SERVER_IP "
    cd /var/www/firestop
    
    # Stop the app
    echo '🛑 Stopping app...'
    docker-compose stop app
    
    # Restore database
    echo '🔄 Restoring database...'
    mv /tmp/restore_db.db data/firestop_evac_tracker.db
    
    # Start the app
    echo '▶️  Starting app...'
    docker-compose up -d app
    
    echo '⏳ Waiting for app to start...'
    sleep 5
    
    echo '✅ Restore complete!'
    docker-compose logs app | tail -10
"

echo ""
echo "✅ Database restored successfully!"
echo "📋 Pre-restore backup: data/firestop_evac_tracker_pre_restore_*.db (on server)"
