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

- .NET 8 SDK
- Node.js 20+
- npm or yarn

### Development Setup

> **⚠️ Important:** Always clone the repository to a clean directory. Avoid creating nested copies like `bitget-bot/bitget-bot/` which can confuse build tools and deployment scripts. The canonical source should always be under `src/` at the repository root.

1. **Clone the repository**

```bash
git clone --recursive https://github.com/PeDave/bitget-bot.git
cd bitget-bot
```

**Note:** The `--recursive` flag is important as it initializes git submodules including `vendor/Bitget.Net`.

If you cloned without `--recursive`, initialize submodules manually:

```bash
git submodule update --init --recursive
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

# System metrics
curl http://localhost:3001/api/system/metrics

# Service status
curl http://localhost:3001/api/system/services

# Bitget endpoints
# Get all trading symbols
curl http://localhost:3001/api/bitget/symbols

# Get ticker data for a specific symbol
curl "http://localhost:3001/api/bitget/market/ticker?symbol=BTCUSDT"

# Get spot account balances
curl http://localhost:3001/api/bitget/spot/balances

# Get spot balances with filter (only show non-zero balances)
curl "http://localhost:3001/api/bitget/spot/balances?nonZeroOnly=true"

# Get spot balances with minimum value filter
curl "http://localhost:3001/api/bitget/spot/balances?minValue=10"

# Get futures account balances (USDT and USDC futures)
curl http://localhost:3001/api/bitget/futures/balances

# Get futures balances with filter (only show non-zero balances)
curl "http://localhost:3001/api/bitget/futures/balances?nonZeroOnly=true"

# Get futures positions (USDT and USDC futures)
curl http://localhost:3001/api/bitget/futures/positions

# Get futures positions with filter (only show non-zero positions)
curl "http://localhost:3001/api/bitget/futures/positions?nonZeroOnly=true"

# Get only USDT futures positions
curl "http://localhost:3001/api/bitget/futures/positions?includeUsdc=false"

# Get only USDC futures positions
curl "http://localhost:3001/api/bitget/futures/positions?includeUsdt=false"

# Get futures positions with custom margin asset
curl "http://localhost:3001/api/bitget/futures/positions?usdtMarginAsset=USDT&usdcMarginAsset=USDC"

# Get account valuation (total balance across all account types)
curl http://localhost:3001/api/bitget/account/valuation

# Get spot open orders
curl http://localhost:3001/api/bitget/spot/orders/open

# Get spot open orders with filters
curl "http://localhost:3001/api/bitget/spot/orders/open?symbol=BTCUSDT&limit=50"

# Get spot closed orders
curl http://localhost:3001/api/bitget/spot/orders/closed

# Get spot closed orders with filters
curl "http://localhost:3001/api/bitget/spot/orders/closed?symbol=BTCUSDT&limit=50"

# Get spot order detail by order ID
curl "http://localhost:3001/api/bitget/spot/orders/detail?symbol=BTCUSDT&orderId=123456789"

# Get spot order detail by client order ID
curl "http://localhost:3001/api/bitget/spot/orders/detail?symbol=BTCUSDT&clientOrderId=my-order-123"

# Get spot user trades
curl http://localhost:3001/api/bitget/spot/trades

# Get spot user trades with filters
curl "http://localhost:3001/api/bitget/spot/trades?symbol=BTCUSDT&orderId=123456789&limit=50"

# Get futures open orders (USDT and USDC futures)
curl http://localhost:3001/api/bitget/futures/orders/open

# Get futures open orders with filters
curl "http://localhost:3001/api/bitget/futures/orders/open?includeUsdc=false&symbol=BTCUSDT&limit=50"

# Get futures closed orders
curl http://localhost:3001/api/bitget/futures/orders/closed

# Get futures closed orders with filters
curl "http://localhost:3001/api/bitget/futures/orders/closed?includeUsdc=false&symbol=BTCUSDT&limit=50"

# Get futures order detail by order ID
curl "http://localhost:3001/api/bitget/futures/orders/detail?symbol=BTCUSDT&orderId=123456789"

# Get futures order detail by client order ID
curl "http://localhost:3001/api/bitget/futures/orders/detail?symbol=BTCUSDT&clientOrderId=my-order-123"

# Get futures order detail with specific product type
curl "http://localhost:3001/api/bitget/futures/orders/detail?productType=USDT-FUTURES&symbol=BTCUSDT&orderId=123456789"

# Get futures user trades
curl http://localhost:3001/api/bitget/futures/trades

# Get futures user trades with filters
curl "http://localhost:3001/api/bitget/futures/trades?includeUsdc=false&symbol=BTCUSDT&orderId=123456789&limit=50"

# Get copy trading current orders
curl http://localhost:3001/api/bitget/copytrading/current-orders

# Get copy trading current orders with filters
curl "http://localhost:3001/api/bitget/copytrading/current-orders?productType=USDT-FUTURES&symbol=BTCUSDT&limit=50"

# Place order (requires Trade mode, returns 403 in ReadOnly mode)
curl -X POST http://localhost:3001/api/bitget/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSDT","side":0,"type":0,"quantity":0.001,"price":50000}'
# Note: side: 0=Buy, 1=Sell; type: 0=Market, 1=Limit
```

