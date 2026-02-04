# Futures Data Pipeline

## Overview

The Futures Data Pipeline feature provides automated, per-symbol data management for futures trading pairs. When started for a symbol, it performs sequential initial backfill for all configured intervals, then keeps data fresh via WebSocket (small intervals) and periodic REST sync (large intervals), persisting to Postgres with retention (time window + maxRows cap).

## API Endpoints

### Start Pipeline
```bash
POST /api/bitget/pipeline/start?symbol=BTCUSDT&market=futures
```

Starts a data pipeline for the specified symbol. The pipeline will:
1. Perform sequential backfill for all configured intervals (15m, 30m, 1h, 4h, 1d)
2. Subscribe to WebSocket updates for small intervals (15m, 30m, 1h)
3. Schedule periodic REST sync for large intervals (4h, 1d)
4. Apply retention policies after each update

**Response:**
```json
{
  "symbol": "BTCUSDT",
  "market": "futures",
  "state": 1,
  "progress": "Backfilling 15m (45 days)...",
  "intervals": ["15m", "30m", "1h", "4h", "1d"],
  "completedIntervals": [],
  "startedAt": "2026-02-04T15:54:11.5695533Z",
  "errorMessage": null
}
```

**States:**
- `0`: Stopped
- `1`: Backfilling
- `2`: Running
- `3`: Failed

### Stop Pipeline
```bash
POST /api/bitget/pipeline/stop?symbol=BTCUSDT&market=futures
```

Stops a running pipeline for the specified symbol.

### Get Status
```bash
GET /api/bitget/pipeline/status?symbol=BTCUSDT&market=futures
```

Returns the current status of a pipeline.

## Configuration

Configuration is done via `appsettings.json` under the `Bitget:Futures:Pipeline` section:

```json
{
  "Bitget": {
    "Futures": {
      "Pipeline": {
        "Enabled": true,
        "WsIntervals": ["15m", "30m", "1h"],
        "RestIntervals": ["4h", "1d"],
        "SyncEveryMinutes": 30,
        "MaxRows": 10000,
        "LookbackDays": {
          "15m": 45,
          "30m": 90,
          "1h": 180,
          "4h": 365,
          "1d": 730
        }
      }
    }
  }
}
```

### Configuration Options

- **Enabled**: Enable/disable the pipeline feature
- **WsIntervals**: Intervals to keep fresh via WebSocket subscriptions
- **RestIntervals**: Intervals to keep fresh via periodic REST sync
- **SyncEveryMinutes**: How often to sync REST intervals (in minutes)
- **MaxRows**: Maximum number of rows to keep per (symbol, market_type, interval)
- **LookbackDays**: Lookback days per interval for initial backfill and retention

## Retention

The pipeline enforces two types of retention policies:

1. **Time-based**: Deletes rows older than the configured lookback window for each interval
2. **Safety cap**: If more than `MaxRows` exist, deletes the oldest rows beyond the newest `MaxRows`

Retention is applied:
- After each interval backfill during startup
- After each periodic REST sync for large intervals

## Database Schema

The pipeline uses the existing `candles` table with the following structure:

```sql
CREATE TABLE candles (
    symbol VARCHAR(50) NOT NULL,
    interval VARCHAR(10) NOT NULL,
    open_time TIMESTAMP NOT NULL,
    open NUMERIC(20, 8) NOT NULL,
    high NUMERIC(20, 8) NOT NULL,
    low NUMERIC(20, 8) NOT NULL,
    close NUMERIC(20, 8) NOT NULL,
    volume NUMERIC(20, 8) NOT NULL,
    quote_volume NUMERIC(20, 8) NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    market_type VARCHAR(20) NOT NULL DEFAULT 'spot',
    PRIMARY KEY (symbol, interval, open_time, market_type)
);
```

All candle inserts use `ON CONFLICT` upserts to avoid duplicates.

## Known Limitations

1. **WebSocket Market Type**: The existing `WebSocketSubscriptionService` does not currently support distinguishing between spot and futures market types. WebSocket updates for futures symbols will be persisted with `MarketType.Spot` until the WebSocketSubscriptionService is enhanced to support market type differentiation.

2. **Network Connectivity**: The pipeline requires network access to `api.bitget.com` for REST API calls and WebSocket subscriptions.

3. **Database Requirement**: A PostgreSQL database must be configured for the pipeline to persist data. Set the connection string in `ConnectionStrings:PostgreSQL` in `appsettings.json`.

## Usage Example

```bash
# Start a pipeline for BTCUSDT futures
curl -X POST "http://localhost:3001/api/bitget/pipeline/start?symbol=BTCUSDT&market=futures"

# Check status
curl "http://localhost:3001/api/bitget/pipeline/status?symbol=BTCUSDT&market=futures"

# Stop the pipeline
curl -X POST "http://localhost:3001/api/bitget/pipeline/stop?symbol=BTCUSDT&market=futures"
```

## Implementation Details

- **FuturesSymbolPipelineManager**: Singleton service that manages all active pipelines
- **Backfill**: Sequential across all intervals, cancellable via Stop endpoint
- **Live Updates**: WS subscriptions for small intervals, REST sync timer for large intervals
- **Retention**: Two-pass deletion (time-based, then row cap) using efficient SQL queries
- **Thread Safety**: ConcurrentDictionary for pipeline state management
- **Logging**: Structured logging at Info, Debug, and Error levels
