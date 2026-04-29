# DigitalOcean Deployment Guide - FireStop Evac Tracker

## Cheapest Option: $4-6/month Droplet

### Prerequisites

1. DigitalOcean account (https://digitalocean.com)
2. Git installed on your local machine
3. Docker & Docker Compose knowledge (optional but helpful)

---

## Step-by-Step Deployment

### 1. Create a DigitalOcean Droplet

**Recommended Configuration (Cheapest Viable):**

- **Plan**: Basic - $6/month (1GB RAM, 1 vCPU, 25GB SSD)
  - _Note: $4/month option has only 512MB RAM - may struggle with .NET_
- **OS**: Ubuntu 22.04 LTS (x64)
- **Region**: Choose closest to your users
- **Authentication**: SSH key (recommended over password)
- **Hostname**: `firestop-evac-tracker`

### 2. Connect to Your Droplet

```bash
# SSH into your droplet
ssh root@YOUR_DROPLET_IP

# Update system packages
apt update && apt upgrade -y

# Install Docker & Docker Compose
apt install docker.io docker-compose -y

# Add current user to docker group (optional, avoids sudo)
usermod -aG docker $USER
```

### 3. Deploy Your Application

```bash
# Create app directory
mkdir -p /var/www/firestop
cd /var/www/firestop

# Clone your repository (or upload files)
git clone https://github.com/YOUR_USERNAME/FireStopEvacTracker.git .

# Start the application
docker-compose up -d

# Check if running
docker-compose ps

# View logs
docker-compose logs -f app
```

### 4. Setup Nginx as Reverse Proxy (Optional but Recommended)

```bash
# Install Nginx
apt install nginx -y

# Create Nginx config
cat > /etc/nginx/sites-available/default << 'EOF'
server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name _;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
EOF

# Enable Nginx
systemctl restart nginx
systemctl enable nginx
```

### 5. Setup SSL/HTTPS (Free with Let's Encrypt)

```bash
# Install Certbot
apt install certbot python3-certbot-nginx -y

# Get certificate
certbot --nginx -d yourdomain.com

# Auto-renewal setup (automatic with certbot)
systemctl enable certbot.timer
```

### 6. Database Backup Setup

```bash
# Create backup script
mkdir -p /var/www/firestop/backups

cat > /var/www/firestop/backup.sh << 'EOF'
#!/bin/bash
BACKUP_DIR="/var/www/firestop/backups"
DATE=$(date +%Y%m%d_%H%M%S)
cp /var/www/firestop/data/firestop_evac_tracker.db $BACKUP_DIR/backup_$DATE.db
# Keep only last 7 backups
ls -t $BACKUP_DIR/backup_*.db | tail -n +8 | xargs rm -f
EOF

chmod +x /var/www/firestop/backup.sh

# Add to cron for daily backups
echo "0 2 * * * /var/www/firestop/backup.sh" | crontab -
```

---

## Cost Breakdown

| Service            | Cost          | Notes                            |
| ------------------ | ------------- | -------------------------------- |
| Droplet (Basic)    | $6/month      | 1GB RAM, 1 vCPU, 25GB SSD        |
| Bandwidth          | Included      | 1TB/month included               |
| Backups (Optional) | +$1.20/month  | 20% of droplet cost              |
| Domain (Optional)  | ~$10/year     | From any registrar               |
| **Total**          | **~$6/month** | No SSL cost (Let's Encrypt free) |

---

## Management Commands

```bash
# SSH into droplet
ssh root@YOUR_DROPLET_IP

# Go to app directory
cd /var/www/firestop

# View logs
docker-compose logs -f app

# Restart app
docker-compose restart

# Stop app
docker-compose down

# Update code and redeploy
git pull
docker-compose up -d --build

# Check disk usage
df -h

# Check memory usage
free -m

# Backup database
cp data/firestop_evac_tracker.db backups/backup_$(date +%s).db
```

---

## Alternative: DigitalOcean App Platform

**Pros:**

- Automatic deployment from Git
- Built-in SSL
- Automatic scaling
- Easier management

**Cons:**

- More expensive (~$12-20/month minimum)
- Less customization

**Cost:** $12/month base + compute units (~$0.0000148 per hour)

---

## Troubleshooting

**App won't start:**

```bash
docker-compose logs app
docker-compose down
docker-compose up -d --build
```

**Out of memory:**

- Consider upgrading to $12/month droplet (2GB RAM)
- Check: `docker stats`

**Database locked:**

- Ensure only one app instance running
- Check: `lsof /var/www/firestop/data/firestop_evac_tracker.db`

**SSL certificate issues:**

- Renew manually: `certbot renew --force-renewal`
- Check: `certbot certificates`

---

## Post-Deployment

1. **Update DNS** to point to your Droplet IP
2. **Test Login** with demo accounts (admin/admin123)
3. **Enable Firewall** in DigitalOcean dashboard:
   - Allow SSH (22)
   - Allow HTTP (80)
   - Allow HTTPS (443)
4. **Enable Backups** in DigitalOcean dashboard
5. **Monitor** via DigitalOcean dashboard or set up alerts

---

## Security Checklist

- [ ] SSH key authentication enabled
- [ ] Root login disabled
- [ ] Firewall enabled and configured
- [ ] SSL/HTTPS enabled
- [ ] Regular backups enabled
- [ ] Update password for demo users
- [ ] Add real users via `/admin` if implemented
- [ ] Enable automatic security updates

```bash
# Enable automatic security updates
apt install unattended-upgrades -y
dpkg-reconfigure -plow unattended-upgrades
```

---

## Support & Further Help

- **DigitalOcean Docs:** https://docs.digitalocean.com
- **.NET Docker:** https://hub.docker.com/_/microsoft-dotnet
- **Nginx:** https://nginx.org/en/docs/