**Note on Earn/Bots**: Balances from Earn and Bots products are included in the `/api/bitget/account/valuation` endpoint, but there is no dedicated Earn/Bots API client in Bitget.Net for querying detailed information about these products.

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
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=bitgetlab;Username=postgres;Password=yourpassword"
  },
  "Charting": {
    "EnablePersistence": false,
    "BufferSize": 500,
    "EnableGapDetection": true
  }
}
```

### PostgreSQL Database Setup (Optional)

The application supports optional PostgreSQL persistence for candle data. This feature enables:
- Historical candle storage across restarts
- Efficient querying of historical data
- Automatic database fallback when fetching candles

**Configuration Options**:
- `EnablePersistence` (bool, default: false) - Enable/disable database persistence
- `BufferSize` (int, default: 500) - Size of in-memory ring buffer per subscription
- `EnableGapDetection` (bool, default: true) - Enable automatic gap detection and backfill

**Setup Steps**:

1. **Install PostgreSQL** (if not already installed):
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install postgresql postgresql-contrib

# macOS (using Homebrew)
brew install postgresql@16
brew services start postgresql@16
```

2. **Create Database**:
```bash
sudo -u postgres psql
CREATE DATABASE bitgetlab;
CREATE USER bitgetlab_user WITH ENCRYPTED PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE bitgetlab TO bitgetlab_user;
\q
```

3. **Apply SQL Schema**:
```bash
# Navigate to project directory
cd /path/to/bitget-bot

# Apply the schema
psql -U bitgetlab_user -d bitgetlab -f docs/sql/001_create_candles.sql
```

4. **Configure Connection String**:

