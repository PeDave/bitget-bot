# Charting Pipeline Design

## Overview

This document describes the design for extending the charting pipeline scaffolding to persist candle data to PostgreSQL and continuously update with real-time WebSocket data.

## Current Implementation

The current scaffolding provides:
- REST endpoints for fetching historical candles (`GET /api/bitget/market/candles`)
- REST endpoints for computing technical indicators (`GET /api/bitget/market/indicators`)
- In-memory WebSocket subscription service with subscribe/unsubscribe endpoints
- Basic subscription registry that stores latest candles in memory

## Proposed PostgreSQL Schema

### Tables

#### `candles` table
Stores historical OHLCV candle data for all symbols and intervals.

```sql
CREATE TABLE candles (
    id BIGSERIAL PRIMARY KEY,
    symbol VARCHAR(50) NOT NULL,
    interval VARCHAR(10) NOT NULL,
    open_time TIMESTAMP NOT NULL,
    open_price NUMERIC(20, 8) NOT NULL,
    high_price NUMERIC(20, 8) NOT NULL,
    low_price NUMERIC(20, 8) NOT NULL,
    close_price NUMERIC(20, 8) NOT NULL,
    volume NUMERIC(20, 8) NOT NULL,
    quote_volume NUMERIC(20, 8) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_candle UNIQUE (symbol, interval, open_time)
);

-- Indexes for efficient querying
CREATE INDEX idx_candles_symbol_interval ON candles (symbol, interval);
CREATE INDEX idx_candles_open_time ON candles (open_time);
CREATE INDEX idx_candles_symbol_interval_time ON candles (symbol, interval, open_time DESC);
```

#### `subscriptions` table (optional)
Tracks active WebSocket subscriptions for persistence across restarts.

```sql
CREATE TABLE subscriptions (
    id SERIAL PRIMARY KEY,
    symbol VARCHAR(50) NOT NULL,
    interval VARCHAR(10) NOT NULL,
    subscribed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_update TIMESTAMP,
    is_active BOOLEAN DEFAULT true,
    CONSTRAINT unique_subscription UNIQUE (symbol, interval)
);
```

## Update Strategy

### 1. Historical Data Backfill
When a new subscription is created:
1. Check if historical data exists in the database
2. If not, fetch historical candles from Bitget API (up to max limit)
3. Insert candles using UPSERT to avoid duplicates:

```sql
INSERT INTO candles (symbol, interval, open_time, open_price, high_price, low_price, close_price, volume, quote_volume)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
ON CONFLICT (symbol, interval, open_time)
DO UPDATE SET
    open_price = EXCLUDED.open_price,
    high_price = EXCLUDED.high_price,
    low_price = EXCLUDED.low_price,
    close_price = EXCLUDED.close_price,
    volume = EXCLUDED.volume,
    quote_volume = EXCLUDED.quote_volume,
    updated_at = CURRENT_TIMESTAMP
RETURNING id;
```

### 2. Real-time WebSocket Updates
When a WebSocket message is received:
1. Parse the candle update from the Bitget WebSocket feed
2. UPSERT the candle to the database using the same query as above
3. Update in-memory cache for quick access
4. Broadcast to any connected clients (if implementing SignalR/WebSockets)

### 3. WebSocket Connection Management

Implement in `WebSocketSubscriptionService`:

```csharp
public class WebSocketSubscriptionService : IWebSocketSubscriptionService, IHostedService
{
    private readonly IBitgetSocketClient _socketClient;
    private readonly ICandleRepository _candleRepository;
    private readonly ILogger _logger;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Restore subscriptions from database
        var activeSubscriptions = await _candleRepository.GetActiveSubscriptionsAsync();
        
        foreach (var sub in activeSubscriptions)
        {
            await SubscribeToWebSocketAsync(sub.Symbol, sub.Interval);
        }
    }
    
    private async Task SubscribeToWebSocketAsync(string symbol, string interval)
    {
        // Subscribe to Bitget WebSocket kline channel
        await _socketClient.SpotApiV2.SubscribeToKlineUpdatesAsync(
            symbol, 
            interval,
            async (update) => {
                // On candle update, persist to database
                await _candleRepository.UpsertCandleAsync(update);
                
                // Update in-memory cache
                UpdateInMemoryCandle(symbol, interval, update);
            });
    }
}
```

## Repository Pattern

Create a `ICandleRepository` interface:

```csharp
public interface ICandleRepository
{
    Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol, string interval, DateTime? startTime, DateTime? endTime, int limit);
    
    Task UpsertCandleAsync(CandleDto candle);
    
    Task UpsertCandlesAsync(IEnumerable<CandleDto> candles);
    
    Task<IEnumerable<SubscriptionDto>> GetActiveSubscriptionsAsync();
    
    Task SaveSubscriptionAsync(string symbol, string interval);
    
    Task RemoveSubscriptionAsync(string symbol, string interval);
}
```

## Migration Strategy

### Phase 1: Database Setup
1. Create PostgreSQL database and tables
2. Set up connection string in `appsettings.json`
3. Add Npgsql NuGet package for PostgreSQL access

### Phase 2: Repository Implementation
1. Implement `CandleRepository` with PostgreSQL
2. Update `CandleService` to check database before calling Bitget API
3. Implement caching layer (Redis or in-memory) for frequently accessed candles

### Phase 3: WebSocket Integration
1. Convert `WebSocketSubscriptionService` to `IHostedService`
2. Implement Bitget.Net WebSocket subscriptions
3. Add candle update handlers that persist to PostgreSQL
4. Implement subscription persistence across restarts

### Phase 4: Performance Optimization
1. Add Redis caching for hot data
2. Implement batch UPSERT for multiple candles
3. Add database indexes as needed
4. Consider partitioning the `candles` table by symbol or time range

## Configuration

Add to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=bitgetlab;Username=postgres;Password=your_password"
  },
  "Charting": {
    "EnableWebSocketSubscriptions": true,
    "MaxConcurrentSubscriptions": 50,
    "CandleRetentionDays": 90,
    "EnableCaching": true,
    "CacheExpirationMinutes": 5
  }
}
```

## Performance Considerations

1. **Batch Processing**: Group multiple candle updates and insert in batches
2. **Connection Pooling**: Use Npgsql connection pooling for better performance
3. **Indexes**: Ensure proper indexes on frequently queried columns
4. **Caching**: Cache frequently accessed candles in memory or Redis
5. **Partitioning**: Consider table partitioning for very large datasets
6. **Cleanup**: Implement periodic cleanup of old candle data based on retention policy

## Monitoring and Maintenance

1. Monitor WebSocket connection health and reconnect on failure
2. Track subscription metrics (active subscriptions, update frequency, errors)
3. Implement database cleanup job to remove old candles
4. Log WebSocket errors and reconnection attempts
5. Add health checks for database connectivity and WebSocket status

## Future Enhancements

1. Add support for multiple exchanges
2. Implement candle aggregation (e.g., build 1h candles from 1m candles)
3. Add real-time SignalR broadcasting to web clients
4. Implement candle gap detection and backfill
5. Add support for custom indicators stored in database
6. Implement candle compression for long-term storage
