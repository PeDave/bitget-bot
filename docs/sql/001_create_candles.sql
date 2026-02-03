-- Create candles table for persisting OHLCV data
-- This table stores historical and real-time candle data from Bitget exchange

CREATE TABLE IF NOT EXISTS candles (
    symbol VARCHAR(50) NOT NULL,
    interval VARCHAR(10) NOT NULL,
    open_time TIMESTAMP NOT NULL,
    open NUMERIC(20, 8) NOT NULL,
    high NUMERIC(20, 8) NOT NULL,
    low NUMERIC(20, 8) NOT NULL,
    close NUMERIC(20, 8) NOT NULL,
    volume NUMERIC(20, 8) NOT NULL,
    quote_volume NUMERIC(20, 8) NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Unique constraint on symbol, interval, and open_time
    CONSTRAINT pk_candles PRIMARY KEY (symbol, interval, open_time)
);

-- Index for efficient queries by symbol and interval with time range
CREATE INDEX IF NOT EXISTS idx_candles_symbol_interval_time 
ON candles (symbol, interval, open_time DESC);

-- Index for queries by updated_at (useful for syncing)
CREATE INDEX IF NOT EXISTS idx_candles_updated_at 
ON candles (updated_at DESC);

-- Comments for documentation
COMMENT ON TABLE candles IS 'OHLCV candle data from Bitget exchange with WebSocket updates and historical backfills';
COMMENT ON COLUMN candles.symbol IS 'Trading pair symbol (e.g., BTCUSDT)';
COMMENT ON COLUMN candles.interval IS 'Candle interval (e.g., 1m, 5m, 1h, 1d)';
COMMENT ON COLUMN candles.open_time IS 'Candle open time (UTC)';
COMMENT ON COLUMN candles.open IS 'Opening price';
COMMENT ON COLUMN candles.high IS 'Highest price';
COMMENT ON COLUMN candles.low IS 'Lowest price';
COMMENT ON COLUMN candles.close IS 'Closing price';
COMMENT ON COLUMN candles.volume IS 'Base asset volume';
COMMENT ON COLUMN candles.quote_volume IS 'Quote asset volume';
COMMENT ON COLUMN candles.updated_at IS 'Last update timestamp (UTC)';
