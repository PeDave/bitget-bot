# Futures Data Pipeline Implementation Summary

## Overview
This implementation adds a per-symbol futures data pipeline system that automatically manages historical data backfill, real-time WebSocket subscriptions, periodic REST synchronization, and retention policies.

## Features

### 1. Per-Symbol Pipeline Management
- Start/stop independent pipelines for each trading symbol
- Single instance guarantee per symbol+market combination
- Status tracking with timestamps and error reporting
- Background service that runs continuously

### 2. Initial Historical Backfill
When a pipeline starts, it automatically fetches historical data for all configured intervals:
- **15m**: 45 days of history
- **30m**: 90 days of history
- **1h**: 180 days of history
- **4h**: 365 days of history (1 year)
- **1d**: 730 days of history (2 years)

### 3. Live Data Updates
Two strategies for different interval types:

**WebSocket Subscriptions** (smaller intervals):
- 15m, 30m, 1h intervals subscribe to real-time WebSocket updates
- Automatic gap detection and backfill
- Persistent subscriptions during pipeline lifetime

**Periodic REST Sync** (larger intervals):
- 4h, 1d intervals fetch via REST API every 30 minutes (configurable)
- Catches any missed candles
- More efficient than WebSocket for infrequent updates

### 4. Retention Policy
Automatically manages database size with two mechanisms:

**Time-based retention**:
- Deletes candles older than the lookback window for each interval
- Prevents unbounded database growth
- Configurable per interval

**Max rows safety cap**:
- Enforces a hard limit (default: 10,000 rows per symbol+market+interval)
- Keeps only the newest rows if limit exceeded
- Prevents runaway storage issues

### 5. API Endpoints

#### Start Pipeline
```bash
POST /api/bitget/pipeline/start?symbol=BTCUSDT&market=futures
```
Response:
```json
{
  "success": true,
  "message": "Pipeline started for BTCUSDT (futures)"
}
```

#### Stop Pipeline
```bash
POST /api/bitget/pipeline/stop?symbol=BTCUSDT&market=futures
```

#### Get Pipeline Status
```bash
GET /api/bitget/pipeline/status?symbol=BTCUSDT&market=futures
```
Response:
```json
{
  "success": true,
  "status": {
    "symbol": "BTCUSDT",
    "market": "futures",
    "isRunning": true,
    "startedAt": "2026-02-04T15:30:00Z",
    "lastBackfillTimes": {
      "15m": "2026-02-04T15:31:00Z",
      "30m": "2026-02-04T15:32:00Z",
      "1h": "2026-02-04T15:33:00Z",
      "4h": "2026-02-04T15:34:00Z",
      "1d": "2026-02-04T15:35:00Z"
    },
    "lastWsUpdate": "2026-02-04T15:40:00Z",
    "lastRestSync": "2026-02-04T15:45:00Z",
    "activeWsIntervals": ["15m", "30m", "1h"],
    "activeRestIntervals": ["4h", "1d"],
    "errors": []
  }
}
```

#### List All Pipelines
```bash
GET /api/bitget/pipeline/list
```

## Configuration

Add to `appsettings.json`:
```json
{
  "Bitget": {
    "Futures": {
      "Pipeline": {
        "Enabled": false,
        "WsIntervals": ["15m", "30m", "1h"],
        "RestIntervals": ["4h", "1d"],
        "LookbackDays": {
          "15m": 45,
          "30m": 90,
          "1h": 180,
          "4h": 365,
          "1d": 730
        },
        "MaxRows": 10000,
        "SyncEveryMinutes": 30
      }
    }
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable/disable the pipeline system |
| `WsIntervals` | string[] | `["15m","30m","1h"]` | Intervals to subscribe via WebSocket |
| `RestIntervals` | string[] | `["4h","1d"]` | Intervals to sync via periodic REST |
| `LookbackDays` | object | See above | Retention window per interval |
| `MaxRows` | int | `10000` | Maximum rows per symbol+market+interval |
| `SyncEveryMinutes` | int | `30` | REST sync frequency in minutes |

## Architecture

### Services

1. **FuturesPipelineManagerService**
   - BackgroundService that manages all pipelines
   - Runs periodic REST sync timer
   - Coordinates backfill, WebSocket, and retention

2. **CandleRetentionService**
   - Applies time-based and max-rows retention policies
   - Calculates cutoff times based on configuration
   - Performs efficient SQL cleanup

3. **PostgresCandleRepository** (Extended)
   - Added retention methods: `DeleteCandlesOlderThanAsync`, `DeleteCandlesBeyondMaxRowsAsync`
   - Added row counting: `GetCandleCountAsync`
   - Efficient batch operations

### Data Flow

```
User → API Endpoint (Start Pipeline)
  ↓
