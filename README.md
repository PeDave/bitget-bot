# BitgetLab - Cryptocurrency Trading Bot Platform

BitgetLab is an advanced cryptocurrency trading bot system integrated with the Bitget exchange. The platform consists of multiple components running on Ubuntu 24.04 VPS behind a Caddy reverse proxy.

## 🏗️ Architecture

### Components

- **BitgetLab.Api**: ASP.NET Core (.NET 8) Web API
  - Bitget exchange integration
  - System metrics and monitoring (CPU, RAM, Disk, Load)
  - Service status monitoring (systemctl integration)
  
- **BitgetLab.Core**: Shared domain models and services
  - Common business logic
  - Bitget service abstractions
  - Configuration options
  
- **BitgetLab.Worker**: Background worker service
  - WebSocket data ingestion
  - Scheduled job execution
  - Real-time market data processing
  
- **BitgetLab.Simulator**: Paper trading simulator
  - Strategy backtesting
  - Risk-free testing environment
  
- **BitgetLab.Web**: Next.js 14 (Node 20) web application
  - Clerk authentication (admin + user roles)
  - Admin panel for system management
  - User dashboard for bot monitoring

### Infrastructure

- **Platform**: Ubuntu 24.04 LTS
- **Runtime**: Runs as root user (no Docker)
- **Reverse Proxy**: Caddy (automatic HTTPS)
- **Domains**:
  - `labotkripto.com` → Web UI (port 3000)
  - `api.labotkripto.com` → API (port 3001)
  - `n8n.labotkripto.com` → n8n automation (port 5678)

## 🚀 Quick Start

### Prerequisites

- .NET 8 SDK (required for building the project)
- Node.js 20+
- npm or yarn

> **Note:** This project is designed for .NET 8. The VPS runs .NET 8 SDK, and the Bitget.Net fork ensures full .NET 8 compatibility. Do not use .NET 9 as it may introduce compatibility issues.

### Development Setup

> **⚠️ Important:** Always clone the repository to a clean directory. Avoid creating nested copies like `bitget-bot/bitget-bot/` which can confuse build tools and deployment scripts. The canonical source should always be under `src/` at the repository root.

1. **Clone the repository with submodules**

```bash
git clone --recursive https://github.com/PeDave/bitget-bot.git
cd bitget-bot
```

**Note:** The `--recursive` flag is important as it initializes git submodules including `vendor/Bitget.Net`.

The Bitget.Net submodule points to the PeDave fork at `https://github.com/PeDave/Bitget.Net`, which ensures .NET 8 compatibility for VPS deployment.

2. **Initialize submodules manually (if cloned without --recursive)**

If you cloned without `--recursive`, initialize submodules manually:

```bash
git submodule update --init --recursive
```

To update the Bitget.Net submodule to the latest version from the fork:

```bash
cd vendor/Bitget.Net
git pull origin main
cd ../..
git add vendor/Bitget.Net
git commit -m "Update Bitget.Net submodule to latest version"
```

2. **Build .NET projects**

```bash
# Option 1: Use the build script (recommended)
./build.sh          # Builds in Release mode by default
./build.sh Debug    # Or build in Debug mode

# Option 2: Use dotnet CLI directly
dotnet restore
dotnet build

# Or build in Release mode
dotnet build -c Release

# Or build specific solution file
dotnet build BitgetLab.sln -c Release
```

3. **Run the API**

```bash
cd src/BitgetLab.Api
dotnet run

# API will be available at http://localhost:3001
# Swagger UI available at http://localhost:3001/swagger (in Development mode)
```

4. **Run the Worker (optional)**

```bash
cd src/BitgetLab.Worker
dotnet run
```

5. **Set up and run the Web app**

```bash
cd src/bitgetlab-web

# Install dependencies
npm install

# Copy environment file
cp .env.example .env.local

# Edit .env.local with your Clerk credentials
# Get credentials from https://dashboard.clerk.com

# Run development server
npm run dev

# Web app will be available at http://localhost:3000
```

### Test API Endpoints

