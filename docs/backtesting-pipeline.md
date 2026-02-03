# Backtesting Pipeline Documentation

## Overview

The BitgetLab backtesting pipeline enables server-side strategy testing using historical candle data. It includes a complete backtesting engine, multiple trading strategies, database persistence, and API endpoints for integration with n8n or other automation tools.

## Architecture

### Components

1. **Backtest Engine** (`BacktestEngine.cs`)
   - Loads historical candle data via `CandleService`
   - Generates trading signals from strategies
   - Simulates trade execution with fees and slippage
   - Calculates performance metrics

2. **Strategies** (in `Services/Backtest/`)
   - `EmaCrossoverStrategy`: EMA crossover-based trading
   - `RsiStrategy`: RSI threshold-based trading
   - Extensible `IStrategy` interface for custom strategies

3. **Repository Layer** (`PostgresBacktestRepository.cs`)
   - Persists backtest configurations and results
   - Stores individual trade records
   - Query support for filtering and pagination

4. **API Endpoints** (in `BitgetController.cs`)
   - `POST /api/bitget/backtests/run`: Execute a backtest
   - `GET /api/bitget/backtests/:id`: Get backtest details
   - `GET /api/bitget/backtests`: List backtests
   - `GET /api/bitget/backtests/:id/trades`: Get trade history

## Database Schema

### Tables

#### `backtests`
Stores backtest configurations and results.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| created_at | TIMESTAMP | Creation timestamp |
| symbol | VARCHAR(50) | Trading pair (e.g., BTCUSDT) |
| interval | VARCHAR(10) | Candle interval (e.g., 1h, 4h) |
| start_time | TIMESTAMP | Backtest start time |
| end_time | TIMESTAMP | Backtest end time |
| strategy | VARCHAR(50) | Strategy name (ema_cross, rsi) |
| parameters | JSONB | Strategy parameters |
| status | VARCHAR(20) | Status (pending, running, completed, failed) |
| summary | JSONB | Results summary |
| error | TEXT | Error message if failed |

#### `backtest_trades`
Stores individual simulated trades.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| backtest_id | UUID | Foreign key to backtests |
| entry_time | TIMESTAMP | Position entry time |
| exit_time | TIMESTAMP | Position exit time |
| side | VARCHAR(10) | Trade direction (long/short) |
| entry_price | NUMERIC(20,8) | Entry price |
| exit_price | NUMERIC(20,8) | Exit price |
| qty | NUMERIC(20,8) | Position size |
| pnl | NUMERIC(20,8) | Profit/loss |
| metadata | JSONB | Additional trade data |

### Migration

Run the SQL migration to create tables:

```bash
psql -U postgres -d bitgetlab -f ops/db/002_create_backtest_tables.sql
```

## API Usage

### Run a Backtest

**Endpoint:** `POST /api/bitget/backtests/run`

**Request Body:**
```json
{
  "symbol": "BTCUSDT",
  "interval": "1h",
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-31T23:59:59Z",
  "strategy": "ema_cross",
  "parameters": {
    "fastPeriod": 10,
    "slowPeriod": 20
  },
  "initialBalance": 10000,
  "feeBps": 10,
  "slippageBps": 5
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "backtestId": "550e8400-e29b-41d4-a716-446655440000",
    "status": "completed",
    "summary": {
      "totalTrades": 15,
      "winningTrades": 9,
      "losingTrades": 6,
      "winRate": 60.0,
      "netPnl": 234.56,
      "maxDrawdown": 5.2,
      "initialBalance": 10000,
      "finalBalance": 10234.56,
      "returnPercent": 2.35,
      "equityCurve": [...]
    }
  },
  "count": 1
}
```

### Get Backtest Details

**Endpoint:** `GET /api/bitget/backtests/:id`

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "createdAt": "2024-02-03T14:30:00Z",
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {
      "fastPeriod": 10,
      "slowPeriod": 20
    },
    "status": "completed",
    "summary": {...},
    "error": null
  },
  "count": 1
}
```

### List Backtests

**Endpoint:** `GET /api/bitget/backtests?symbol=BTCUSDT&strategy=ema_cross&limit=50`

**Query Parameters:**
- `symbol` (optional): Filter by trading pair
- `strategy` (optional): Filter by strategy name
- `status` (optional): Filter by status
- `limit` (optional): Max results (default: 50, max: 100)

**Response:**
```json
{
  "success": true,
  "data": [...],
  "count": 15
}
```

### Get Backtest Trades

**Endpoint:** `GET /api/bitget/backtests/:id/trades`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "...",
      "backtestId": "...",
      "entryTime": "2024-01-05T10:00:00Z",
      "exitTime": "2024-01-06T14:00:00Z",
      "side": "long",
      "entryPrice": 42000.50,
      "exitPrice": 42500.75,
      "qty": 0.238095,
      "pnl": 119.11,
      "metadata": {
        "entryReason": "Fast EMA(10) crossed above Slow EMA(20)",
        "exitReason": "Fast EMA(10) crossed below Slow EMA(20)",
        "entryFee": 1.00,
        "exitFee": 1.01
      }
    }
  ],
  "count": 15
}
```

## Strategies

### EMA Crossover Strategy

Trades based on exponential moving average crossovers.

**Strategy Name:** `ema_cross`

**Parameters:**
```json
{
  "fastPeriod": 10,
  "slowPeriod": 20
}
```

**Logic:**
- **Buy Signal:** Fast EMA crosses above slow EMA (bullish crossover)
- **Sell Signal:** Fast EMA crosses below slow EMA (bearish crossover)

**Requirements:**
- Minimum candles: `slowPeriod + 1`

### RSI Strategy

