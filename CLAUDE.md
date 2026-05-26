# FireStop Evac Tracker — Project Guidelines

**Single source of truth.** All operational rules (Git workflow, DB safety, deployment, Postmark, gotchas) live here. If something contradicts this file, this file wins.

## Overview

ASP.NET Core 8 Razor Pages app for managing evacuation diagram jobs. Hosted on a single 961 MB DigitalOcean droplet running both prod and staging side-by-side.

**Tech stack:** ASP.NET Core 8 · SQLite · Docker Compose v1.29.2 · Caddy (HTTPS) · ImageMagick (PDF→PNG) · Postmark (email)

---

## Environments

| | Prod | Staging |
|---|---|---|
| URL | https://firestopevacs.sureprosoftware.com.au | http://134.199.146.192:3001 |
| Droplet dir | `/var/www/firestop` | `/var/www/firestop-staging` |
| Git branch | `main` | `staging` |
| Compose file | `docker-compose.yml` | `docker-compose.yml` (both droplets use plain `docker-compose.yml`; staging's lives only on the droplet — not in the repo) |
| Service name | `app` | `app` |
| Container | `firestop_app_1` | `firestop-staging_app_1` |
| Host port | 5000 (Caddy proxies 80/443 → 5000) | 3001 (no SSL) |
| Data volumes | `./data`, `./uploads`, `./dataprotection` | `./data-staging`, `./uploads-staging`, `./dataprotection-staging` |
| `.env` location | `/var/www/firestop/.env` (Postmark) | `/var/www/firestop-staging/.env` (Postmark) |

**Droplet:** `134.199.146.192` (root, SSH key `~/.ssh/id_ed25519`, no passphrase)
**Repo:** https://github.com/Rodbotman/FireStopEvacTracker.git
**Specs:** 961 MB RAM, 24 GB disk, 2 GB swap at `/swapfile`

---

## 🚨 Database Safety (CRITICAL)

**Rule:** Backup prod DB before every prod deploy. No exceptions.

**Procedure (from local repo root):**
```bash
bash ./backup-db.sh
# Pulls /var/www/firestop/data/firestop_evac_tracker.db via scp to ./backups/
# Keeps last 7 backups; deletes older ones automatically
```

Backups are **manual only** — no cron on droplet, no DO snapshots assumed. Verify the backup file exists locally before continuing the deploy.

**Never:**
- Delete prod DB without explicit user approval
- Overwrite prod data without a fresh backup
- Apply staging-only fixes directly to prod DB
- Run `EnsureDeleted` / destructive EF migrations on prod

**Restore:** `bash ./restore-db.sh <backup-file>` (script exists; verify path).

---

## Git Workflow

**Rule:** Develop locally on the `staging` branch. Push to staging. Test. **Ask the user before merging to `main`.** Only then deploy prod.

### Standard cycle
1. `git checkout staging` (be on staging locally — never main during dev cycles)
2. Make changes, commit on `staging`
3. `git push origin staging` → triggers / unblocks staging deploy
4. Deploy staging (see below), ask user to test on http://134.199.146.192:3001
5. **Wait for user sign-off.** Don't merge to main yet.
6. After user says "merge to main" / "deploy prod" / equivalent: `git push origin staging:main`
7. Run prod deploy

**Why:** `main` is what prod pulls from. Keeping work on `staging` until sign-off means `main` only ever contains tested, approved code. If you push to `main` prematurely, the next prod deploy silently ships untested code.

**If you accidentally commit on `main`:** `git branch -f staging main` then `git reset --hard HEAD~N` on `main` (work is preserved on `staging`).

### Never
- Push directly to `main` during a dev/test cycle
- `git push origin main main:staging` (the anti-pattern — pushes untested code to main first)
- Force-push to `main` without explicit user approval
- Skip hooks (`--no-verify`) unless explicitly asked

---

## Deployment — Staging

```bash
# On droplet
cd /var/www/firestop-staging
git pull origin staging

# Pre-remove container (docker-compose v1 recreate bug — see Gotcha #2)
docker ps -a --filter "name=firestop-staging" -q | xargs -r docker rm -f

# Rebuild and start
docker-compose up -d --build

# Verify
docker-compose logs app | tail -20
curl -sI http://localhost:3001/ | head -1
```

**Test URL:** http://134.199.146.192:3001

---

## Deployment — Production

**Pre-requisites (in order, no skipping):**
1. Staging deployed and signed off by user
2. `staging` merged into `main` on user's request (not before)
3. Prod DB backed up locally (`bash ./backup-db.sh`)

**Procedure:**
```bash
# 0. Backup prod DB locally first
bash ./backup-db.sh
ls -lh ./backups/firestop_evac_tracker_*.db | tail -1  # verify backup exists

# 1. On droplet — stop staging to free RAM (Gotcha #1)
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "cd /var/www/firestop-staging && docker-compose stop"

# 2. Pull main on prod dir
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "cd /var/www/firestop && git fetch origin main && git checkout main && git pull origin main"

# 3. Pre-remove old container (Gotcha #2)
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "docker ps -a --filter 'name=firestop_app' -q | xargs -r docker rm -f"

# 4. Rebuild and restart
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "cd /var/www/firestop && docker-compose up -d --build app"

# 5. Verify health from outside (canonical check — Gotcha #3)
curl -sI https://firestopevacs.sureprosoftware.com.au/ | head -1

# 6. Restart staging
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "cd /var/www/firestop-staging && docker-compose start"
```

`deploy-prod.sh` automates this — but verify it matches the rules above before trusting it.

### Prod Deploy Gotchas (3 known failure modes)

**Gotcha #1 — RAM exhaustion.** 961 MB total. Two .NET containers + `dotnet publish` peak (600–900 MB) = swap-thrash, SSH banner timeout, droplet appears frozen. **Always stop staging before rebuilding prod.** Restart staging after prod is healthy.

**Gotcha #2 — docker-compose v1 recreate bug.** v1.29.2 has two recreate bugs:
- Image SHA change → prompts "Continue? [yN]" and aborts on non-interactive stdin
- Env-var-only change → crashes with `KeyError: 'ContainerConfig'` mid-recreate, leaving prod with no app container (Caddy returns 502)

**Always pre-remove the container** before `docker-compose up`. Data is safe — all volumes are bind mounts to host paths, not Docker-managed.

**Gotcha #3 — Caddy vs nginx port race.** Nginx is installed but stopped + disabled. If it auto-starts (e.g. after reboot), it grabs port 80 before Caddy and HTTPS goes down. Verify after reboots:
```bash
systemctl is-enabled nginx  # must be: disabled
systemctl status caddy      # must be: active (running)
```

**Canonical "is prod up" check:** External HTTPS probe (`curl -sI https://firestopevacs.sureprosoftware.com.au/`). Internal `curl localhost:5000` only confirms the app, not the edge.

**Sanity check:** DB SHA-256 before/after deploy should match (unless a migration is being applied):
```bash
sha256sum /var/www/firestop/data/firestop_evac_tracker.db
```

---

## Postmark / Email

Configured on **both prod and staging**. Each environment has its own `.env` file next to the compose file on the droplet (already in place — `chmod 600`, gitignored).

**Env vars** (see `.env.example`):
```
POSTMARK_SERVER_TOKEN=<token from Postmark "Server" page > API Tokens>
EMAIL_FROM_ADDRESS=noreply@sureprosoftware.com.au
EMAIL_FROM_NAME=FireStop Evac Tracker
APP_BASE_URL=https://firestopevacs.sureprosoftware.com.au  # prod
# Staging APP_BASE_URL=http://134.199.146.192:3001
```

`docker-compose.yml` reads these as `${POSTMARK_SERVER_TOKEN}` etc. and maps them to `Email__*` env vars consumed by `Services/PostmarkEmailService.cs`.

**Sender requirements:** The `EMAIL_FROM_ADDRESS` must be a verified Sender Signature on the Postmark account, and the sending domain must have DKIM/SPF configured.

**To rotate the token:**
```bash
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192
nano /var/www/firestop/.env          # update POSTMARK_SERVER_TOKEN
cd /var/www/firestop
docker ps -a --filter "name=firestop_app" -q | xargs -r docker rm -f
docker-compose up -d app             # picks up new env on recreate
```

(Same procedure on `/var/www/firestop-staging/` for staging.)

**Testing:** the app exposes `EmailController` for outbound test sends. Check `docker-compose logs app | grep -i postmark` after sending.

---

## Features

### Pen & Eraser Markup (client approval page)

Clients mark up evacuation diagrams via canvas overlay on `/JobApprove/{ShareCode}` before approving.

**Pipeline:**
1. On upload, server converts first page of PDF to PNG using ImageMagick (`PdfStorageService`).
2. Client draws on HTML5 Canvas overlay positioned over the PNG.
3. Tools: Draw (5 colors — red, blue, green, white, lighter grey `#D0D0D0`), Erase, Pan, Zoom In/Out/Reset, Undo, Clear, Save.
4. On Save, canvas is serialized to base64 PNG and POSTed to `/api/jobs/save-annotation`.
5. Stored in `JobAnnotation` table (DbSet `JobAnnotations`, mapped via `.ToTable("JobAnnotation")` — singular table name).

**Schema:** `JobAnnotation { Id, JobApprovalId, CanvasDataUrl (base64 PNG), CreatedAt }`.

**ImageMagick must be installed on droplet:** `apt-get install imagemagick` (verify with `convert --version`, expect 6.9.x+).

**Manual PNG generation** (if a PDF was uploaded before ImageMagick was installed):
```bash
cd /var/www/firestop-staging/uploads/<JOB>/
convert -density 150 "<file>.pdf[0]" "<file>.png"
```

---

## Common Tasks

```bash
# View logs
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "cd /var/www/firestop && docker-compose logs -f app"   # prod
# Staging: same with /var/www/firestop-staging

# Restart prod (no rebuild)
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "cd /var/www/firestop && docker-compose restart app"

# Check droplet health
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192 \
  "free -m; df -h /; docker stats --no-stream"

# SSH in
ssh -i ~/.ssh/id_ed25519 root@134.199.146.192
```

---

## docker-compose.yml note

Repo's `docker-compose.yml` matches prod (`5000:5000`, Caddy proxies). Staging's compose lives only on the droplet at `/var/www/firestop-staging/docker-compose.yml` (port `3001:5000`) — it's not in the repo because both branches share the same `docker-compose.yml` file. **Don't change ports in the repo's compose file without re-checking the droplet** — they must stay in sync or prod will collide with Caddy on port 80.

---

## Troubleshooting

| Symptom | Check | Fix |
|---|---|---|
| Prod returns 502 | `ssh ... curl localhost:5000` | App crashed — `docker-compose logs app` |
| Prod HTTPS down but app up | `systemctl status caddy` | Nginx grabbed port 80; `systemctl stop nginx && systemctl start caddy` |
| App won't start | `docker-compose logs app` | DB locked? OOM? Missing migration? |
| Build hangs mid-`dotnet publish` | `free -m` | RAM exhausted — stop staging, retry |
| Container recreate hangs / 502 | `docker ps -a` | Orphaned container — pre-remove, retry |
| Markup PDF not converting | `convert --version` on droplet | ImageMagick missing — `apt-get install imagemagick` |
| Email not sending | `docker-compose logs app \| grep -i postmark` | Token missing/invalid; check `.env` |

---

## Notes for Future Sessions

1. **Local branch is `staging`.** Never start a dev cycle on `main`.
2. **Backup before prod deploy** is non-negotiable.
3. **Ask before merging to `main`.** User decides when staging is signed off.
4. **Prod rebuild = stop staging + pre-remove container + backup.** Skipping any step has caused outages.
5. **Caddy owns 80/443.** Keep nginx disabled.
6. **ImageMagick** required for markup feature.
7. **Postmark** configured per-environment via `.env` next to compose file.
8. **`docker-compose.yml` ports must stay `5000:5000`** in the repo — anything else breaks prod's Caddy proxy.

Last updated: 2026-05-26
