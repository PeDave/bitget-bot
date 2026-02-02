# VPS Setup Runbook - BitgetLab

This document provides step-by-step instructions for deploying BitgetLab on an Ubuntu 24.04 VPS running as root without Docker.

## Prerequisites

- Ubuntu 24.04 LTS VPS
- Root access
- Domain names configured (DNS A records):
  - `labotkripto.com` → VPS IP
  - `api.labotkripto.com` → VPS IP
  - `n8n.labotkripto.com` → VPS IP

## 1. Initial System Setup

```bash
# Update system
apt update && apt upgrade -y

# Install required packages
apt install -y git curl wget ufw
```

## 2. Install .NET 8 SDK

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK
apt update
apt install -y dotnet-sdk-8.0

# Verify installation
dotnet --version
```

## 3. Install Node.js 20

```bash
# Install Node.js 20 via NodeSource
curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
apt install -y nodejs

# Verify installation
node --version
npm --version
```

## 4. Install and Configure Caddy

```bash
# Install Caddy
apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt update
apt install -y caddy

# Create sites directory
mkdir -p /etc/caddy/sites

# Enable Caddy service
systemctl enable caddy
systemctl start caddy
```

## 5. Clone and Build BitgetLab

```bash
# Clone repository to /root
cd /root
git clone https://github.com/PeDave/bitget-bot.git
cd bitget-bot

# Build .NET projects
dotnet build -c Release

# Build Next.js web app
cd src/bitgetlab-web
npm install
npm run build
cd ../..
```

## 6. Configure Environment Variables

### API Configuration

```bash
# Create API environment file (or use systemd environment files)
cd /root/bitget-bot/src/BitgetLab.Api

# Copy and edit appsettings.Production.json
cat > appsettings.Production.json <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Urls": "http://localhost:3001",
  "Bitget": {
    "Mode": "ReadOnly",
    "ReadOnly": {
      "ApiKey": "YOUR_READONLY_API_KEY",
      "ApiSecret": "YOUR_READONLY_API_SECRET",
      "Passphrase": "YOUR_READONLY_PASSPHRASE"
    },
    "Trade": {
      "ApiKey": "YOUR_TRADE_API_KEY",
      "ApiSecret": "YOUR_TRADE_API_SECRET",
      "Passphrase": "YOUR_TRADE_PASSPHRASE"
    }
  }
}
EOF
```

### Web Configuration

```bash
cd /root/bitget-bot/src/bitgetlab-web

# Copy environment template
cp .env.example .env.local

# Edit .env.local with your Clerk credentials
nano .env.local
# Update:
# - NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY
# - CLERK_SECRET_KEY
# - NEXT_PUBLIC_API_URL=https://api.labotkripto.com
```

## 7. Install Systemd Services

```bash
cd /root/bitget-bot

# Copy service files
cp deploy/systemd/labot-api.service /etc/systemd/system/
cp deploy/systemd/labot-worker.service /etc/systemd/system/
cp deploy/systemd/labot-web.service /etc/systemd/system/

# Reload systemd
systemctl daemon-reload

# Enable services
systemctl enable labot-api
systemctl enable labot-worker
systemctl enable labot-web

# Start services
systemctl start labot-api
systemctl start labot-worker
systemctl start labot-web

# Check status
systemctl status labot-api
systemctl status labot-worker
systemctl status labot-web
```

## 8. Configure Caddy Reverse Proxy

```bash
# Option 1: Add to main Caddyfile
cat /root/bitget-bot/deploy/caddy/Caddyfile.snippet >> /etc/caddy/Caddyfile

# Option 2: Use separate site configs
cp /root/bitget-bot/deploy/caddy/sites.conf /etc/caddy/sites/bitgetlab.conf

# Add import to main Caddyfile if needed
echo "import /etc/caddy/sites/*" >> /etc/caddy/Caddyfile

# Reload Caddy
systemctl reload caddy

# Check Caddy status
systemctl status caddy
```

## 9. Configure Firewall (UFW)

```bash
# Allow SSH (important!)
ufw allow 22/tcp

# Allow HTTP and HTTPS
ufw allow 80/tcp
ufw allow 443/tcp

# Enable UFW
ufw enable

# Check status
ufw status
```

## 10. Install PostgreSQL (for n8n and future use)

```bash
# Install PostgreSQL
apt install -y postgresql postgresql-contrib

# Start and enable service
systemctl enable postgresql
systemctl start postgresql

