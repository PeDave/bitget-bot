# Backtesting Pipeline Implementation Summary

## Overview

This implementation adds a complete server-side backtesting pipeline to the BitgetLab trading bot, enabling strategy testing using historical candle data with database persistence and API integration.

## What Was Implemented

### 1. Database Schema (ops/db/)
- **002_create_backtest_tables.sql**: Idempotent migration script
  - `backtests` table: Stores backtest configurations and results
  - `backtest_trades` table: Stores individual simulated trades
  - Proper indexes for efficient queries
  - JSONB columns for flexible parameter storage

### 2. Core Models (BitgetLab.Core/Models/)
- **BacktestDto.cs**: Complete data models
  - BacktestDto: Backtest record with configuration and results
  - BacktestTradeDto: Individual trade record
  - BacktestSummary: Performance metrics
  - RunBacktestRequest: API request model
  - Strategy and status constants

### 3. Strategy Framework (BitgetLab.Core/Services/Backtest/)
- **IStrategy.cs**: Strategy interface with trading signal generation
- **EmaCrossoverStrategy.cs**: EMA crossover strategy
  - Configurable fast and slow periods
  - Bullish/bearish crossover detection
- **RsiStrategy.cs**: RSI threshold strategy
  - Configurable period and thresholds
  - Oversold/overbought signal generation

### 4. Backtest Engine (BitgetLab.Core/Services/Backtest/)
- **BacktestEngine.cs**: Core backtesting logic
  - Loads candles via CandleService
  - Generates signals from strategies
  - Simulates trade execution with:
    - Configurable fees (basis points)
    - Configurable slippage (basis points)
    - Single position at a time
  - Calculates performance metrics:
    - Total trades, win rate
    - Net PnL, return percentage
    - Max drawdown
    - Equity curve

### 5. Repository Layer (BitgetLab.Core/Services/Backtest/)
- **IBacktestRepository.cs**: Repository interface
- **PostgresBacktestRepository.cs**: PostgreSQL implementation
  - SaveBacktestAsync: Persist backtest record
  - UpdateBacktestAsync: Update results
  - GetBacktestAsync: Retrieve by ID
  - GetBacktestsAsync: List with filters (symbol, strategy, status)
  - SaveTradesAsync: Batch insert with transaction
  - GetTradesAsync: Retrieve trades for backtest
  - Uses raw SQL with Npgsql (consistent with existing patterns)

### 6. Service Layer (BitgetLab.Core/Services/Backtest/)
- **BacktestService.cs**: Orchestrates backtest execution
  - Validates requests
  - Creates and configures strategies
  - Manages backtest lifecycle (pending → running → completed/failed)
  - Persists results to database
  - Error handling and logging

### 7. API Endpoints (BitgetLab.Api/Controllers/)
- **BitgetController.cs**: Added 4 new endpoints
  - `POST /api/bitget/backtests/run`: Execute backtest
  - `GET /api/bitget/backtests/:id`: Get backtest details
  - `GET /api/bitget/backtests`: List backtests (with filters)
  - `GET /api/bitget/backtests/:id/trades`: Get trade history
  - Follows existing response conventions: `{success, data, count}`

### 8. Service Registration (BitgetLab.Api/)
- **Program.cs**: Registered new services
  - IBacktestRepository → PostgresBacktestRepository
  - IBacktestEngine → BacktestEngine
  - IBacktestService → BacktestService

### 9. n8n Integration (docs/n8n/)
- **backtest-workflow.json**: Ready-to-import workflow
  - Manual trigger → Run backtest → Wait → Get results
  - Conditional check for completion
  - Get trades and format summary
- **README.md**: Comprehensive usage guide
  - Import instructions
  - Configuration details
  - Parameter documentation
  - Example usage
  - Troubleshooting guide

### 10. Documentation (docs/)
- **backtesting-pipeline.md**: Complete technical documentation
  - Architecture overview
  - Database schema details
  - API usage examples
  - Strategy documentation
  - Configuration guide
  - Performance metrics explanation
  - Custom strategy creation guide
  - Integration examples
  - Best practices
  - Troubleshooting

## Key Features

### Strategies
1. **EMA Crossover**
   - Fast/slow EMA periods configurable
   - Buy on bullish crossover, sell on bearish crossover

2. **RSI Threshold**
   - Period and thresholds configurable
   - Buy when oversold, sell when overbought

### Backtest Configuration
- Initial balance (default: 10000 USDT)
- Fee basis points (default: 10 bps / 0.1%)
- Slippage basis points (default: 5 bps / 0.05%)
- Symbol and interval selection
- Custom date range

