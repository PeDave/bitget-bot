# n8n Backtest Workflow

This directory contains ready-to-import n8n workflows for the BitgetLab backtesting system.

## Workflow: backtest-workflow.json

### Description
This workflow triggers a backtest run through the BitgetLab API, waits for completion, and retrieves the results including trade details and performance summary.

### How to Import
1. Open your n8n instance
2. Click on "Workflows" in the left sidebar
3. Click the "+" button to create a new workflow
4. Click the "..." menu in the top right
5. Select "Import from File"
6. Choose the `backtest-workflow.json` file
7. The workflow will be imported with all nodes configured

### Workflow Structure

```
Start (Manual Trigger)
  ↓
Run Backtest (HTTP Request - POST)
  ↓
Wait (3 seconds)
  ↓
Get Backtest Results (HTTP Request - GET)
  ↓
Check Status (IF node)
  ↓
  ├─ [Completed] → Get Backtest Trades → Format Summary
  └─ [Not Completed] → Format Error
```

### Configuration

Before running the workflow, update the HTTP Request nodes with your API endpoint:
- Default: `http://localhost:3001/api/bitget/backtests`
- Production: Update to your deployed API URL

### Input Parameters

The workflow accepts the following optional input parameters (defaults shown):

```json
{
  "symbol": "BTCUSDT",
  "interval": "1h",
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-31T23:59:59Z",
  "strategy": "ema_cross",
  "strategyParams": {
    "fastPeriod": 10,
    "slowPeriod": 20
  },
  "initialBalance": 10000,
  "feeBps": 10,
  "slippageBps": 5
}
```

### Strategy Parameters

#### EMA Crossover Strategy (`ema_cross`)
```json
{
  "fastPeriod": 10,
  "slowPeriod": 20
}
```

#### RSI Strategy (`rsi`)
```json
{
  "period": 14,
  "oversoldThreshold": 30,
  "overboughtThreshold": 70
}
```

### Output

The workflow outputs a formatted summary including:
- Backtest ID
- Symbol and strategy used
- Status (completed/failed)
- Total trades
- Win rate (%)
- Net PnL
- Return percentage (%)
- Max drawdown (%)

### Example Usage

1. **Manual Execution with Defaults**
   - Simply click "Execute Workflow" to run with default parameters

2. **Custom Parameters**
   - Add a "Set" node before "Run Backtest" to customize parameters:
   ```json
   {
     "symbol": "ETHUSDT",
     "interval": "4h",
     "startTime": "2024-02-01T00:00:00Z",
     "endTime": "2024-02-28T23:59:59Z",
     "strategy": "rsi",
     "strategyParams": {
       "period": 14,
       "oversoldThreshold": 30,
       "overboughtThreshold": 70
     }
   }
   ```

3. **Scheduled Backtests**
   - Replace the "Start" node with a "Cron" or "Schedule" trigger
   - Configure the schedule (e.g., daily at midnight)

### Prerequisites

1. **BitgetLab API Running**
   - Ensure the API is running and accessible
   - Default: `http://localhost:3001`

2. **PostgreSQL Database**
   - Database must be configured with backtest tables
   - Run the SQL migration: `ops/db/002_create_backtest_tables.sql`

3. **Candle Data Available**
   - Ensure historical candle data exists for the symbol and time range
   - Use the charting pipeline to backfill data if needed

### Troubleshooting

**Error: Backtest service not available**
- Check that PostgreSQL connection string is configured in `appsettings.json`
- Verify database tables exist

**Error: No candles found**
- Backfill candle data for the requested symbol and time range
- Adjust the date range to match available data

**Status: Failed**
- Check the error message in the backtest record
- Verify strategy parameters are valid
- Ensure sufficient candle data for the strategy period requirements

### Advanced Usage

**Parallel Backtests**
- Duplicate the workflow
- Configure different symbols or strategies
- Run them in parallel to compare results

**Result Storage**
- Add nodes to store results in a database, spreadsheet, or send notifications
- Use the formatted summary output as input to downstream nodes

**Performance Comparison**
- Create a workflow that runs multiple strategies on the same data
- Compare results to find the best performing strategy
