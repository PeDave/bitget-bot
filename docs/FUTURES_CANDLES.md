# Futures Candle Support - Implementation Guide

This guide explains how to use the futures candle support feature for backtesting on futures markets.

## Overview

The system now supports fetching candles from both **Spot** and **Futures** (USDT-margined) markets. This allows you to:
- Backtest strategies on futures markets with potentially deeper history
- Separate spot and futures candle data in the database
- Mix spot and futures strategies in your trading setup

## Database Migrations

Before using futures candles, you need to run the database migrations to add the `market_type` column:

```bash
# Run candles table migration
psql -U postgres -d bitgetlab -f docs/sql/002_add_market_type_to_candles.sql

# Run backtests table migration
psql -U postgres -d bitgetlab -f docs/sql/003_add_market_to_backtests.sql
```

These migrations:
- Add `market_type` column to `candles` table (defaults to 'spot' for existing data)
- Add `market` column to `backtests` table (defaults to 'spot' for existing data)
- Update primary keys and indexes to include market type

## API Usage

### Fetching Spot Candles (Default)

```bash
# Fetch spot candles (market parameter optional, defaults to 'spot')
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=100"

# Explicitly specify spot market
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=100&market=spot"
```

### Fetching Futures Candles

```bash
# Fetch futures candles
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=100&market=futures"

# Fetch a date range of futures candles (automatic pagination)
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&startTime=2024-11-01T00:00:00Z&endTime=2025-02-01T00:00:00Z&market=futures"
```

### Get Candle Stats by Market

```bash
# Get spot candle stats
curl "http://localhost:3001/api/bitget/market/candles/stats?symbol=BTCUSDT&interval=1h&market=spot"

# Get futures candle stats
curl "http://localhost:3001/api/bitget/market/candles/stats?symbol=BTCUSDT&interval=1h&market=futures"
```

Response example:
```json
{
  "success": true,
  "data": {
    "dbEnabled": true,
    "dbAvailable": true,
    "count": 2160,
    "minOpenTime": "2024-11-01T00:00:00Z",
    "maxOpenTime": "2025-02-01T00:00:00Z",
    "lastUpdatedAt": "2025-02-04T10:30:00Z"
  },
  "count": 2160
}
```

## Backtesting with Market Selection

### Run Spot Backtest (Default)

```bash
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20}
  }'
```

### Run Futures Backtest

```bash
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-07T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "market": "futures"
  }'
```

### Parameter Sweep on Futures Market

```bash
curl -X POST http://localhost:3001/api/bitget/backtests/sweep \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "rsi",
    "grid": {
      "period": [7, 14, 21],
      "oversoldThreshold": [20, 30],
      "overboughtThreshold": [70, 80]
    },
    "market": "futures",
    "topN": 5,
    "sortBy": "netPnl"
  }'
```

## Market Type Values

The API accepts the following values for the `market` parameter:
- `spot` - Spot market (default if not specified)
- `futures` - USDT-margined futures/perpetual market

## Data Separation

The system ensures complete separation between spot and futures candles:
- Candles are stored in the database with a `market_type` column
- The primary key includes `(symbol, interval, open_time, market_type)`
- Spot and futures candles for the same symbol/interval can coexist without conflicts

Example:
- `BTCUSDT 1h 2024-01-01T00:00:00Z spot` - Spot candle
- `BTCUSDT 1h 2024-01-01T00:00:00Z futures` - Futures candle (separate record)

## Supported Intervals

Both spot and futures markets support the same intervals:
- `1m`, `5m`, `15m`, `30m` - Minutes
- `1h`, `4h`, `6h`, `12h` - Hours
- `1d`, `3d` - Days
- `1w` - Week
- `1mo` or `1month` - Month

## Implementation Details

### Code Changes

1. **MarketType Enum** (`src/BitgetLab.Core/Models/MarketType.cs`)
   - Enum with Spot and Futures values
   - Helper methods for parsing and conversion

2. **CandleService** (`src/BitgetLab.Core/Services/Bitget/CandleService.cs`)
   - Routes to `FetchFromSpotApiAsync` or `FetchFromFuturesApiAsync` based on market
   - Uses `client.SpotApiV2.ExchangeData.GetKlinesAsync` for spot
   - Uses `client.FuturesApiV2.ExchangeData.GetKlinesAsync` for futures

3. **Database Schema**
   - Added `market_type VARCHAR(20)` column to `candles` table
   - Added `market VARCHAR(20)` column to `backtests` table
   - Updated indexes to include market type

### Backward Compatibility

The implementation maintains full backward compatibility:
- Existing API calls without `market` parameter default to `spot`
- Existing database records have `market_type` set to `'spot'` via migration default
- All existing code continues to work without modifications

## Example Workflow: Compare Spot vs Futures

```bash
# 1. Fetch spot candles for January 2024
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&startTime=2024-01-01T00:00:00Z&endTime=2024-01-31T23:59:59Z&market=spot"

# 2. Fetch futures candles for the same period
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&startTime=2024-01-01T00:00:00Z&endTime=2024-01-31T23:59:59Z&market=futures"

# 3. Run backtest on spot data
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "market": "spot"
  }'

# 4. Run identical backtest on futures data
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "market": "futures"
  }'

# 5. Compare results using the backtest IDs from responses
```

## Troubleshooting

### Issue: "Invalid market type" error

**Solution:** Ensure you're using lowercase values: `spot` or `futures`

### Issue: Different candle counts between spot and futures

**Expected behavior:** Spot and futures markets may have different data availability. Futures markets often have longer historical data.

### Issue: Database constraint violation

**Solution:** Run the migrations in order:
1. `002_add_market_type_to_candles.sql`
2. `003_add_market_to_backtests.sql`

## Future Enhancements

Potential future additions:
- USDC-margined futures support
- Market-specific backtest parameters (leverage, funding rates)
- Cross-market arbitrage strategies
- Market comparison reports
