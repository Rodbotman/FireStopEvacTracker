#!/bin/bash
# Quick deployment script for DigitalOcean

set -e

echo "🚀 FireStop Evac Tracker - Quick Deploy Script"
echo "=============================================="
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker not found. Installing..."
    apt update && apt install docker.io docker-compose -y
fi

# Get inputs
read -p "Enter Droplet IP or domain: " SERVER_ADDRESS
read -p "Enter your Git repository URL (leave blank to skip clone): " REPO_URL

# Backup database before deployment
echo ""
echo "🔐 Creating database backup before deployment..."
if [ -f "./backup-db.sh" ]; then
    bash ./backup-db.sh
    echo ""
else
    echo "⚠️  backup-db.sh not found. Skipping backup."
    read -p "Continue without backup? (y/n): " CONTINUE
    if [ "$CONTINUE" != "y" ]; then
        exit 1
    fi
    echo ""
fi

# Connect to server
if [ ! -z "$REPO_URL" ]; then
    echo "📥 Cloning repository to server..."
    ssh root@$SERVER_ADDRESS "
        mkdir -p /var/www/firestop
        cd /var/www/firestop
        git clone $REPO_URL .
        docker-compose up -d
        echo '✅ Deployment complete!'
        echo 'Access your app at: http://$SERVER_ADDRESS'
    "
else
    echo "📤 Upload your code to /var/www/firestop on the server"
    echo "Then run: docker-compose up -d"
fi

echo ""
echo "Next steps:"
echo "1. SSH into server: ssh root@$SERVER_ADDRESS"
echo "2. Check status: cd /var/www/firestop && docker-compose ps"
echo "3. View logs: docker-compose logs -f app"
echo "4. Setup domain and SSL with: certbot --nginx -d yourdomain.com"
