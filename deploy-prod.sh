#!/bin/bash
# Quick production deployment script
# Backs up database, pulls latest code, rebuilds, and redeploys

set -e

SERVER_IP="134.199.146.192"
SSH_KEY="$HOME/.ssh/id_ed25519"

echo "🚀 FireStop Evac Tracker - Production Deployment"
echo "================================================"
echo ""

# Backup database locally first
echo "🔐 Step 1: Backing up production database..."
if [ ! -f "./backup-db.sh" ]; then
    echo "❌ backup-db.sh not found"
    exit 1
fi
bash ./backup-db.sh
echo ""

# Deploy to production
echo "📤 Step 2: Deploying to production..."
ssh -i "$SSH_KEY" root@$SERVER_IP "
    cd /var/www/firestop
    
    echo '📥 Pulling latest code from Git...'
    git pull origin main
    
    echo '🔨 Rebuilding Docker image...'
    docker-compose up -d --build app
    
    echo '⏳ Waiting for app to start...'
    sleep 5
    
    echo '✅ Deployment complete!'
    echo 'App Status:'
    docker-compose ps
    
    echo ''
    echo 'Recent logs:'
    docker-compose logs app | tail -20
"

echo ""
echo "✅ Production deployment complete!"
echo "🔗 Access at: http://134.199.146.192"
echo "📋 Backup saved to: ./backups/"
