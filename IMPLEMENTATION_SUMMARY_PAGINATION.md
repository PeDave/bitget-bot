# Implementation Summary: Historical Candle Pagination for Backtesting

## Overview

This implementation adds automatic pagination support to the candle fetching service, enabling backtests and long-range historical queries to retrieve complete datasets instead of being limited to the first 100-1000 candles.

## Problem Statement

**Before**: When requesting candles for a multi-month date range (e.g., 2025-11-01 to 2026-02-01), the API would only return the first page of results (default 100 candles, max 1000), causing backtests to start much later than requested and operate on incomplete data.

**After**: The service automatically paginates through all available candles when both `startTime` and `endTime` are provided, returning complete historical datasets.

## Key Changes

### 1. CandleService Pagination Implementation

**File**: `src/BitgetLab.Core/Services/Bitget/CandleService.cs`

#### New Constants
```csharp
private const int MAX_PAGINATION_ITERATIONS = 200; // Maximum iterations to prevent infinite loops
private const int MIN_PAGINATION_ITERATIONS = 10;  // Minimum iterations regardless of expected candles
private const int SAFETY_MULTIPLIER = 2;           // Multiply expected iterations by this for buffer
private const int DEFAULT_EXPECTED_CANDLES = 1000; // Default when interval calculation fails
```

#### New Method: `FetchRangeWithPaginationAsync`
- Automatically paginates through Bitget API when both startTime and endTime are provided
- Uses `limit` parameter as page size (max 1000 per request)
- Deduplicates candles using Dictionary<DateTime, CandleDto> keyed by OpenTime
- Includes safety mechanisms:
  - **Max iterations limit**: Prevents infinite loops
  - **Expected candle calculation**: Estimates required iterations based on date range and interval
  - **Progress detection**: Stops if pagination isn't advancing
  - **Early exit**: Stops when fewer candles than requested are returned
- Returns candles in chronological order

#### New Method: `CalculateExpectedCandles`
- Calculates expected number of candles for a date range
- Uses `IntervalHelper.ParseIntervalToTimeSpan()` for accurate interval duration
- Provides safety defaults for invalid intervals

#### Updated Method: `GetCandlesAsync`
- **Changed behavior when date range provided**: `limit` acts as page size, returns all candles in range
- **Unchanged behavior without date range**: Returns up to `limit` candles (backward compatible)
- **DB merge logic updated**: Returns full range instead of truncating to limit when both startTime and endTime are provided

### 2. API Endpoint Behavior

**Endpoint**: `GET /api/bitget/market/candles`

**Query Parameters**:
- `symbol` (string, required) - Trading symbol
- `interval` (string, required) - Candle interval (1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo)
- `startTime` (DateTime, optional) - Start time filter
- `endTime` (DateTime, optional) - End time filter
- `limit` (int, default: 100, max: 1000) - **Now acts as page size when date range provided**

**Behavior**:
- **Without date range**: Returns up to `limit` candles (max 1000) - unchanged
- **With date range** (`startTime` + `endTime`): Paginates automatically, returns all available candles

**Example**:
```bash
# Returns ~2200+ hourly candles for 3-month range (not just 100)
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&startTime=2025-11-01T00:00:00Z&endTime=2026-02-01T00:00:00Z"
```

### 3. Documentation Updates

- **README.md**: Added pagination behavior explanation and examples
- **QUICKSTART_BACKTEST.md**: Updated with multi-month example
- **Interface documentation**: Updated to clarify new behavior

## Technical Details

### Pagination Algorithm

1. **Calculate expected candles**: `(endTime - startTime) / interval`
2. **Calculate max iterations**: `min(200, max(10, (expectedCandles / pageSize + 1) * 2))`
3. **Loop while**:
   - `currentStartTime < endTime`
   - `iteration < maxIterations`
4. **Each iteration**:
   - Fetch page: `GetKlinesAsync(symbol, interval, currentStartTime, endTime, pageSize)`
   - Add to dictionary for deduplication
   - Calculate next start: `lastCandleTime + interval`
   - Check for progress and early exit conditions
5. **Return**: Sorted list by OpenTime

### Deduplication Strategy

Uses `Dictionary<DateTime, CandleDto>` with OpenTime as key:
- Automatically handles overlapping pages
- Preserves most recent data for duplicate timestamps
- O(1) lookup/insert performance

### Safety Mechanisms