Trades based on RSI (Relative Strength Index) overbought/oversold levels.

**Strategy Name:** `rsi`

**Parameters:**
```json
{
  "period": 14,
  "oversoldThreshold": 30,
  "overboughtThreshold": 70
}
```

**Logic:**
- **Buy Signal:** RSI crosses below oversold threshold
- **Sell Signal:** RSI crosses above overbought threshold

**Requirements:**
- Minimum candles: `period + 2`

## Backtest Configuration

### Fee Configuration

**feeBps** (Basis Points)
- Trading fee as basis points (1 bps = 0.01%)
- Default: 10 bps (0.1%)
- Applied to both entry and exit

### Slippage Configuration

**slippageBps** (Basis Points)
- Market slippage as basis points
- Default: 5 bps (0.05%)
- Applied as:
  - Buy: Price increased by slippage
  - Sell: Price decreased by slippage

### Initial Balance

**initialBalance**
- Starting capital for simulation
- Default: 10000 (USDT)
- Used to calculate position sizes

## Performance Metrics

### Summary Fields

- **totalTrades**: Total number of completed trades
- **winningTrades**: Number of profitable trades
- **losingTrades**: Number of losing trades
- **winRate**: Percentage of winning trades
- **netPnl**: Total profit/loss after fees
- **maxDrawdown**: Maximum peak-to-trough decline (%)
- **initialBalance**: Starting capital
- **finalBalance**: Ending capital
- **returnPercent**: Return on initial balance (%)
- **equityCurve**: Array of balance snapshots over time

## Integration

### n8n Workflow

The provided n8n workflow (`docs/n8n/backtest-workflow.json`) demonstrates:
1. Triggering a backtest via API
2. Waiting for completion
3. Retrieving results and trades
4. Formatting output

See `docs/n8n/README.md` for detailed n8n integration guide.

### Custom Integration

Any HTTP client can integrate with the backtesting API:

```python
import requests

# Run backtest
response = requests.post('http://localhost:3001/api/bitget/backtests/run', json={
    'symbol': 'BTCUSDT',
    'interval': '1h',
    'startTime': '2024-01-01T00:00:00Z',
    'endTime': '2024-01-31T23:59:59Z',
    'strategy': 'ema_cross',
    'parameters': {'fastPeriod': 10, 'slowPeriod': 20}
})

result = response.json()
backtest_id = result['data']['backtestId']

# Get details
details = requests.get(f'http://localhost:3001/api/bitget/backtests/{backtest_id}')
print(details.json())
```

## Creating Custom Strategies

### Step 1: Implement IStrategy Interface

```csharp
public class MyCustomStrategy : IStrategy
{
    public string Name => "My Custom Strategy";
    private int _myParam;

    public void Configure(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("myParam", out var param))
        {
            _myParam = Convert.ToInt32(param);
        }
    }

    public IEnumerable<TradingSignal> GenerateSignals(List<CandleDto> candles)
    {
        var signals = new List<TradingSignal>();
        
        // Your strategy logic here
        // Generate Buy/Sell signals based on candle data
        
        return signals;
    }
}
```

### Step 2: Register Strategy

Update `BacktestService.CreateStrategy()`:

```csharp
private IStrategy CreateStrategy(string strategyName)
{
    return strategyName.ToLowerInvariant() switch
    {
        "ema_cross" => new EmaCrossoverStrategy(),
        "rsi" => new RsiStrategy(),
        "my_custom" => new MyCustomStrategy(), // Add here
        _ => throw new ArgumentException($"Unknown strategy: {strategyName}")
    };
}
```

## Prerequisites

1. **PostgreSQL Database**
   - Connection string configured in `appsettings.json`
   - Tables created via migration script

2. **Historical Candle Data**
   - Use the charting pipeline to backfill data
   - Ensure sufficient candles for the backtest period

3. **API Running**
   - Start the API: `cd src/BitgetLab.Api && dotnet run`
   - Default port: 3001

## Troubleshooting

### "Backtest service not available"
- Verify PostgreSQL connection string in `appsettings.json`
- Check database tables exist
- Ensure backtest services are registered in `Program.cs`

### "No candles found"
- Backfill candle data for the symbol and time range
- Check candle data exists in database: `SELECT * FROM candles WHERE symbol='BTCUSDT' LIMIT 10`

### "Not enough candles to compute"
- Strategy requires minimum candles (e.g., EMA needs `slowPeriod + 1`)
- Extend the date range or reduce period parameters

### Poor Strategy Performance
- Adjust strategy parameters
- Test different time periods
- Consider market conditions during backtest period
- Account for overfitting on historical data

## Best Practices

1. **Data Quality**
   - Ensure complete candle data without gaps
   - Verify candle data accuracy
   - Use consistent intervals

2. **Parameter Optimization**
   - Test multiple parameter combinations
   - Avoid overfitting to historical data
   - Use walk-forward analysis

3. **Risk Management**
   - Include realistic fees and slippage
   - Test worst-case scenarios
   - Consider maximum drawdown limits

4. **Validation**
   - Compare results with manual calculations
   - Verify trade logic matches expectations
   - Test edge cases (no signals, all wins/losses)

## Future Enhancements

Potential improvements to the backtesting system:

- [ ] Portfolio-level backtesting (multiple symbols)
- [ ] Advanced order types (limit, stop-loss)
- [ ] Position sizing strategies
- [ ] Multi-timeframe analysis
- [ ] Walk-forward optimization
- [ ] Monte Carlo simulation
- [ ] Export results to CSV/Excel
- [ ] Visualization of equity curves
- [ ] Live trading integration
- [ ] Strategy comparison dashboard
