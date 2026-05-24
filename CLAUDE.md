# FireStop Evac Tracker — Project Guidelines

## Overview

FireStop Evac Tracker is an ASP.NET Core Razor Pages application for managing evacuation diagram jobs. It runs on DigitalOcean (961MB RAM droplet) with separate prod and staging environments.

**Key Tech Stack:**
- ASP.NET Core 8 (Razor Pages)
- SQLite database (Docker-persisted)
- Docker Compose v1.29.2 (Python)
- Caddy reverse proxy (HTTPS termination)
- ImageMagick (PDF → PNG conversion)

---

## 🚨 CRITICAL: Production Database Safety

**MANDATORY RULE: Before every production deploy, backup the database locally.**

**Procedure:**
```bash
# 1. On droplet, backup production database
scp -i ~/.ssh/id_ed25519 root@134.199.146.192:/var/www/firestop/data/firestop_evac_tracker.db \
    ./backups/firestop_evac_tracker_$(date +%Y%m%d_%H%M%S).db

# 2. Verify backup exists locally
ls -lh ./backups/firestop_evac_tracker_*.db | tail -1

# 3. Then proceed with production deploy
```

**Why:** Production data is irreplaceable. If something goes wrong during deploy, we can restore from the backup.

**NEVER:**
- Delete production database without explicit approval
- Overwrite production without a backup
- Apply staging-only fixes to production data
- Assume there are automated backups (verify first)

---

## Deployment Workflow (MANDATORY)

### ✅ Correct Order

1. **Backup production database** (see CRITICAL section above)
2. **Develop locally** on `main` branch
3. **Push patches to `staging` branch first** (never directly to main)
4. **Deploy staging:** `cd /var/www/firestop-staging && docker-compose up -d --build`
5. **Test on staging:** http://134.199.146.192:3001
6. **After sign-off,** merge `staging` → `main` on GitHub
7. **Deploy prod:** (with RAM/container precautions — see Gotchas below)

### ❌ Never Do This

- Push untested code to `main` then deploy prod directly
- Use `docker-compose up -d` without removing old containers first
- Rebuild prod without stopping staging (RAM exhaustion)
- Enable/start Nginx (Caddy owns ports 80/443)
- Delete production database for any reason without backup & approval

**Why:** `main` branch is what prod pulls from. Staging-only work on main silently arms the next prod deploy with untested code. Branch isolation prevents this.

---

## Directory Layout (Droplet)

```
/var/www/firestop/              → main branch → prod container (port 5000)
  ├── data/                     → SQLite DB (bind-mounted)
  ├── uploads/                  → PDF and PNG files
  └── docker-compose.yml        → prod config

/var/www/firestop-staging/      → staging branch → staging container (port 3001)
  ├── data-staging/             → staging DB
  ├── uploads-staging/          → staging uploads
  └── docker-compose.yml        → staging config (port 3001)
```

**Key:** Separate directories with separate branches. Never share a working tree.

---

## Prod Rebuild Gotchas (3 Failure Modes)

### Gotcha #1: RAM Exhaustion

**Problem:** 961MB total RAM. Two .NET containers running (~300MB) + dotnet publish peak (~600–900MB) = swap thrashing.

**Symptom:** SSH banner timeout, HTTPS goes down, droplet appears frozen.

**Fix:** Always stop staging before rebuilding prod:

```bash
# On droplet
cd /var/www/firestop-staging
docker-compose stop

# Rebuild prod (in another SSH session)
cd /var/www/firestop
docker ps -a --filter "name=firestop_app" -q | xargs -r docker rm -f
docker-compose up -d --build

# Restart staging after prod is healthy
cd /var/www/firestop-staging
docker-compose start
```

**Remember:** 2GB swapfile exists at `/swapfile` but is slow. Stopping staging is the cheaper guardrail.

---

### Gotcha #2: docker-compose v1 Container Recreate Bug

**Problem:** docker-compose v1.29.2 on the droplet has two bugs when recreating containers:
- **Bug A:** Image SHA change → prompts "Continue? [yN]" and aborts under non-interactive stdin
- **Bug B:** Even env-var-only changes crash with `KeyError: 'ContainerConfig'` midway through recreate, leaving prod with **no running app container** (Caddy returns 502)

**Symptom:** Prod container down or orphaned `<hash>_firestop_app_1` exists.

**Fix:** Never rely on `docker-compose up -d` to recreate. Always pre-remove:

```bash
docker ps -a --filter "name=firestop_app" -q | xargs -r docker rm -f
docker-compose up -d app
```

Brief downtime (~5–10s) is unavoidable but bounded. Data loss is impossible (all volumes are host bind-mounts, not Docker-managed).

**Sanity check:** SHA-256 of DB before and after should match (unless DB migration):
```bash
sha256sum /var/www/firestop/data/firestop_evac_tracker.db
```

---

### Gotcha #3: Caddy vs Nginx Port Race

**Problem:** Nginx is installed but obsolete. On boot, if enabled, it wins the race for port 80 before Caddy starts. Caddy fails with `bind: address already in use`, taking HTTPS down without affecting the app itself.

**Fix:** Keep Nginx stopped and disabled:

```bash
systemctl is-enabled nginx  # Should return: disabled
systemctl status caddy      # Should return: active (running)
```

**Canonical prod health check:** External HTTPS probe (not internal `curl localhost:5000`).

---

## Deployment Commands

### Staging Deploy