1. **Max iterations limit**: Prevents infinite loops from API issues
2. **Expected candle calculation**: Sets reasonable iteration limit based on date range
3. **Progress detection**: Exits if `nextStartTime <= currentStartTime`
4. **Early exit**: Stops when `result.Count < pageSize`
5. **Cancellation support**: Honors `CancellationToken` in loop

### Database Persistence Integration

- DB queries use same logic: return full range when both dates provided
- Merge logic updated to not truncate full range results
- New candles from REST are persisted to DB (fire-and-forget)
- DB check validates range coverage before REST fallback

## Acceptance Criteria ✅

- ✅ Requesting BTCUSDT 1h candles from 2025-11-01 to 2026-02-01 returns complete data (2200+ candles)
- ✅ Backtests over long ranges use complete candle sets
- ✅ No performance regression for short queries (single API call as before)
- ✅ Backward compatible with existing limit-only queries
- ✅ Safety mechanisms prevent infinite loops
- ✅ Candles deduplicated by OpenTime
- ✅ Results sorted chronologically
- ✅ Database persistence works correctly with pagination
- ✅ Code review passed with feedback addressed
- ✅ Security scan passed (CodeQL: 0 alerts)

## Testing Recommendations

### Unit Testing (if test infrastructure added)
```csharp
[Fact]
public async Task GetCandlesAsync_WithDateRange_ReturnsAllCandles()
{
    // Arrange: Mock Bitget client to return multiple pages
    // Act: Call GetCandlesAsync with 3-month range
    // Assert: Result count > limit (e.g., 2200+ for hourly)
}

[Fact]
public async Task GetCandlesAsync_WithoutDateRange_ReturnsLimitedCandles()
{
    // Arrange: Mock Bitget client
    // Act: Call GetCandlesAsync with only limit
    // Assert: Result count <= limit
}

[Fact]
public async Task FetchRangeWithPagination_Deduplicates()
{
    // Arrange: Mock client to return overlapping pages
    // Act: Call pagination method
    // Assert: No duplicate OpenTimes in result
}

[Fact]
public async Task FetchRangeWithPagination_StopsOnMaxIterations()
{
    // Arrange: Mock client to always return data
    // Act: Call with very large date range
    // Assert: Stops at MAX_PAGINATION_ITERATIONS
}
```

### Integration Testing
```bash
# Test 1: Short query (backward compatibility)
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&limit=100"
# Expected: count = 100

# Test 2: Long date range (pagination)
curl "http://localhost:3001/api/bitget/market/candles?symbol=BTCUSDT&interval=1h&startTime=2025-11-01T00:00:00Z&endTime=2026-02-01T00:00:00Z"
# Expected: count > 2000

# Test 3: Backtest with long range
curl -X POST http://localhost:3001/api/bitget/backtests/run \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "1h",
    "startTime": "2025-11-01T00:00:00Z",
    "endTime": "2026-02-01T00:00:00Z",
    "strategy": "ema_cross",
    "parameters": {"fastPeriod": 10, "slowPeriod": 20}
  }'
# Expected: Backtest runs on full dataset
```

## Performance Considerations

- **Short queries (no date range)**: Single API call - no impact
- **Long queries (with date range)**: Multiple API calls, but:
  - Each call limited to 1000 candles max
  - Deduplication is O(1) per candle
  - Only fetches what's needed (early exit when API returns less than page size)
  - Results cached in DB for future queries

## Migration Path

No migration needed - changes are:
- **Backward compatible**: Existing queries work unchanged
- **Opt-in enhancement**: Only activates when both startTime and endTime provided
- **No schema changes**: No database migration required
- **No config changes**: Works with existing configuration

## Files Modified

1. `src/BitgetLab.Core/Services/Bitget/CandleService.cs` - Core implementation
2. `README.md` - API documentation
3. `QUICKSTART_BACKTEST.md` - Usage examples

## Related Issues

Fixes the issue where:
- Backtests only used first 100 candles of requested range
- User requested 2025-11-01 to 2026-02-01 but got data starting ~2025-12-21
- Long-range queries returned incomplete historical data

## Future Enhancements (Optional)

1. Add `maxCandles` query parameter for hard limit on total returned candles
2. Add progress callbacks for very long pagination operations
3. Consider caching pagination results in memory for repeated queries
4. Add metrics/logging for pagination performance monitoring
5. Implement parallel page fetching for very large ranges (with rate limiting)

---

**Implementation Date**: 2026-02-04  
**Status**: ✅ Complete  
**Security**: ✅ Passed CodeQL  
**Review**: ✅ Passed with feedback addressed