Edit `appsettings.json` or use environment variables:
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=bitgetlab;Username=bitgetlab_user;Password=your_secure_password"
  },
  "Charting": {
    "EnablePersistence": true,
    "BufferSize": 500,
    "EnableGapDetection": true
  }
}
```

Or use environment variable:
```bash
export ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=bitgetlab;Username=bitgetlab_user;Password=your_secure_password"
```

5. **Verify Connection**:

The application will log connection status on startup. Check logs:
```bash
journalctl -u labot-api -f | grep -i postgres
```

**Behavior**:
- When `EnablePersistence=false`: Candles are only stored in-memory (ring buffer)
- When `EnablePersistence=true` and DB configured:
  - WebSocket updates are persisted to database
  - Gap backfills are persisted to database  
  - `GET /api/bitget/market/candles` reads from database first, falls back to Bitget REST API
  - Buffer initialization loads from database if available
- When `EnablePersistence=true` but no DB connection: Application continues without persistence (logged as warning)

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
- **Account Balance API**: Get spot and futures account balances
- **Futures Position API**: Get open positions in USDT and USDC futures
- **Account Valuation API**: Get total account valuation across all account types
- **Mode-based Security**: ReadOnly mode prevents accidental trades

### Available Endpoints

#### 1. Futures Positions
**Endpoint**: `GET /api/bitget/futures/positions`

Retrieves open futures positions for USDT and/or USDC futures markets.

**Query Parameters**:
- `nonZeroOnly` (bool, default: false) - Filter to show only non-zero positions
- `includeUsdt` (bool, default: true) - Include USDT futures positions
- `includeUsdc` (bool, default: true) - Include USDC futures positions
- `usdtMarginAsset` (string, optional) - Override margin asset for USDT futures (default: "USDT")
- `usdcMarginAsset` (string, optional) - Override margin asset for USDC futures (default: "USDC")

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "symbol": "BTCUSDT",
      "positionSide": "Long",
      "total": 0.5,
      "available": 0.5,
      "averageOpenPrice": 45000.0,
      "unrealizedPnl": 250.5,
      "leverage": 10,
      "liquidationPrice": 40000.0,
      "updateTime": "2024-01-01T12:00:00Z",
      "productType": "USDT-FUTURES",
      "marginAsset": "USDT"
    }
  ],
  "count": 1
}
```

**Examples**:
```bash
# Get all futures positions
curl http://localhost:3001/api/bitget/futures/positions

# Get only non-zero positions
curl "http://localhost:3001/api/bitget/futures/positions?nonZeroOnly=true"

# Get only USDT futures positions
curl "http://localhost:3001/api/bitget/futures/positions?includeUsdc=false"
```

#### 2. Account Valuation
**Endpoint**: `GET /api/bitget/account/valuation`

Retrieves total account valuation across all account types (spot, p2p, futures, etc.) in USDT equivalent.

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "accountType": "spot",
      "usdtBalance": 1000.50
    },
    {
      "accountType": "usdt_futures",
      "usdtBalance": 5000.25
    },
    {
      "accountType": "usdc_futures",
      "usdtBalance": 2000.75
    }
  ],
  "count": 3
}
```

**Example**:
```bash
# Get account valuation
curl http://localhost:3001/api/bitget/account/valuation
```

#### 3. Charting and Real-Time Data

##### Get Historical Candles
**Endpoint**: `GET /api/bitget/market/candles`

Retrieves historical OHLCV (Open, High, Low, Close, Volume) candle data for a trading symbol.

**Query Parameters**:
- `symbol` (string, required) - Trading symbol (e.g., "BTCUSDT")
- `interval` (string, required) - Candle interval (see supported intervals below)
- `startTime` (DateTime, optional) - Start time for filtering
- `endTime` (DateTime, optional) - End time for filtering
- `limit` (int, default: 100, max: 1000) - Number of candles to retrieve

**Supported Intervals**:
- `1m`, `5m`, `15m`, `30m` - Minutes
- `1h`, `4h`, `6h`, `12h` - Hours
- `1d`, `3d` - Days
- `1w` - Week
- `1mo`, `1month` - Month

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "openTime": "2024-01-01T12:00:00Z",
      "open": 45000.50,
      "high": 45500.00,
      "low": 44800.00,
      "close": 45300.00,
      "volume": 123.45,
      "quoteVolume": 5567890.12
    }
  ],
  "count": 100
}
```

**Examples**:
```bash
# Get last 100 1-hour candles for BTC
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h"

# Get daily candles with limit
curl "http://localhost:3001/api/bitget/market/candles?symbol=ETHUSDT&interval=1d&limit=30"

# Get monthly candles
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1mo"
```

##### Get Candle Buffer (In-Memory Ring Buffer)
**Endpoint**: `GET /api/bitget/market/candle-buffer`

Retrieves candles from the in-memory ring buffer for an active WebSocket subscription. The buffer maintains the last N candles (default 500) in chronological order with automatic gap detection and backfill.