### Performance Metrics
- Total trades, winning/losing trades
- Win rate percentage
- Net PnL after fees and slippage
- Maximum drawdown percentage
- Return on investment percentage
- Equity curve with timestamps

## Technical Decisions

### Database
- **PostgreSQL with raw SQL**: Consistent with existing CandleRepository pattern
- **JSONB for parameters**: Flexible storage for strategy parameters
- **UUID primary keys**: Standard for distributed systems
- **Proper indexes**: Optimized for common queries
- **Batch inserts with transactions**: Performance optimization

### Architecture
- **Strategy pattern**: Extensible for adding new strategies
- **Service layer separation**: Clear boundaries between concerns
- **Optional database**: Repository is optional (graceful degradation)
- **Consistent error handling**: Follows existing API patterns

### Integration
- **n8n workflow**: Visual automation for non-technical users
- **RESTful API**: Standard HTTP endpoints for any client
- **Consistent responses**: Maintains existing API conventions

## Testing Performed

### Build Verification
✅ Solution builds successfully with no errors or warnings
✅ All dependencies resolved (including Bitget.Net submodule)

### Code Review
✅ Addressed equity curve timing issue
✅ Optimized batch inserts with transactions
✅ No additional issues found

### Security Analysis
✅ CodeQL scan: 0 vulnerabilities found
✅ No security issues in new code

## Prerequisites for Use

1. **PostgreSQL Database**
   - Connection string configured in appsettings.json
   - Migration script executed: `ops/db/002_create_backtest_tables.sql`

2. **Historical Candle Data**
   - Use charting pipeline to backfill data
   - Ensure data exists for requested symbol/interval/date range

3. **API Running**
   - Start API: `cd src/BitgetLab.Api && dotnet run`
   - Default endpoint: http://localhost:3001

## Example API Usage

### Run Backtest
```bash
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2024-01-01T00:00:00Z",
    "endTime": "2024-01-31T23:59:59Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20},
    "initialBalance": 10000,
    "feeBps": 10,
    "slippageBps": 5
  }'
```

### Get Results
```bash
curl http://localhost:3001/api/bitget/backtests/{id}
```

### List Backtests
```bash
curl http://localhost:3001/api/bitget/backtests?symbol=BTCUSDT&limit=50
```

### Get Trades
```bash
curl http://localhost:3001/api/bitget/backtests/{id}/trades
```

## Files Changed/Added

### New Files (17)
- ops/db/002_create_backtest_tables.sql
- src/BitgetLab.Core/Models/BacktestDto.cs
- src/BitgetLab.Core/Services/Backtest/IStrategy.cs
- src/BitgetLab.Core/Services/Backtest/EmaCrossoverStrategy.cs
- src/BitgetLab.Core/Services/Backtest/RsiStrategy.cs
- src/BitgetLab.Core/Services/Backtest/BacktestEngine.cs
- src/BitgetLab.Core/Services/Backtest/IBacktestRepository.cs
- src/BitgetLab.Core/Services/Backtest/PostgresBacktestRepository.cs
- src/BitgetLab.Core/Services/Backtest/BacktestService.cs
- docs/n8n/backtest-workflow.json
- docs/n8n/README.md
- docs/backtesting-pipeline.md

### Modified Files (2)
- src/BitgetLab.Api/Controllers/BitgetController.cs (added 4 endpoints)
- src/BitgetLab.Api/Program.cs (registered 3 services)

## Next Steps

### For Users
1. Run the database migration
2. Backfill candle data for desired symbols
3. Import n8n workflow or use API directly
4. Run backtests and analyze results

### For Developers
1. Add custom strategies by implementing IStrategy
2. Enhance with additional performance metrics
3. Add visualization capabilities
4. Implement walk-forward optimization
5. Add support for multiple positions/portfolios

## Success Criteria Met

✅ Server-side indicator computation in .NET API
✅ API endpoints for running and querying backtests
✅ PostgreSQL persistence with proper schema
✅ Ready-to-import n8n workflow
✅ Two strategies (EMA crossover, RSI)
✅ Configurable strategy parameters
✅ Spot trading simulation with fees and slippage
✅ Performance summary with all required metrics
✅ Existing response conventions preserved
✅ Complete documentation

## Security Summary

No security vulnerabilities detected:
- CodeQL analysis: 0 alerts
- No SQL injection risks (parameterized queries)
- No hardcoded credentials
- Proper error handling
- Input validation on all endpoints
- Transaction management for data consistency
