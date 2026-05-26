#!/bin/bash
# Production deployment — follows CLAUDE.md "Deployment — Production" procedure.
# Run from repo root. Requires `main` to be merged & pushed from `staging` first
# (per CLAUDE.md Git Workflow — don't bypass that step).
#
# Steps (matches CLAUDE.md):
#   0. Backup prod DB locally
#   1. Stop staging container (free RAM — Gotcha #1)
#   2. Pull main on prod dir
#   3. Pre-remove old prod container (docker-compose v1 bug — Gotcha #2)
#   4. Rebuild and restart prod
#   5. Verify health from outside (Gotcha #3)
#   6. Restart staging

set -e

SERVER_IP="134.199.146.192"
SSH_KEY="$HOME/.ssh/id_ed25519"
SSH="ssh -i $SSH_KEY root@$SERVER_IP"
PROD_URL="https://firestopevacs.sureprosoftware.com.au"

echo "🚀 FireStop Evac Tracker — Production Deploy"
echo "============================================="
echo ""

# 0. Backup prod DB
echo "🔐 Step 0: Backing up prod DB..."
bash ./backup-db.sh
echo ""

# 1. Stop staging (RAM)
echo "🛑 Step 1: Stopping staging container to free RAM..."
$SSH "cd /var/www/firestop-staging && docker-compose stop"
echo "   ✓ Staging stopped"
echo ""

# 2. Pull main
echo "📥 Step 2: Pulling latest main on prod..."
$SSH "cd /var/www/firestop && git fetch origin main && git checkout main && git pull origin main"
echo ""

# 3. Pre-remove old container (docker-compose v1 recreate bug)
echo "🗑️  Step 3: Removing old prod container..."
$SSH "docker ps -a --filter 'name=firestop_app' -q | xargs -r docker rm -f"
echo "   ✓ Old container removed"
echo ""

# 4. Rebuild and restart prod
echo "🔨 Step 4: Rebuilding prod image and starting container..."
$SSH "cd /var/www/firestop && docker-compose up -d --build app"
echo ""

# Give the app a few seconds to come up
sleep 5

# 5. Verify health from outside (canonical check)
echo "🩺 Step 5: Verifying prod is reachable externally..."
HTTP_STATUS=$(curl -sI -o /dev/null -w "%{http_code}" "$PROD_URL/" || echo "000")
if [[ "$HTTP_STATUS" =~ ^(200|301|302|303)$ ]]; then
    echo "   ✓ Prod responds: HTTP $HTTP_STATUS"
else
    echo "   ❌ Prod returned HTTP $HTTP_STATUS — investigate before considering deploy successful"
    echo "      Last logs:"
    $SSH "cd /var/www/firestop && docker-compose logs app | tail -30"
fi
echo ""

# 6. Restart staging
echo "▶️  Step 6: Restarting staging..."
$SSH "cd /var/www/firestop-staging && docker-compose start"
echo "   ✓ Staging restarted"
echo ""

echo "✅ Deployment complete!"
echo "   Prod:    $PROD_URL"
echo "   Staging: http://$SERVER_IP:3001"
echo "   Backup:  ./backups/"