**Query Parameters**:
- `symbol` (string, required) - Trading symbol (e.g., "BTCUSDT")
- `interval` (string, required) - Candle interval (e.g., 1m, 5m, 1h, 1d)
- `limit` (int, optional, default: 500, max: 500) - Number of candles to return

**Response** (when subscribed with data):
```json
{
  "success": true,
  "data": [
    {
      "openTime": "2024-01-01T12:00:00Z",
      "open": 45000.50,
      "high": 45500.00,
      "low": 44800.00,
      "close": 45300.00,
      "volume": 123.45,
      "quoteVolume": 5567890.12
    }
  ],
  "count": 100
}
```

**Response** (when not subscribed or no data):
```json
{
  "success": true,
  "data": [],
  "count": 0
}
```

**Examples**:
```bash
# Get all candles from buffer for active subscription
curl "http://localhost:3001/api/bitget/market/candle-buffer?symbol=BTCUSDT&interval=1m"

# Get last 100 candles
curl "http://localhost:3001/api/bitget/market/candle-buffer?symbol=BTCUSDT&interval=1m&limit=100"

# Get candles for different intervals
curl "http://localhost:3001/api/bitget/market/candle-buffer?symbol=ETHUSDT&interval=15m&limit=200"
```

**Features**:
- **In-Memory Ring Buffer**: Maintains the last N candles (configurable, default 500) per subscription
- **Automatic Gap Detection**: Detects missing candles between consecutive timestamps
- **Automatic Backfill**: Fetches missing candles from Bitget REST API when gaps are detected
- **Thread-Safe**: Concurrent read/write operations are safe
- **Chronological Order**: Returns candles sorted by open time (oldest to newest)
- **Optional PostgreSQL Persistence**: Can persist candles to database if enabled (see configuration below)

##### Get Latest Real-Time Candle
**Endpoint**: `GET /api/bitget/market/latest-candle`

Retrieves the latest real-time candle data from active WebSocket subscription. Returns empty if not subscribed or no data available.

**Query Parameters**:
- `symbol` (string, required) - Trading symbol (e.g., "BTCUSDT")
- `interval` (string, required) - Candle interval (same as supported intervals above)

**Response** (when data available):
```json
{
  "success": true,
  "data": [
    {
      "openTime": "2024-01-01T12:00:00Z",
      "open": 45000.50,
      "high": 45500.00,
      "low": 44800.00,
      "close": 45300.00,
      "volume": 123.45,
      "quoteVolume": 5567890.12
    }
  ],
  "count": 1
}
```

**Response** (when not subscribed or no data):
```json
{
  "success": true,
  "data": [],
  "count": 0
}
```

**Examples**:
```bash
# Get latest candle for active subscription
curl "http://localhost:3001/api/bitget/market/latest-candle?symbol=BTCUSDT&interval=1m"
```

##### Subscribe to Real-Time Candle Updates
**Endpoint**: `POST /api/bitget/market/subscribe`

Creates a WebSocket subscription for real-time candle updates. The subscription persists and updates automatically.

**Request Body**:
```json
{
  "symbol": "BTCUSDT",
  "interval": "1m"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Successfully subscribed to BTCUSDT candles"
}
```

##### Unsubscribe from Candle Updates
**Endpoint**: `POST /api/bitget/market/unsubscribe`

Removes an active WebSocket subscription.

**Request Body**:
```json
{
  "symbol": "BTCUSDT",
  "interval": "1m"
}
```

##### Get Active Subscriptions
**Endpoint**: `GET /api/bitget/market/subscriptions`