FuturesPipelineManagerService
  ↓
┌─────────────────────┬──────────────────┬────────────────┐
│  Initial Backfill   │  WebSocket Sub   │  REST Sync     │
│  (all intervals)    │  (15m,30m,1h)    │  (4h,1d)       │
└─────────┬───────────┴────────┬─────────┴────────┬───────┘
          │                    │                  │
          ↓                    ↓                  ↓
    CandleService    WebSocketSubscriptionService
          │                    │                  │
          └────────────────────┴──────────────────┘
                             ↓
                  PostgresCandleRepository
                             ↓
                       PostgreSQL DB
                             ↓
                  CandleRetentionService
                   (periodic cleanup)
```

## Database Impact

### New Methods in ICandleRepository
- `DeleteCandlesOlderThanAsync`: Deletes candles before cutoff timestamp
- `DeleteCandlesBeyondMaxRowsAsync`: Enforces row count limit
- `GetCandleCountAsync`: Counts candles for a symbol+interval

### SQL Queries
All retention queries use indexed columns for efficient execution:
```sql
-- Time-based deletion
DELETE FROM candles
WHERE symbol = @symbol AND interval = @interval 
  AND market_type = @marketType AND open_time < @cutoffTime;

-- Max rows enforcement
DELETE FROM candles
WHERE (symbol, interval, market_type, open_time) IN (
  SELECT symbol, interval, market_type, open_time
  FROM candles
  WHERE symbol = @symbol AND interval = @interval 
    AND market_type = @marketType
  ORDER BY open_time DESC
  OFFSET @maxRows
);
```

## Testing

### Unit Tests (30 tests, all passing)
- Configuration validation
- Interval categorization (WS vs REST)
- Retention cutoff calculations
- Lookback day ordering
- Default value verification

Run tests:
```bash
dotnet test src/BitgetLab.Core.Tests/BitgetLab.Core.Tests.csproj
```

### Manual Testing Script
```bash
./test-pipeline-api.sh
```

## Usage Example

### 1. Enable Pipeline in Configuration
```json
{
  "Bitget": {
    "Futures": {
      "Pipeline": {
        "Enabled": true
      }
    }
  }
}
```

### 2. Start API
```bash
cd src/BitgetLab.Api
dotnet run
```

### 3. Start Pipeline for BTCUSDT
```bash
curl -X POST "http://localhost:3001/api/bitget/pipeline/start?symbol=BTCUSDT&market=futures"
```

### 4. Check Status
```bash
curl "http://localhost:3001/api/bitget/pipeline/status?symbol=BTCUSDT&market=futures"
```

### 5. Stop Pipeline (when done)
```bash
curl -X POST "http://localhost:3001/api/bitget/pipeline/stop?symbol=BTCUSDT&market=futures"
```

## Security

- CodeQL security scan: ✅ 0 vulnerabilities
- No SQL injection vulnerabilities (parameterized queries)
- No hardcoded credentials
- Proper error handling and logging
- Resource cleanup on shutdown

## Performance Considerations

1. **Backfill**: Uses pagination with safety limits (max 200 iterations)
2. **WebSocket**: Reuses existing subscription infrastructure
3. **REST Sync**: Fire-and-forget to avoid blocking
4. **Retention**: Efficient indexed SQL queries
5. **Memory**: Pipelines track only metadata, not candle data

## Future Enhancements

Possible improvements:
- Add metrics/monitoring (Prometheus)
- Support multiple symbols in single request
- Configurable backfill batch sizes
- Dashboard UI for pipeline management
- Alert notifications on errors
- Automatic restart on failures
