# DEPLOYMENT.md — Fresh Droplet Bootstrap

> **For day-to-day deploys, read `CLAUDE.md`.** This file is only for spinning up a **new** droplet from scratch (replacement, disaster recovery, second region).

The live droplet (`134.199.146.192`) is already set up per the procedure below. Use this only if you're building another one.

---

## Target setup

The droplet ends up with:
- Ubuntu 24.04 LTS (or 22.04)
- Docker + docker-compose v1.29.2 (already known-working — see the v1 recreate gotcha in CLAUDE.md)
- **Caddy** as reverse proxy on ports 80 + 443 (HTTPS auto-cert via Let's Encrypt)
- 2 GB swapfile at `/swapfile` (961 MB RAM is too tight without it)
- ImageMagick (PDF→PNG for the markup feature)
- Two app working dirs side-by-side:
  - `/var/www/firestop` (prod, branch `main`, container exposes 5000)
  - `/var/www/firestop-staging` (staging, branch `staging`, container exposes 3001)

**Do NOT install Nginx.** Earlier versions of this doc said to — that was wrong. Nginx fights Caddy for port 80. If Nginx is already installed on a fresh image, stop and disable it (`systemctl stop nginx && systemctl disable nginx`).

---

## 1. Provision the droplet

- DigitalOcean Basic plan, 1 GB RAM minimum (we run on 961 MB — anything smaller will OOM).
- Ubuntu 24.04 LTS, region closest to users (Sydney for AU).
- SSH key auth (paste your local `~/.ssh/id_ed25519.pub` into the DO console).
- DNS: point `firestopevacs.sureprosoftware.com.au` A record at the droplet's IP. Caddy needs this resolving before it can issue certs.

## 2. Base packages + swap

```bash
ssh root@<NEW_IP>

apt update && apt upgrade -y
apt install -y docker.io docker-compose imagemagick git

# Swap: 2 GB at /swapfile
fallocate -l 2G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
free -m   # confirm Swap shows ~2048
```

## 3. Caddy (reverse proxy)

```bash
apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt update && apt install -y caddy

# Caddyfile
cat > /etc/caddy/Caddyfile << 'EOF'
firestopevacs.sureprosoftware.com.au, www.firestopevacs.sureprosoftware.com.au {
    reverse_proxy localhost:5000
    encode gzip
}
EOF

systemctl restart caddy
systemctl status caddy   # must be active (running)

# Confirm Nginx isn't competing
systemctl is-enabled nginx 2>/dev/null   # disabled (or "Failed to get unit file state" if not installed)
```

## 4. Clone repos into two working dirs

```bash
# Prod
mkdir -p /var/www/firestop && cd /var/www/firestop
git clone https://github.com/Rodbotman/FireStopEvacTracker.git .
git checkout main

# Staging
mkdir -p /var/www/firestop-staging && cd /var/www/firestop-staging
git clone https://github.com/Rodbotman/FireStopEvacTracker.git .
git checkout staging
```

## 5. docker-compose.yml on each side

The repo's `docker-compose.yml` maps port `80:5000` which **breaks Caddy** — do NOT use it directly. On each droplet dir, write a local compose file with the correct port mapping.

**Prod** — `/var/www/firestop/docker-compose.yml`:
```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/firestop_evac_tracker.db
      - TZ=Australia/Sydney
      - Email__PostmarkServerToken=${POSTMARK_SERVER_TOKEN:-}
      - Email__FromAddress=${EMAIL_FROM_ADDRESS:-}
      - Email__FromName=${EMAIL_FROM_NAME:-FireStop Evac Tracker}
      - Email__AppBaseUrl=${APP_BASE_URL:-}
    volumes:
      - ./data:/app/data
      - ./uploads:/app/wwwroot/uploads
      - ./dataprotection:/root/.aspnet/DataProtection-Keys
    restart: unless-stopped
```

**Staging** — `/var/www/firestop-staging/docker-compose.yml`:
```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "3001:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/firestop_evac_tracker.db
      - TZ=Australia/Sydney
      - Email__PostmarkServerToken=${POSTMARK_SERVER_TOKEN:-}
      - Email__FromAddress=${EMAIL_FROM_ADDRESS:-}
      - Email__FromName=${EMAIL_FROM_NAME:-FireStop Evac Tracker (staging)}
      - Email__AppBaseUrl=${APP_BASE_URL:-}
    volumes:
      - ./data-staging:/app/data
      - ./uploads-staging:/app/wwwroot/uploads
      - ./dataprotection-staging:/root/.aspnet/DataProtection-Keys
    restart: unless-stopped
```

## 6. .env files (Postmark)

```bash
# Prod
cat > /var/www/firestop/.env << 'EOF'
POSTMARK_SERVER_TOKEN=<paste-from-postmark>
EMAIL_FROM_ADDRESS=noreply@sureprosoftware.com.au
EMAIL_FROM_NAME=FireStop Evac Tracker
APP_BASE_URL=https://firestopevacs.sureprosoftware.com.au
EOF
chmod 600 /var/www/firestop/.env

# Staging — same but APP_BASE_URL=http://134.199.146.192:3001
```

The `EMAIL_FROM_ADDRESS` must be a verified Sender Signature on the Postmark account with DKIM/SPF on the domain.

## 7. First build

```bash
cd /var/www/firestop && docker-compose up -d --build app
cd /var/www/firestop-staging && docker-compose up -d --build

curl -sI http://localhost:5000/    # prod (proxied by Caddy)
curl -sI http://localhost:3001/    # staging
curl -sI https://firestopevacs.sureprosoftware.com.au/   # full HTTPS edge
```

## 8. Firewall

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 3001/tcp   # staging (no SSL)
ufw enable
```

---

## After bootstrap

Day-to-day operations (deploys, backups, troubleshooting, Postmark token rotation) are documented in `CLAUDE.md`. Stop reading this file; go there.