Lists all active real-time candle subscriptions.

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "symbol": "BTCUSDT",
      "interval": "1m",
      "subscribedAt": "2024-01-01T12:00:00Z"
    }
  ],
  "count": 1
}
```

#### 4. Spot Order History and Trades

##### Get Spot Closed Orders
**Endpoint**: `GET /api/bitget/spot/orders/closed`

Retrieves closed (completed, cancelled, or expired) spot orders.

**Query Parameters**:
- `symbol` (string, optional) - Trading symbol filter (e.g., "BTCUSDT")
- `orderId` (string, optional) - Filter by specific order ID
- `startTime` (DateTime, optional) - Filter orders after this time
- `endTime` (DateTime, optional) - Filter orders before this time
- `idLessThan` (string, optional) - Pagination cursor
- `limit` (int, default: 100) - Maximum number of results

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "orderId": "1234567890",
      "clientOrderId": "my-order-123",
      "symbol": "BTCUSDT",
      "side": "Buy",
      "type": "Limit",
      "status": "Filled",
      "price": 50000.0,
      "quantity": 0.1,
      "quantityFilled": 0.1,
      "averagePrice": 50000.0,
      "createTime": "2024-01-01T12:00:00Z",
      "updateTime": "2024-01-01T12:01:00Z",
      "source": "spot",
      "productType": null,
      "marginAsset": null
    }
  ],
  "count": 1
}
```

**Examples**:
```bash
# Get all closed orders
curl http://localhost:3001/api/bitget/spot/orders/closed

# Get closed orders for a specific symbol
curl "http://localhost:3001/api/bitget/spot/orders/closed?symbol=BTCUSDT&limit=50"
```

##### Get Spot Order Detail
**Endpoint**: `GET /api/bitget/spot/orders/detail`

Retrieves detailed information for a specific spot order.

**Query Parameters** (required):
- `symbol` (string, required) - Trading symbol (e.g., "BTCUSDT")
- `orderId` (string, optional) - Order ID (exactly one of orderId or clientOrderId required)
- `clientOrderId` (string, optional) - Client order ID (exactly one of orderId or clientOrderId required)

**Response**: Same structure as closed orders, returns a single order wrapped in an array.

**Examples**:
```bash
# Get order detail by order ID
curl "http://localhost:3001/api/bitget/spot/orders/detail?symbol=BTCUSDT&orderId=1234567890"

# Get order detail by client order ID
curl "http://localhost:3001/api/bitget/spot/orders/detail?symbol=BTCUSDT&clientOrderId=my-order-123"
```

##### Get Spot User Trades
**Endpoint**: `GET /api/bitget/spot/trades`

Retrieves spot trade history (fills/executions).

**Query Parameters**:
- `symbol` (string, optional) - Trading symbol filter
- `orderId` (string, optional) - Filter by specific order ID
- `startTime` (DateTime, optional) - Filter trades after this time
- `endTime` (DateTime, optional) - Filter trades before this time
- `idLessThan` (string, optional) - Pagination cursor
- `limit` (int, default: 100) - Maximum number of results

**Response**:
```json
{
  "success": true,
  "data": [
    {
      "tradeId": "9876543210",
      "orderId": "1234567890",
      "clientOrderId": null,
      "symbol": "BTCUSDT",
      "side": "Buy",
      "price": 50000.0,
      "quantity": 0.1,
      "tradeTime": "2024-01-01T12:00:00Z",
      "feeAsset": "USDT",
      "fee": 5.0,
      "feeDeduction": null,
      "feeTotalDeduction": null,
      "source": "spot",
      "productType": null,
      "marginAsset": null
    }
  ],
  "count": 1
}
```

**Examples**:
```bash
# Get all trades
curl http://localhost:3001/api/bitget/spot/trades

# Get trades for a specific order
curl "http://localhost:3001/api/bitget/spot/trades?orderId=1234567890"
```

#### 5. Futures Order History and Trades

##### Get Futures Closed Orders
**Endpoint**: `GET /api/bitget/futures/orders/closed`

Retrieves closed futures orders. Queries USDT and USDC futures in parallel when both include flags are set.

