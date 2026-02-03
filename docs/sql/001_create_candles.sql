-- Candles table migration script
-- This script creates the table for storing historical candle data

CREATE TABLE IF NOT EXISTS candles (
    id BIGSERIAL PRIMARY KEY,
    symbol VARCHAR(50) NOT NULL,
    interval VARCHAR(10) NOT NULL,
    open_time TIMESTAMP NOT NULL,
    open NUMERIC(20, 8) NOT NULL,
    high NUMERIC(20, 8) NOT NULL,
    low NUMERIC(20, 8) NOT NULL,
    close NUMERIC(20, 8) NOT NULL,
    volume NUMERIC(20, 8) NOT NULL,
    quote_volume NUMERIC(20, 8) NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    
    -- Unique constraint to prevent duplicate candles
    CONSTRAINT unique_candle UNIQUE (symbol, interval, open_time)
);

-- Indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_candles_symbol_interval ON candles(symbol, interval);
CREATE INDEX IF NOT EXISTS idx_candles_open_time ON candles(open_time);
CREATE INDEX IF NOT EXISTS idx_candles_symbol_interval_time ON candles(symbol, interval, open_time DESC);

-- Comments
COMMENT ON TABLE candles IS 'Stores historical OHLCV candle data from Bitget exchange';
COMMENT ON COLUMN candles.symbol IS 'Trading pair symbol (e.g., BTCUSDT)';
COMMENT ON COLUMN candles.interval IS 'Candle interval (e.g., 1m, 5m, 1h, 1d)';
COMMENT ON COLUMN candles.open_time IS 'Candle open time (UTC)';
COMMENT ON COLUMN candles.open IS 'Opening price';
COMMENT ON COLUMN candles.high IS 'Highest price';
COMMENT ON COLUMN candles.low IS 'Lowest price';
COMMENT ON COLUMN candles.close IS 'Closing price';
COMMENT ON COLUMN candles.volume IS 'Base asset volume';
COMMENT ON COLUMN candles.quote_volume IS 'Quote asset volume';
