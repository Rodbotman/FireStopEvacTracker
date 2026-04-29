# Quick Start: Deploy to DigitalOcean

## TL;DR - Cheapest Setup ($6/month)

1. **Create Droplet on DigitalOcean:**
   - Plan: $6/month (Ubuntu 22.04 LTS)
   - Choose SSH key auth
   - Copy the IP address

2. **SSH into your server:**

   ```bash
   ssh root@YOUR_DROPLET_IP
   ```

3. **Run these commands:**

   ```bash
   # Update and install Docker
   apt update && apt upgrade -y
   apt install docker.io docker-compose -y

   # Clone this repo
   mkdir -p /var/www/firestop
   cd /var/www/firestop
   git clone https://github.com/YOUR_REPO_URL .

   # Start the app
   docker-compose up -d

   # View logs
   docker-compose logs -f app
   ```

4. **Access your app:**

   ```
   http://YOUR_DROPLET_IP
   ```

5. **Login with demo account:**
   - Username: `admin`
   - Password: `admin123`

---

## Add a Domain & SSL (Free)

```bash
# Install Certbot
apt install certbot python3-certbot-nginx nginx -y

# Setup Nginx reverse proxy (copy from DEPLOYMENT.md)
# Then get free SSL certificate:
certbot --nginx -d yourdomain.com
```

---

## Cost Summary

- **Server**: $6/month
- **SSL**: Free (Let's Encrypt)
- **Domain**: $10-15/year
- **Total**: ~$6/month + domain cost

---

## See `DEPLOYMENT.md` for:

- Detailed setup guide
- Database backups
- Security hardening
- Troubleshooting
- Alternative deployment methods

---

## Need Help?

All deployment files are included:

- `Dockerfile` - Container image
- `docker-compose.yml` - Easy deployment
- `deploy.sh` - Automated script
- `DEPLOYMENT.md` - Full guide