```bash
# Health check
curl http://localhost:3001/api/health

# Swagger JSON (Development mode)
curl http://localhost:3001/swagger/v1/swagger.json

# Swagger UI (Development mode)
# Open in browser: http://localhost:3001/swagger

# System metrics
curl http://localhost:3001/api/system/metrics

# Service status
curl http://localhost:3001/api/system/services

# Bitget endpoints
# Get all trading symbols
curl http://localhost:3001/api/bitget/symbols

# Get ticker data for a specific symbol
curl "http://localhost:3001/api/bitget/market/ticker?symbol=BTCUSDT"

# Place order (requires Trade mode, returns 403 in ReadOnly mode)
# Limit order
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "side": "Buy",
    "type": "Limit",
    "quantity": 0.001,
    "price": 50000
  }'

# Market order
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "ETHUSDT",
    "side": "Sell",
    "type": "Market",
    "quantity": 0.01
  }'
```

**Order Endpoint Notes:**
- Enum values are case-insensitive: `"buy"`, `"Buy"`, `"BUY"` are all valid
- Supported `side` values: `Buy`, `Sell`
- Supported `type` values: `Market`, `Limit`
- `price` is required for Limit orders, optional for Market orders
- Returns 400 for invalid requests (missing required fields, invalid enums)
- Returns 403 when API is in ReadOnly mode

## 📦 Project Structure

```
bitget-bot/
├── src/
│   ├── BitgetLab.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/         # API controllers
│   │   ├── Services/            # System services
│   │   └── Program.cs           # Application entry point
│   │
│   ├── BitgetLab.Core/          # Shared library
│   │   ├── Models/              # Domain models
│   │   ├── Options/             # Configuration options
│   │   └── Services/            # Business services
│   │       └── Bitget/          # Bitget integration (TODO)
│   │
│   ├── BitgetLab.Worker/        # Background worker
│   │   └── Worker.cs            # Worker implementation
│   │
│   ├── BitgetLab.Simulator/     # Paper trading simulator
│   │   └── PaperTradingSimulator.cs
│   │
│   └── bitgetlab-web/           # Next.js web application
│       ├── app/                 # App router pages
│       │   ├── admin/           # Admin panel
│       │   ├── app/             # User dashboard
│       │   ├── sign-in/         # Clerk sign-in
│       │   └── sign-up/         # Clerk sign-up
│       └── middleware.ts        # Clerk middleware
│
├── deploy/
│   ├── systemd/                 # Systemd service templates
│   │   ├── labot-api.service
│   │   ├── labot-worker.service
│   │   └── labot-web.service
│   │
│   ├── caddy/                   # Caddy configuration
│   │   ├── Caddyfile.snippet
│   │   └── sites.conf
│   │
│   ├── env/                     # Environment file templates
│   │   ├── .env.api.example
│   │   └── .env.web.example
│   │
│   └── runbook/                 # Deployment documentation
│       └── VPS_SETUP.md
│
├── vendor/                      # Vendored dependencies
│   └── BITGET_INTEGRATION.md    # Integration guide
│
├── build.sh                     # Build script (alternative to dotnet build)
├── BitgetLab.sln               # Classic solution file
└── BitgetLab.slnx              # Visual Studio XML solution file
```

## ⚙️ Configuration

### API Configuration

Edit `src/BitgetLab.Api/appsettings.json`:

```json
{
  "Bitget": {
    "Mode": "ReadOnly",  // or "Trade"
    "ReadOnly": {
      "ApiKey": "your_readonly_api_key",
      "ApiSecret": "your_readonly_api_secret",
      "Passphrase": "your_readonly_passphrase"
    },
    "Trade": {
      "ApiKey": "your_trade_api_key",
      "ApiSecret": "your_trade_api_secret",
      "Passphrase": "your_trade_passphrase"
    }
  },
  "Swagger": {
    "EnableInProduction": false  // Set to true to enable Swagger in Production
  }
}
```

**Swagger Configuration:**
- Swagger is automatically enabled in Development environment
- To enable Swagger in Production, set `Swagger:EnableInProduction` to `true`
- Swagger UI: `/swagger`
- Swagger JSON: `/swagger/v1/swagger.json`

### Web Configuration

Edit `src/bitgetlab-web/.env.local`:

```env
# Clerk Authentication
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_...
CLERK_SECRET_KEY=sk_test_...

# API URL
NEXT_PUBLIC_API_URL=http://localhost:3001
```

## 🔧 Development Commands

### .NET Projects

```bash
# Build all projects (using build script - recommended)
./build.sh          # Release mode
./build.sh Debug    # Debug mode

# Build using dotnet CLI
dotnet build
dotnet build BitgetLab.sln

# Build in Release mode
dotnet build -c Release

# Run tests (when available)
dotnet test

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore
```

### Next.js Web App

```bash
cd src/bitgetlab-web

# Install dependencies
npm install

# Development server (with hot reload)
npm run dev

# Production build
npm run build

# Start production server
npm start

# Lint code
npm run lint
```

## 🚢 Production Deployment

See the comprehensive [VPS Setup Runbook](deploy/runbook/VPS_SETUP.md) for detailed deployment instructions.

### Quick Deploy Summary

1. Set up Ubuntu 24.04 VPS
2. Install .NET 8, Node.js 20, Caddy
3. Clone repository to `/root/bitget-bot`
4. Build projects
5. Configure environment variables
6. Install systemd services
7. Configure Caddy reverse proxy
8. Configure firewall (UFW)

```bash
# Deploy all services
systemctl enable --now labot-api labot-worker labot-web

# Check status
systemctl status labot-*
```

## 🔐 Security

- **API Keys**: Never commit credentials to Git
- **Modes**: Start with `ReadOnly` mode for safe testing
- **Firewall**: Use UFW to restrict access
- **HTTPS**: Caddy provides automatic TLS certificates
- **Authentication**: Clerk handles secure user authentication

## 🐛 Troubleshooting

### Check Service Logs

```bash
# API logs
journalctl -u labot-api -f

# Worker logs
journalctl -u labot-worker -f

# Web logs
journalctl -u labot-web -f
```

### Common Issues

1. **Port already in use**: Check with `netstat -tlnp | grep <port>`
2. **Service won't start**: Check logs with `journalctl -u <service> -n 100`
3. **Build errors**: Ensure correct .NET SDK version: `dotnet --version`
4. **Clerk errors**: Verify API keys in `.env.local`
5. **Submodule not initialized**: Run `git submodule update --init --recursive`

## 📝 Bitget.Net Integration

The Bitget SDK has been successfully integrated using git submodules. The integration provides:

- **Market Data API**: Get symbols, tickers, and real-time market data
- **Trading API**: Place orders (requires Trade mode)
- **Mode-based Security**: ReadOnly mode prevents accidental trades

### Submodule Management

The Bitget.Net SDK is vendored under `vendor/Bitget.Net` as a git submodule pointing to the **PeDave fork** at `https://github.com/PeDave/Bitget.Net`.

**Why the fork?**
- The VPS runs .NET 8 SDK
- The PeDave fork ensures full .NET 8 compatibility
- This avoids any potential compatibility issues with newer .NET versions

**Initialize submodules** (if not already done):

```bash
git submodule update --init --recursive
```

**Update Bitget.Net to latest version from the fork**:

```bash
cd vendor/Bitget.Net
git pull origin main
cd ../..
git add vendor/Bitget.Net
git commit -m "Update Bitget.Net to latest version"
```

**Verify submodule configuration**:

```bash
# Check submodule remote URL
git config --file .gitmodules --get-regexp url
# Should show: submodule.vendor/Bitget.Net.url https://github.com/PeDave/Bitget.Net.git

# Check current commit
cd vendor/Bitget.Net && git log --oneline -1
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 📄 License

[Add your license here]

## 🔗 Links

- Bitget Exchange: https://www.bitget.com
- Bitget.Net SDK: https://github.com/JKorf/Bitget.Net
- Clerk Auth: https://clerk.com
- Caddy Server: https://caddyserver.com
- n8n Automation: https://n8n.io

## 📞 Support

For issues or questions:
- Check the logs: `journalctl -u <service-name>`
- Review [VPS_SETUP.md](deploy/runbook/VPS_SETUP.md)
- Open a GitHub issue