# Create database and user for n8n
sudo -u postgres psql <<EOF
CREATE DATABASE n8n;
CREATE USER n8n WITH ENCRYPTED PASSWORD 'your_n8n_password';
GRANT ALL PRIVILEGES ON DATABASE n8n TO n8n;
\q
EOF
```

## 11. Install n8n

```bash
# Install n8n globally
npm install -g n8n

# Create n8n systemd service
cat > /etc/systemd/system/n8n.service <<EOF
[Unit]
Description=n8n - Workflow Automation
After=network.target postgresql.service

[Service]
Type=simple
User=root
WorkingDirectory=/root
ExecStart=/usr/bin/n8n start
Restart=on-failure
Environment=N8N_PORT=5678
Environment=N8N_PROTOCOL=http
Environment=N8N_HOST=n8n.labotkripto.com
Environment=WEBHOOK_URL=https://n8n.labotkripto.com/
Environment=DB_TYPE=postgresdb
Environment=DB_POSTGRESDB_HOST=localhost
Environment=DB_POSTGRESDB_PORT=5432
Environment=DB_POSTGRESDB_DATABASE=n8n
Environment=DB_POSTGRESDB_USER=n8n
Environment=DB_POSTGRESDB_PASSWORD=your_n8n_password

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd and start n8n
systemctl daemon-reload
systemctl enable n8n
systemctl start n8n
systemctl status n8n
```

## 12. Verify Installation

```bash
# Check all services are running
systemctl status labot-api
systemctl status labot-worker
systemctl status labot-web
systemctl status caddy
systemctl status n8n
systemctl status postgresql

# Check logs
journalctl -u labot-api -n 50 --no-pager
journalctl -u labot-web -n 50 --no-pager

# Test API endpoint
curl http://localhost:3001/api/health

# Test web app
curl http://localhost:3000

# Test via domain (requires DNS to be configured)
curl https://api.labotkripto.com/api/health
curl https://labotkripto.com
```

## 13. Maintenance Commands

### View Logs

```bash
# API logs
journalctl -u labot-api -f

# Worker logs
journalctl -u labot-worker -f

# Web logs
journalctl -u labot-web -f

# Caddy logs
journalctl -u caddy -f

# n8n logs
journalctl -u n8n -f
```

### Restart Services

```bash
systemctl restart labot-api
systemctl restart labot-worker
systemctl restart labot-web
systemctl reload caddy
```

### Update Application

```bash
cd /root/bitget-bot

# Pull latest changes
git pull

# Rebuild .NET projects
dotnet build -c Release

# Rebuild Next.js
cd src/bitgetlab-web
npm install
npm run build
cd ../..

# Restart services
systemctl restart labot-api
systemctl restart labot-worker
systemctl restart labot-web
```

## 14. Monitoring

### System Metrics

The API provides system metrics at:
- `https://api.labotkripto.com/api/system/metrics`
- `https://api.labotkripto.com/api/system/services`

### Service Status

```bash
# Check all BitgetLab services
systemctl status labot-*

# Check supporting services
systemctl status caddy n8n postgresql
```

## 15. Troubleshooting

### API won't start

```bash
# Check logs
journalctl -u labot-api -n 100 --no-pager

# Check port availability
netstat -tlnp | grep 3001

# Test configuration
cd /root/bitget-bot/src/BitgetLab.Api
dotnet run
```

### Web app won't start

```bash
# Check logs
journalctl -u labot-web -n 100 --no-pager

# Check port availability
netstat -tlnp | grep 3000

# Test manually
cd /root/bitget-bot/src/bitgetlab-web
npm start
```

### Caddy certificate issues

```bash
# Check Caddy logs
journalctl -u caddy -n 100 --no-pager

# Validate Caddyfile
caddy validate --config /etc/caddy/Caddyfile

# Ensure DNS is correctly configured
dig labotkripto.com
dig api.labotkripto.com
```

## 16. Security Considerations

1. **API Keys**: Never commit API keys to git. Use environment variables or secure configuration files.
2. **Mode Selection**: Start with `ReadOnly` mode for Bitget integration until thoroughly tested.
3. **Firewall**: Keep UFW enabled with minimal open ports.
4. **Updates**: Regularly update system packages and dependencies.
5. **Backups**: Set up regular backups of configuration and database.
6. **Monitoring**: Monitor system metrics and logs regularly.

## Support

For issues or questions:
- Check logs: `journalctl -u <service-name> -n 100`
- Review configuration files
- Check GitHub repository issues