```bash
# On droplet
cd /var/www/firestop-staging

# Pull staging branch
git fetch origin staging
git checkout staging

# Rebuild container
docker ps -a --filter "name=firestop-staging" -q | xargs -r docker rm -f
docker-compose up -d --build

# Verify
docker-compose logs -f app  # Ctrl-C to exit
curl http://localhost:3001/
```

**Test URL:** http://134.199.146.192:3001

---

### Prod Deploy (Full Sequence)

```bash
# 0. BACKUP PRODUCTION DATABASE FIRST (MANDATORY)
scp -i ~/.ssh/id_ed25519 root@134.199.146.192:/var/www/firestop/data/firestop_evac_tracker.db \
    ./backups/firestop_evac_tracker_$(date +%Y%m%d_%H%M%S).db
ls -lh ./backups/firestop_evac_tracker_*.db | tail -1  # Verify backup created

# 1. On droplet — stop staging to free RAM
cd /var/www/firestop-staging
docker-compose stop

# 2. Rebuild prod
cd /var/www/firestop
git fetch origin main
git checkout main
git pull origin main

# 3. Remove old container (gotcha #2)
docker ps -a --filter "name=firestop_app" -q | xargs -r docker rm -f

# 4. Rebuild and restart
docker-compose up -d --build app

# 5. Verify health
docker-compose logs app
curl https://firestopevacs.sureprosoftware.com.au/

# 6. Restart staging (if successful)
cd /var/www/firestop-staging
docker-compose start
```

**Expected:** Prod HTTPS responds, staging comes back up.

---

## Feature: Pen & Eraser Markup (Latest)

### Overview
Clients can mark up evacuation diagrams before approval.

### How It Works
1. **PDF → PNG conversion:** Server-side ImageMagick converts first page of PDF to PNG on upload
2. **Fabric.js drawing:** Client-side canvas overlay for pen, eraser, undo, clear
3. **Persistence:** Annotations saved as base64 PNG in `JobAnnotations` table

### Database
- **Table:** `JobAnnotation` (singular — maps to `JobAnnotations` DbSet with `.ToTable("JobAnnotation")`)
- **Fields:** `Id`, `JobApprovalId`, `CanvasDataUrl` (base64 PNG), `CreatedAt`
- **API:** `POST /api/jobs/save-annotation`

### Dependencies
- **ImageMagick:** Must be installed on droplet (`apt-get install imagemagick`)
  ```bash
  convert --version  # Verify: should be ImageMagick 6.9.x+
  ```

### Testing Checklist
- [ ] PDF renders as image on JobApprove page
- [ ] Pen tool draws with color selection (red/blue/green)
- [ ] Eraser removes strokes
- [ ] Undo works
- [ ] Clear button works
- [ ] Save Markup button saves to DB
- [ ] Admin can see saved annotation in Details page

---

## Common Tasks

### View Logs
```bash
cd /var/www/firestop
docker-compose logs -f app  # Prod
# or
cd /var/www/firestop-staging
docker-compose logs -f app  # Staging
```

### Restart App (Prod)
```bash
cd /var/www/firestop
docker-compose restart
```

### Check Disk/Memory
```bash
df -h          # Disk usage
free -m        # Memory usage
docker stats   # Container stats
```

### Database Backup (Manual)
```bash
cp /var/www/firestop/data/firestop_evac_tracker.db \
   /var/www/firestop/backups/backup_$(date +%s).db
```

### SSH into Droplet
```bash
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192
```

---

## Environment / Credentials

**Droplet IP:** 134.199.146.192  
**Prod URL:** https://firestopevacs.sureprosoftware.com.au  
**Staging URL:** http://134.199.146.192:3001  
**SSH Key:** ~/.ssh/id_ed25519 (no passphrase)  
**GitHub Repo:** https://github.com/Rodbotman/FireStopEvacTracker.git  
**Local Backups:** `./backups/firestop_evac_tracker_*.db`

---

## Security Checklist

- [ ] SSH key authentication enabled
- [ ] Root login disabled
- [ ] Firewall enabled (SSH 22, HTTP 80, HTTPS 443)
- [ ] SSL/HTTPS enabled (Caddy)
- [ ] Nginx is disabled
- [ ] Regular backups enabled (local and/or automated)
- [ ] Default credentials changed (if any)

---

## Troubleshooting

| Issue | Check | Fix |
|-------|-------|-----|
| Prod returns 502 | `curl localhost:5000` | App crashed; check `docker-compose logs` |
| Prod HTTPS unreachable but app runs | Caddy crashed | SSH check: `systemctl status caddy`; is Nginx enabled? |
| App won't start | `docker-compose logs app` | DB locked? OOM? Check logs. |
| Staging port conflict | `sudo ss -tulpn \| grep 3001` | Another service on 3001? Kill or reassign. |
| Docker-compose recreate hangs | `free -m` | RAM exhausted? Stop staging and retry. |

---

## Notes for Future Sessions

1. **Separate prod/staging dirs:** Never cd into one directory and switch branches. Each dir tracks its own branch.
2. **Always backup before prod deploy:** This is non-negotiable. Data loss is catastrophic.
3. **Always test staging first:** New commits belong on `staging` initially, not `main`.
4. **Prod rebuilds need precautions:** RAM stop + explicit container remove + database backup. This is not optional.
5. **ImageMagick required:** Markup feature depends on it. Verify on deploy.
6. **No Nginx:** Caddy is the reverse proxy. Nginx is disabled and should stay disabled.

---

## Related

- [DigitalOcean Deployment Guide](DEPLOYMENT.md) — generic DO setup
- [README.md](README.md) — local dev setup

Last updated: 2026-05-24
