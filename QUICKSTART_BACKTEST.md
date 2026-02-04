# Backtesting Pipeline - Quick Start Guide

This guide will help you get started with the backtesting pipeline in under 5 minutes.

## Prerequisites

1. **PostgreSQL Database Running**
   ```bash
   # Check if PostgreSQL is running
   psql --version
   ```

2. **BitgetLab Built**
   ```bash
   ./build.sh
   ```

## Step 1: Setup Database (1 minute)

### Create Database (if not exists)
```bash
psql -U postgres -c "CREATE DATABASE bitgetlab;"
```

### Run Migrations
```bash
# Run candles table migration (if not already done)
psql -U postgres -d bitgetlab -f docs/sql/001_create_candles.sql

# Run backtest tables migration
psql -U postgres -d bitgetlab -f ops/db/002_create_backtest_tables.sql
```

### Verify Tables
```bash
psql -U postgres -d bitgetlab -c "\dt"
```

You should see: `backtests`, `backtest_trades`, and `candles` tables.

## Step 2: Configure Connection String (30 seconds)

Edit `src/BitgetLab.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=bitgetlab;Username=postgres;Password=yourpassword"
  },
  "BitgetOptions": {
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret",
    "Passphrase": "your-passphrase"
  },
  "ChartingOptions": {
    "EnablePersistence": true
  }
}
```

## Step 3: Start the API (10 seconds)

```bash
cd src/BitgetLab.Api
dotnet run
```

The API will start on: http://localhost:3001

Keep this terminal open.

## Step 4: Backfill Candle Data (2 minutes)

Open a new terminal and use the API to fetch some historical data. The API now supports automatic pagination for date ranges, so you can request large historical datasets:

```bash
# Backfill 1 week of hourly BTCUSDT candles (168 candles)
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=168"

# Or fetch a multi-month range (pagination happens automatically)
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&startTime=2024-11-01T00:00:00Z&endTime=2025-02-01T00:00:00Z"
```

The endpoint will automatically paginate through all available data when both `startTime` and `endTime` are provided.

## Step 5: Run Your First Backtest (30 seconds)

### Option A: Using curl

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

### Option B: Using the test script

```bash
./test-backtest-api.sh
```

### Option C: Using n8n

1. Import workflow from `docs/n8n/backtest-workflow.json`
2. Update the API URL if needed
3. Click "Execute Workflow"

## Step 6: View Results

### Get Backtest Details
```bash
curl http://localhost:3001/api/bitget/backtests/{BACKTEST_ID}
```

Replace `{BACKTEST_ID}` with the ID from step 5 response.

### Get Trade History
```bash
curl http://localhost:3001/api/bitget/backtests/{BACKTEST_ID}/trades
```

### List All Backtests
```bash
curl http://localhost:3001/api/bitget/backtests
```

## Understanding the Results

The response includes:

```json
{
  "success": true,
  "data": {
    "backtestId": "...",
    "status": "completed",
    "summary": {
      "totalTrades": 15,          // Number of completed trades
      "winningTrades": 9,          // Number of profitable trades
      "losingTrades": 6,           // Number of losing trades
      "winRate": 60.0,             // Win rate percentage
      "netPnl": 234.56,            // Total profit/loss
      "maxDrawdown": 5.2,          // Largest peak-to-trough decline (%)
      "initialBalance": 10000,     // Starting capital
      "finalBalance": 10234.56,    // Ending capital
      "returnPercent": 2.35,       // ROI percentage
      "equityCurve": [...]         // Balance over time
    }
  }
}
```

## Example: Run Both Strategies

### EMA Crossover
```bash
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20}
  }'
```

### RSI Strategy
```bash
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "rsi",
    "parameters": {"period": 14, "oversoldThreshold": 30, "overboughtThreshold": 70}
  }'
```

## Common Issues

### "Backtest service not available"
- Check PostgreSQL connection string in appsettings.json
- Verify database tables exist: `psql -U postgres -d bitgetlab -c "\dt"`

### "No candles found"
- Backfill candle data for the requested symbol and date range
- Check candles table: `psql -U postgres -d bitgetlab -c "SELECT COUNT(*) FROM candles WHERE symbol='BTCUSDT';"`

### "Not enough candles to compute"
- EMA strategy needs at least `slowPeriod + 1` candles
- RSI strategy needs at least `period + 2` candles
- Either backfill more data or reduce the period parameters

## Next Steps

1. **Experiment with Parameters**
   - Try different fast/slow EMA periods
   - Adjust RSI thresholds
   - Compare results

2. **Test Different Symbols**
   - ETHUSDT, BNBUSDT, etc.
   - Backfill data for each symbol first

3. **Test Different Intervals**
   - 1m, 5m, 15m, 1h, 4h, 1d
   - Different strategies work better on different timeframes

4. **Use n8n for Automation**
   - Import the workflow
   - Schedule regular backtests
   - Compare strategies automatically

5. **Optimize Strategies**
   - Test multiple parameter combinations
   - Track which performs best
   - Be careful of overfitting!

## Advanced Usage

### Custom Initial Balance
```json
{
  "initialBalance": 50000,
  "feeBps": 10,
  "slippageBps": 5
}
```

### Filter Backtests
```bash
# Get all BTCUSDT backtests
curl "http://localhost:3001/api/bitget/backtests?symbol=BTCUSDT"

# Get all EMA crossover backtests
curl "http://localhost:3001/api/bitget/backtests?strategy=ema_cross"

# Get only completed backtests
curl "http://localhost:3001/api/bitget/backtests?status=completed"
```

### Query the Database Directly
```bash
# View all backtests
psql -U postgres -d bitgetlab -c "SELECT id, symbol, strategy, status, created_at FROM backtests ORDER BY created_at DESC LIMIT 10;"

# View trades for a specific backtest
psql -U postgres -d bitgetlab -c "SELECT entry_time, exit_time, side, entry_price, exit_price, pnl FROM backtest_trades WHERE backtest_id='YOUR-BACKTEST-ID' ORDER BY entry_time;"

# Summary statistics
psql -U postgres -d bitgetlab -c "SELECT strategy, COUNT(*), AVG((summary->>'winRate')::numeric) as avg_winrate FROM backtests WHERE status='completed' GROUP BY strategy;"
```

## Documentation

- **Full Documentation**: `docs/backtesting-pipeline.md`
- **n8n Integration**: `docs/n8n/README.md`
- **Implementation Details**: `IMPLEMENTATION_SUMMARY_BACKTEST.md`

## Support

For issues or questions:
1. Check the documentation files above
2. Review the troubleshooting sections
3. Examine the test script: `test-backtest-api.sh`
4. Check the example n8n workflow

---

**Congratulations!** You now have a working backtesting pipeline. Start testing your trading strategies! 🚀
