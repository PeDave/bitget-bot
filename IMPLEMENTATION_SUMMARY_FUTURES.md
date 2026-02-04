# Implementation Summary: Futures Candle Support

## Overview
This implementation adds support for fetching and backtesting on futures market candles while maintaining complete separation from spot market data.

## Changes Made

### 1. Core Models
- **MarketType.cs** - New enum with Spot and Futures values
  - Helper methods: `ParseMarketType()`, `ToStringValue()`, `ToProductType()`
  - Provides type-safe market selection throughout the codebase

### 2. Database Schema
- **002_add_market_type_to_candles.sql** - Adds `market_type` column to candles table
  - Updates primary key: `(symbol, interval, open_time, market_type)`
  - Adds index for better query performance
  - Default value: 'spot' for backward compatibility

- **003_add_market_to_backtests.sql** - Adds `market` column to backtests table
  - Tracks which market was used for each backtest
  - Default value: 'spot' for existing records

### 3. Repository Layer
- **ICandleRepository.cs** - Updated interface with `market` parameter
  - All methods now accept `MarketType market = MarketType.Spot`
  - Maintains backward compatibility with default values

- **PostgresCandleRepository.cs** - Implementation updates
  - Query WHERE clauses include `market_type`
  - Upsert operations include `market_type` in primary key
  - Statistics queries scoped by market type

### 4. Service Layer
- **ICandleService.cs** - Updated interface with `market` parameter
  - `GetCandlesAsync()` now accepts `MarketType market = MarketType.Spot`

- **CandleService.cs** - Routing and API calls
  - Routes to `FetchFromSpotApiAsync()` or `FetchFromFuturesApiAsync()`
  - Uses `client.SpotApiV2.ExchangeData.GetKlinesAsync()` for spot
  - Uses `client.FuturesApiV2.ExchangeData.GetHistoricalKlinesAsync()` for futures
  - **Note**: Futures uses historical endpoint to support startTime/endTime ranges (GetKlinesAsync fails with parameter verification errors)
  - Added `ParseFuturesInterval()` for futures-specific interval enum
  - Pagination works for both markets

### 5. Backtest Integration
- **BacktestDto.cs** - Added `Market` field
  - `RunBacktestRequest` includes optional `Market` parameter
  - `SweepBacktestRequest` includes optional `Market` parameter

- **BacktestEngine.cs** - Updated interface and implementation
  - `RunBacktestAsync()` accepts `MarketType market` parameter
  - Passes market to `CandleService.GetCandlesAsync()`

- **BacktestService.cs** - Parses and validates market type
  - Uses `MarketTypeExtensions.ParseMarketType()` to parse request
  - Passes market through to engine

- **PostgresBacktestRepository.cs** - Persistence updates
  - INSERT includes `market` column
  - SELECT queries include `market` column
  - MapBacktest() reads market field

### 6. API Controllers
- **BitgetController.cs** - Endpoint updates
  - `GET /api/bitget/market/candles` - Added `market` query parameter
  - `GET /api/bitget/market/candles/stats` - Added `market` query parameter
  - `POST /api/bitget/backtests/run` - Accepts `market` in request body
  - `POST /api/bitget/backtests/sweep` - Accepts `market` in request body

### 7. Documentation
- **FUTURES_CANDLES.md** - Comprehensive usage guide
  - API examples for spot and futures candles
  - Backtest examples with market selection
  - Troubleshooting and best practices

- **QUICKSTART_BACKTEST.md** - Updated with migration references
- **test-futures-candles.sh** - Verification script

## API Usage Examples

### Fetch Futures Candles
```bash
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=100&market=futures"
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

## Technical Details

### Market Separation
- Spot and futures candles are completely separate in the database
- Primary key includes market_type to prevent conflicts
- Same symbol/interval/time can exist for both spot and futures

### Backward Compatibility
- All existing code continues to work without changes
- Default value is "spot" for all optional market parameters
- Existing database records have market_type='spot' via migration

### Supported Markets
- **Spot** - Spot trading market
- **Futures** - USDT-margined perpetual futures

### Future Enhancements
- USDC-margined futures support
- Coin-margined futures support
- Market-specific backtest metrics (leverage, funding rates)
- Cross-market analysis and comparison

## Build Status
✅ All code compiles successfully
✅ No breaking changes to existing functionality
✅ Database migrations provided and documented

## Testing
Manual testing recommended:
1. Run database migrations
2. Start API server
3. Execute `./test-futures-candles.sh` to verify endpoints
4. Compare spot vs futures candle data
5. Run backtests on both markets

## Deployment Checklist
- [ ] Run database migration `002_add_market_type_to_candles.sql`
- [ ] Run database migration `003_add_market_to_backtests.sql`
- [ ] Restart API service
- [ ] Verify spot endpoints still work (backward compatibility)
- [ ] Test futures endpoints with sample data
- [ ] Monitor logs for any errors

## Files Modified
- src/BitgetLab.Core/Models/MarketType.cs (NEW)
- docs/sql/002_add_market_type_to_candles.sql (NEW)
- docs/sql/003_add_market_to_backtests.sql (NEW)
- src/BitgetLab.Core/Services/Bitget/ICandleRepository.cs
- src/BitgetLab.Core/Services/Bitget/PostgresCandleRepository.cs
- src/BitgetLab.Core/Services/Bitget/CandleService.cs
- src/BitgetLab.Core/Models/BacktestDto.cs
- src/BitgetLab.Core/Services/Backtest/BacktestEngine.cs
- src/BitgetLab.Core/Services/Backtest/BacktestService.cs
- src/BitgetLab.Core/Services/Backtest/PostgresBacktestRepository.cs
- src/BitgetLab.Api/Controllers/BitgetController.cs
- src/BitgetLab.Core/Services/Bitget/IndicatorService.cs
- src/BitgetLab.Core/Services/Bitget/WebSocketSubscriptionService.cs
- docs/FUTURES_CANDLES.md (NEW)
- QUICKSTART_BACKTEST.md
- test-futures-candles.sh (NEW)
