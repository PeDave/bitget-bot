# Candle Range Query API

## Overview
This document describes the candle range query API endpoint added to support efficient retrieval of historical candle data with warmup periods for technical indicator calculations.

## Endpoint

```
GET /api/candles
```

## Purpose
Fetch candles for a specific symbol, market, and time interval, including optional "warmup" candles before the start time. This is particularly useful for backtesting and indicator calculations that require historical data before the analysis period begins.

## Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `symbol` | string | Yes | - | Trading pair symbol (e.g., "BTCUSDT") |
| `market` | string | No | "spot" | Market type: "spot" or "futures" |
| `interval` | string | Yes | - | Candle interval (e.g., "1m", "5m", "1h", "1d") |
| `start` | string | Yes | - | Start time in ISO 8601 format (e.g., "2024-01-01T00:00:00Z") |
| `end` | string | Yes | - | End time in ISO 8601 format (e.g., "2024-01-02T00:00:00Z") |
| `warmupCandles` | integer | No | 200 | Number of candles to include before start time (0-10000) |

## Response Format

```json
{
  "symbol": "BTCUSDT",
  "market": "spot",
  "interval": "1h",
  "start": "2024-01-01T00:00:00Z",
  "end": "2024-01-02T00:00:00Z",
  "warmupCandles": 200,
  "count": 224,
  "candles": [
    {
      "openTime": "2023-12-31T16:00:00Z",
      "open": 42150.50,
      "high": 42200.00,
      "low": 42100.00,
      "close": 42180.00,
      "volume": 125.45,
      "quoteVolume": 5291234.50
    },
    // ... more candles ...
  ]
}
```

## Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `symbol` | string | The requested trading symbol |
| `market` | string | The requested market type |
| `interval` | string | The requested candle interval |
| `start` | datetime | The requested start time |
| `end` | datetime | The requested end time |
| `warmupCandles` | integer | The requested number of warmup candles |
| `count` | integer | Total number of candles returned (warmup + main range) |
| `candles` | array | Array of candle objects, sorted by time ascending |

### Candle Object

| Field | Type | Description |
|-------|------|-------------|
| `openTime` | datetime | Candle opening time (UTC) |
| `open` | decimal | Opening price |
| `high` | decimal | Highest price |
| `low` | decimal | Lowest price |
| `close` | decimal | Closing price |
| `volume` | decimal | Base asset volume |
| `quoteVolume` | decimal | Quote asset volume |

## Behavior

### Warmup Candles
- The endpoint returns up to `warmupCandles` candles immediately preceding the `start` time
- If fewer warmup candles exist in the database, all available candles are returned
- Warmup candles are useful for technical indicators that require historical context (e.g., 200-period moving average)

### Time Range
- The main query returns all candles where `start <= openTime <= end`
- Both start and end times are inclusive

### Ordering
- Candles are always returned in ascending chronological order
- This ensures proper indicator calculation from oldest to newest

### Deduplication
- The query automatically removes any duplicate candles (by time)
- This provides a defensive layer even though the database has a unique constraint

## Example Requests

### Basic Request
```bash
curl "http://localhost:3001/api/candles?symbol=BTCUSDT&interval=1h&start=2024-01-01T00:00:00Z&end=2024-01-02T00:00:00Z"
```

### With Custom Warmup
```bash
curl "http://localhost:3001/api/candles?symbol=ETHUSDT&interval=15m&start=2024-01-01T00:00:00Z&end=2024-01-01T12:00:00Z&warmupCandles=50"
```

### Futures Market
```bash
curl "http://localhost:3001/api/candles?symbol=BTCUSDT&market=futures&interval=4h&start=2024-01-01T00:00:00Z&end=2024-01-07T00:00:00Z&warmupCandles=100"
```

### No Warmup
```bash
curl "http://localhost:3001/api/candles?symbol=BTCUSDT&interval=1d&start=2024-01-01T00:00:00Z&end=2024-01-31T00:00:00Z&warmupCandles=0"
```

## Error Responses

### 400 Bad Request
Returned when request parameters are invalid:

```json
{
  "success": false,
  "error": "Interval parameter is required"
}
```

Common validation errors:
- Missing required parameters (symbol, interval, start, end)
- Invalid market type (must be "spot" or "futures")
- Invalid date format (must be ISO 8601)
- End time before start time
- Negative warmupCandles value

### 503 Service Unavailable
Returned when the database is not configured:

```json
{
  "success": false,
  "error": "Candle repository is not configured"
}
```

### 500 Internal Server Error
Returned when an unexpected error occurs:

```json
{
  "success": false,
  "error": "Internal server error",
  "message": "Connection timeout"
}
```

## Database Performance

### Index Usage
The query leverages the existing composite index:
```sql
idx_candles_symbol_interval_time_market (symbol, interval, market_type, open_time)
```

### Query Strategy
The implementation uses two separate queries combined with UNION:

1. **Warmup Query**: Fetches up to N candles before start time
   ```sql
   WHERE symbol = ? AND interval = ? AND market_type = ? AND open_time < start
   ORDER BY open_time DESC LIMIT warmupCandles
   ```

2. **Main Query**: Fetches all candles in [start, end] range
   ```sql
   WHERE symbol = ? AND interval = ? AND market_type = ? 
     AND open_time >= start AND open_time <= end
   ```

Both queries use the composite index for efficient lookups. The UNION operation is fast since the result sets are typically small (hundreds to thousands of rows).

### Performance Expectations
- Typical response time: < 100ms for queries under 1000 candles
- Maximum recommended range: 10,000 candles
- Database load: Minimal (index-only scans)

## Use Cases

### Backtesting
```javascript
// Fetch candles with 200-period warmup for moving average calculation
const response = await fetch('/api/candles?' + new URLSearchParams({
  symbol: 'BTCUSDT',
  interval: '1h',
  start: '2024-01-01T00:00:00Z',
  end: '2024-12-31T23:59:59Z',
  warmupCandles: 200
}));

const { candles } = await response.json();
// Now calculate indicators starting from candles[200]
```

### Chart Display with Indicators
```javascript
// Fetch recent data with warmup for real-time indicator calculation
const end = new Date();
const start = new Date(end - 7 * 24 * 60 * 60 * 1000); // 7 days ago

const response = await fetch('/api/candles?' + new URLSearchParams({
  symbol: 'ETHUSDT',
  interval: '15m',
  start: start.toISOString(),
  end: end.toISOString(),
  warmupCandles: 50  // For 50-period EMA
}));
```

## Testing

### Automated Tests
The implementation includes 7 comprehensive unit tests covering:
- DTO structure validation
- Empty database handling
- Ascending time order verification
- Duplicate prevention
- Warmup candle inclusion
- Insufficient warmup candles
- Zero warmup candles
- Market type isolation

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~CandleRangeQueryTests"
```

### Manual Testing
A test script is provided for manual endpoint verification:
```bash
./test-candles-endpoint.sh
```

## Implementation Notes

### Compatibility
- No breaking changes to existing APIs
- Uses existing database schema and indexes
- Compatible with existing backtest and charting features

### Security
- All SQL parameters are parameterized to prevent injection
- Input validation on all parameters
- No sensitive data exposure
- Passed CodeQL security analysis

### Future Enhancements
Potential improvements for future versions:
- Pagination support for very large ranges
- Response compression for bandwidth optimization
- Caching layer for frequently requested ranges
- Multiple symbol support in single request
- WebSocket streaming for real-time updates
