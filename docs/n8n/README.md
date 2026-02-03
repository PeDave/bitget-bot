# n8n Backtest Workflows

This directory contains ready-to-import n8n workflows for the BitgetLab backtesting system.

## Available Workflows

1. **backtest-workflow.json** - Basic backtest execution workflow
2. **backtest-report-workflow.json** - Enhanced backtest with formatted reports

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

---

## Workflow: backtest-report-workflow.json

### Description
This enhanced workflow runs a backtest and produces a comprehensive formatted report including:
- Summary metrics (total trades, win rate, PnL, drawdown, returns)
- First 3 and last 3 trades chronologically
- Top 3 winning and losing trades

### How to Import
1. Open your n8n instance
2. Click on "Workflows" in the left sidebar
3. Click the "+" button to create a new workflow
4. Click the "..." menu in the top right
5. Select "Import from File"
6. Choose the `backtest-report-workflow.json` file
7. The workflow will be imported with all nodes configured

### Workflow Structure

```
Start (Manual Trigger or Webhook)
  ↓
Run Backtest (HTTP Request - POST)
  ↓
Wait (2 seconds)
  ↓
Get Backtest Details (HTTP Request - GET)
  ↓
Get Backtest Trades (HTTP Request - GET)
  ↓
Format Report (Code node - generates Markdown report)
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
  "startTime": "2026-01-01T00:00:00Z",
  "endTime": "2026-02-01T00:00:00Z",
  "strategy": "rsi",
  "parameters": {
    "period": 14,
    "oversoldThreshold": 30,
    "overboughtThreshold": 70
  },
  "initialBalance": 1000,
  "feeBps": 10,
  "slippageBps": 5
}
```

**Important**: This workflow uses the `parameters` field (not `strategyParams`) to match the API specification.

### Strategy Parameters

#### EMA Crossover Strategy (`ema_cross`)
```json
{
  "parameters": {
    "fastPeriod": 10,
    "slowPeriod": 20
  }
}
```

#### RSI Strategy (`rsi`)
```json
{
  "parameters": {
    "period": 14,
    "oversoldThreshold": 30,
    "overboughtThreshold": 70
  }
}
```

### Output

The workflow produces a structured JSON output with:
- `backtestId`: Unique identifier for the backtest
- `summary`: Performance metrics object
- `report`: Formatted Markdown report
- `tradeCount`: Total number of trades
- `backtestDetails`: Full backtest configuration and results
- `trades`: Object containing:
  - `first`: Array of first 3 trades
  - `last`: Array of last 3 trades
  - `best`: Array of top 3 winning trades
  - `worst`: Array of top 3 losing trades

### Example Markdown Report

```markdown
# Backtest Report

## Summary
- **Backtest ID**: 550e8400-e29b-41d4-a716-446655440000
- **Symbol**: BTCUSDT
- **Strategy**: rsi
- **Interval**: 1h
- **Period**: 2026-01-01T00:00:00Z to 2026-02-01T00:00:00Z
- **Status**: completed

## Performance Metrics
- **Total Trades**: 15
- **Win Rate**: 60.00%
- **Net PnL**: $234.56
- **Return**: 23.46%
- **Max Drawdown**: 5.20%
- **Initial Balance**: $1000.00
- **Final Balance**: $1234.56

## First 3 Trades
[Trade details...]

## Last 3 Trades
[Trade details...]

## Top 3 Winners
[Best performing trades...]

## Top 3 Losers
[Worst performing trades...]
```

### Triggering Methods

#### Manual Execution
1. Open the workflow in n8n
2. Click "Execute Workflow" button
3. Uses default parameters or provide custom input

#### Webhook Trigger
1. The workflow includes a webhook trigger node
2. Send POST request to: `http://your-n8n-instance/webhook/backtest-report`
3. Include parameters in request body:
```bash
curl -X POST http://localhost:5678/webhook/backtest-report \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "ETHUSDT",
    "interval": "4h",
    "startTime": "2026-01-01T00:00:00Z",
    "endTime": "2026-02-01T00:00:00Z",
    "strategy": "rsi",
    "parameters": {
      "period": 14,
      "oversoldThreshold": 20,
      "overboughtThreshold": 80
    }
  }'
```

### Use Cases

**1. Automated Reporting**
- Schedule the workflow to run daily/weekly backtest reports
- Send formatted reports via email, Slack, or other channels
- Store reports in a database or file storage

**2. Strategy Analysis**
- Analyze best and worst performing trades
- Identify patterns in winning/losing trades
- Optimize strategy parameters based on report insights

**3. Performance Monitoring**
- Track strategy performance over different time periods
- Compare results across different symbols
- Monitor win rates and PnL trends

### Prerequisites

Same as the basic backtest workflow:
1. **BitgetLab API Running** at `http://localhost:3001`
2. **PostgreSQL Database** configured with backtest tables
3. **Candle Data Available** for the requested symbol and time range