**Query Parameters**:
- `includeUsdt` (bool, default: true) - Include USDT futures orders
- `includeUsdc` (bool, default: true) - Include USDC futures orders
- `symbol` (string, optional) - Trading symbol filter
- `orderId` (string, optional) - Filter by specific order ID
- `clientOrderId` (string, optional) - Filter by client order ID
- `startTime` (DateTime, optional) - Filter orders after this time
- `endTime` (DateTime, optional) - Filter orders before this time
- `idLessThan` (string, optional) - Pagination cursor
- `limit` (int, default: 100) - Maximum number of results per product type

**Response**: Same structure as spot orders but includes `productType` ("USDT-FUTURES" or "USDC-FUTURES") and `marginAsset` fields.

**Examples**:
```bash
# Get all futures closed orders
curl http://localhost:3001/api/bitget/futures/orders/closed

# Get only USDT futures closed orders
curl "http://localhost:3001/api/bitget/futures/orders/closed?includeUsdc=false"
```

##### Get Futures Order Detail
**Endpoint**: `GET /api/bitget/futures/orders/detail`

Retrieves detailed information for a specific futures order. When productType is not specified, searches both USDT and USDC futures in parallel.

**Query Parameters**:
- `includeUsdt` (bool, default: true) - Include USDT futures when searching
- `includeUsdc` (bool, default: true) - Include USDC futures when searching
- `productType` (string, optional) - Override product type ("USDT-FUTURES" or "USDC-FUTURES")
- `symbol` (string, required) - Trading symbol
- `orderId` (string, optional) - Order ID (exactly one of orderId or clientOrderId required)
- `clientOrderId` (string, optional) - Client order ID (exactly one of orderId or clientOrderId required)

**Examples**:
```bash
# Search both USDT and USDC futures
curl "http://localhost:3001/api/bitget/futures/orders/detail?symbol=BTCUSDT&orderId=1234567890"

# Query specific product type
curl "http://localhost:3001/api/bitget/futures/orders/detail?productType=USDT-FUTURES&symbol=BTCUSDT&orderId=1234567890"
```

##### Get Futures User Trades
**Endpoint**: `GET /api/bitget/futures/trades`

Retrieves futures trade history. Queries USDT and USDC futures in parallel when both include flags are set.

**Query Parameters**:
- `includeUsdt` (bool, default: true) - Include USDT futures trades
- `includeUsdc` (bool, default: true) - Include USDC futures trades
- `symbol` (string, optional) - Trading symbol filter
- `orderId` (string, optional) - Filter by specific order ID
- `startTime` (DateTime, optional) - Filter trades after this time
- `endTime` (DateTime, optional) - Filter trades before this time
- `idLessThan` (string, optional) - Pagination cursor
- `limit` (int, default: 100) - Maximum number of results per product type

**Response**: Includes normalized fee fields:
- `feeAsset` - The asset used for fees
- `fee` - Primary fee amount
- `feeDeduction` - Fee deduction amount (futures only)
- `feeTotalDeduction` - Total fee deduction (futures only)

**Examples**:
```bash
# Get all futures trades
curl http://localhost:3001/api/bitget/futures/trades

# Get only USDT futures trades for a specific symbol
curl "http://localhost:3001/api/bitget/futures/trades?includeUsdc=false&symbol=BTCUSDT"
```

**Note**: All order history and trade endpoints require read-only API credentials and work in ReadOnly mode.

**Liquidation Information**: 
- Liquidation price for open positions is available via the `/api/bitget/futures/positions` endpoint.
- Historical liquidation events can be queried through the closed orders endpoint (`/api/bitget/futures/orders/closed`), where liquidated orders will have specific status indicators.
- A dedicated liquidation history/events endpoint may be added in the future if needed.

### Submodule Management

The Bitget.Net SDK is vendored under `vendor/Bitget.Net` as a git submodule.

**Initialize submodules** (if not already done):

```bash
git submodule update --init --recursive
```

**Update Bitget.Net to latest version**:

```bash
cd vendor/Bitget.Net
git checkout main
git pull
cd ../..
git add vendor/Bitget.Net
git commit -m "Update Bitget.Net to latest version"
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